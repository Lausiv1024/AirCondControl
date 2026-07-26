import { api, ApiError } from './api';
import {
  Dehumidification,
  MAX_DEGREE,
  MAX_TIMER,
  MIN_DEGREE,
  MIN_TIMER,
  OperationMode,
  TimerMode,
  TIMER_STEP,
  defaultState,
  type AircondState,
} from './types';

/**
 * エアコン操作の状態。CoolerSystemUI (Avalonia) の AircondViewModel と同じ同期方式をとる:
 *  - 自動送信: 値を変えると一定時間 (デバウンス) 操作が止まった時点で POST /apiac。連打中は送らない。
 *  - ポーリング同期: 一定間隔で GET /acget し、編集中・送信中でなければ画面に反映。
 *  - エコー防止: サーバ状態の反映中は自動送信をトリガしない。
 */
export class AircondStore {
  #debounceMs: number;
  #pollMs: number;

  #debounceTimer: ReturnType<typeof setTimeout> | undefined;
  #pollTimer: ReturnType<typeof setInterval> | undefined;

  /** true の間はサーバ状態の反映中なので自動送信しない。 */
  #applyingServerState = false;
  /** 送信予定 / 送信中。ポーリングによる上書きを抑止する。 */
  #sendPending = false;

  state = $state<AircondState>(defaultState());
  busy = $state(false);
  connected = $state(false);
  status = $state('');

  /** 温度を設定できるモード (冷房・暖房) か。 */
  canEditTemperature = $derived(
    this.state.operationMode === OperationMode.COOLING ||
      this.state.operationMode === OperationMode.HEATING,
  );

  /** 設定温度エリアを出すか。除湿時は代わりに除湿強度を出す。 */
  showTemperature = $derived(this.state.operationMode !== OperationMode.DEHUMIDIFICATION);

  /** 現在モードでの設定温度。 */
  displayDegree = $derived(
    this.state.operationMode === OperationMode.HEATING
      ? this.state.heatingDegrees
      : this.state.coolingDegrees,
  );

  temperatureText = $derived(this.canEditTemperature ? `${this.displayDegree}` : '--');

  canEditTimer = $derived(this.state.timerMode !== TimerMode.NONE);

  timerText = $derived(
    this.canEditTimer
      ? `${Math.floor(this.state.timerLength / 60)}時間${String(this.state.timerLength % 60).padStart(2, '0')}分`
      : '--',
  );

  constructor(debounceMs = 800, pollMs = 10_000) {
    this.#debounceMs = debounceMs;
    this.#pollMs = pollMs;
  }

  /** 初期ロードとポーリング開始。onMount から呼び、返り値を破棄処理に使う。 */
  start(): () => void {
    void this.load();
    if (this.#pollMs > 0) this.#pollTimer = setInterval(() => void this.#poll(), this.#pollMs);

    return () => {
      clearInterval(this.#pollTimer);
      clearTimeout(this.#debounceTimer);
    };
  }

  async load(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      this.#applyServerState(await api.getState());
      this.connected = true;
      this.status = '現在の状態を取得しました';
    } catch (e) {
      this.connected = false;
      this.status = message(e);
    } finally {
      this.busy = false;
    }
  }

  // --- 操作 -----------------------------------------------------------------

  togglePower(): void {
    this.#edit((s) => (s.power = !s.power));
  }

  setMode(mode: OperationMode): void {
    this.#edit((s) => (s.operationMode = mode));
  }

  setTimerMode(mode: TimerMode): void {
    this.#edit((s) => (s.timerMode = mode));
  }

  setDehumidification(level: Dehumidification): void {
    this.#edit((s) => (s.dehumidification = level));
  }

  changeTemperature(delta: number): void {
    if (!this.canEditTemperature) return;
    this.#edit((s) => {
      if (s.operationMode === OperationMode.HEATING)
        s.heatingDegrees = clamp(s.heatingDegrees + delta, MIN_DEGREE, MAX_DEGREE);
      else s.coolingDegrees = clamp(s.coolingDegrees + delta, MIN_DEGREE, MAX_DEGREE);
    });
  }

  changeTimer(steps: number): void {
    if (!this.canEditTimer) return;
    this.#edit((s) => {
      s.timerLength = clamp(s.timerLength + steps * TIMER_STEP, MIN_TIMER, MAX_TIMER);
    });
  }

  /** デバウンスを待たずに即時送信する。 */
  apply(): Promise<void> {
    clearTimeout(this.#debounceTimer);
    return this.#send();
  }

  // --- 内部 -----------------------------------------------------------------

  /** 状態を変更し、自動送信をスケジュールする。 */
  #edit(mutate: (s: AircondState) => void): void {
    mutate(this.state);
    if (this.#applyingServerState) return;

    this.#sendPending = true;
    this.status = '変更を送信予定…';
    clearTimeout(this.#debounceTimer);
    this.#debounceTimer = setTimeout(() => void this.#send(), this.#debounceMs);
  }

  async #send(): Promise<void> {
    // 送信中に呼ばれたら少し待って再試行する (最新の状態を送るため)。
    if (this.busy) {
      this.#debounceTimer = setTimeout(() => void this.#send(), this.#debounceMs);
      return;
    }

    this.#sendPending = false;
    this.busy = true;
    try {
      await api.apply({ ...this.state, degree: this.displayDegree });
      this.connected = true;
      this.status = '送信しました';
    } catch (e) {
      this.connected = false;
      this.status = message(e);
    } finally {
      this.busy = false;
    }
  }

  async #poll(): Promise<void> {
    // 編集中・送信予定中・送信中はサーバ状態で上書きしない。
    if (this.#sendPending || this.busy) return;

    let next: AircondState;
    try {
      next = await api.getState();
    } catch {
      this.connected = false;
      return;
    }

    // 取得中に編集が始まっていないか再確認する。
    if (this.#sendPending || this.busy) return;

    this.connected = true;
    const normalized = normalize(next);
    if (equals(normalized, this.state)) return;

    this.#applyServerState(next);
    this.status = 'サーバの変更を反映しました';
  }

  /** サーバ状態を画面に反映する。反映中は自動送信しない (エコー防止)。 */
  #applyServerState(server: AircondState): void {
    this.#applyingServerState = true;
    try {
      this.state = normalize(server);
    } finally {
      this.#applyingServerState = false;
    }
  }
}

/** 定義外の値 (古い設定ファイル等) が来たら既定値に丸める。 */
function normalize(s: AircondState): AircondState {
  const known = <T extends number>(value: T, allowed: readonly T[], fallback: T) =>
    allowed.includes(value) ? value : fallback;

  return {
    power: !!s.power,
    coolingDegrees: clampOr(s.coolingDegrees, MIN_DEGREE, MAX_DEGREE, 28),
    heatingDegrees: clampOr(s.heatingDegrees, MIN_DEGREE, MAX_DEGREE, 20),
    degree: s.degree,
    operationMode: known(s.operationMode, Object.values(OperationMode), OperationMode.COOLING),
    timerMode: known(s.timerMode, Object.values(TimerMode), TimerMode.NONE),
    dehumidification: known(
      s.dehumidification,
      Object.values(Dehumidification),
      Dehumidification.NORMAL,
    ),
    timerLength: clamp(s.timerLength, MIN_TIMER, MAX_TIMER),
  };
}

function equals(a: AircondState, b: AircondState): boolean {
  return (
    a.power === b.power &&
    a.coolingDegrees === b.coolingDegrees &&
    a.heatingDegrees === b.heatingDegrees &&
    a.operationMode === b.operationMode &&
    a.timerMode === b.timerMode &&
    a.dehumidification === b.dehumidification &&
    a.timerLength === b.timerLength
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/** 範囲外なら fallback に落とす (クランプではなく既定値へ)。 */
function clampOr(value: number, min: number, max: number, fallback: number): number {
  return value < min || value > max ? fallback : value;
}

export function message(e: unknown): string {
  if (e instanceof ApiError) return e.message;
  return e instanceof Error ? e.message : '不明なエラー';
}
