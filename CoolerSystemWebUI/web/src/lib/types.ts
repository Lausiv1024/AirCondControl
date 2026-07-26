/**
 * AEHAFmtSender (RPi) とやり取りする JSON の型。
 * サーバ側は System.Text.Json の既定設定なので、enum は「数値」でシリアライズされる。
 * 値は AEHAFmtSender.Shared.Models / AEHAFmtSender.IRFormats の enum と一致させること。
 */

export const OperationMode = {
  COOLING: 0x58,
  DEHUMIDIFICATION: 0x50,
  HEATING: 0x48,
  VENTILATION: 0x38,
} as const;
export type OperationMode = (typeof OperationMode)[keyof typeof OperationMode];

export const TimerMode = {
  NONE: 0x00,
  OFFTIMER: 0x03,
  ONTIMER: 0x05,
} as const;
export type TimerMode = (typeof TimerMode)[keyof typeof TimerMode];

export const Dehumidification = {
  STRONG: 0x0,
  NORMAL: 0x2,
  WEAK: 0x4,
} as const;
export type Dehumidification = (typeof Dehumidification)[keyof typeof Dehumidification];

/** GET /acget · POST /apiac のエアコン状態 (サーバ側 NP081)。 */
export interface AircondState {
  power: boolean;
  coolingDegrees: number;
  heatingDegrees: number;
  /** 現在モードの設定温度 (表示用)。サーバは受信時に再計算するため送っても無視される。 */
  degree: number;
  operationMode: OperationMode;
  timerMode: TimerMode;
  dehumidification: Dehumidification;
  /** タイマー時間 (分)。 */
  timerLength: number;
}

/** GET/POST /automationconfig */
export interface AutomationConfig {
  /** エアコンの電源に連動してサーキュレーターの電源も送る。 */
  aircondPwrLink: boolean;
  aircondAutoPower: boolean;
}

/** GET /circulatorconfig (表示は commands のキーのみ利用)。 */
export interface CirculatorConfig {
  remoteName: string;
  commands: Record<string, string>;
}

/** GET /sensordata/latest · GET /sensordata/history のセンサー値 (サーバ側 SensorReadingDto)。 */
export interface SensorReading {
  deviceId: string;
  /** ISO8601 (UTC)。 */
  timestampUtc: string;
  temperatureC: number;
  humidityPercent: number;
  pressureHpa: number;
  co2Ppm: number;
}

export const MIN_DEGREE = 16;
export const MAX_DEGREE = 31;
export const MIN_TIMER = 30;
export const MAX_TIMER = 720;
export const TIMER_STEP = 30;

export function defaultState(): AircondState {
  return {
    power: false,
    coolingDegrees: 28,
    heatingDegrees: 20,
    degree: 28,
    operationMode: OperationMode.COOLING,
    timerMode: TimerMode.NONE,
    dehumidification: Dehumidification.NORMAL,
    timerLength: MIN_TIMER,
  };
}
