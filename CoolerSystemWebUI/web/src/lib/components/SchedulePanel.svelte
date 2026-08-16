<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '../api';
  import { message } from '../aircond.svelte';
  import {
    DAY_LABELS,
    DAYS_EVERY,
    DAYS_NONE,
    DAYS_WEEKDAYS,
    DAYS_WEEKEND,
    Dehumidification,
    MAX_DEGREE,
    MIN_DEGREE,
    OperationMode,
    type ScheduleConfig,
    type ScheduleRule,
    type TimerStatus,
  } from '../types';

  const TIMER_POLL_MS = 30_000;

  const MODE_OPTIONS: { value: OperationMode; label: string }[] = [
    { value: OperationMode.COOLING, label: '冷房' },
    { value: OperationMode.DEHUMIDIFICATION, label: '除湿' },
    { value: OperationMode.HEATING, label: '暖房' },
    { value: OperationMode.VENTILATION, label: '送風' },
  ];

  const DEHUMID_OPTIONS: { value: Dehumidification; label: string }[] = [
    { value: Dehumidification.STRONG, label: '強' },
    { value: Dehumidification.NORMAL, label: '標準' },
    { value: Dehumidification.WEAK, label: '弱' },
  ];

  const PRESETS: { label: string; days: number }[] = [
    { label: '毎日', days: DAYS_EVERY },
    { label: '平日', days: DAYS_WEEKDAYS },
    { label: '土日', days: DAYS_WEEKEND },
  ];

  let config = $state<ScheduleConfig>({ enabled: true, rules: [] });
  let timer = $state<TimerStatus | undefined>(undefined);
  let loaded = $state(false);
  let dirty = $state(false);
  let busy = $state(false);
  let status = $state('読み込み中…');
  let openId = $state<string | null>(null);
  /** 残り時間の表示を毎秒書き換えるための現在時刻。 */
  let nowMs = $state(Date.now());

  onMount(() => {
    void load();
    const timerPoll = setInterval(() => void loadTimer(), TIMER_POLL_MS);
    const clock = setInterval(() => (nowMs = Date.now()), 1000);
    return () => {
      clearInterval(timerPoll);
      clearInterval(clock);
    };
  });

  async function load(): Promise<void> {
    try {
      config = await api.getSchedule();
      loaded = true;
      status = '';
      await loadTimer();
    } catch (e) {
      status = `取得に失敗しました: ${message(e)}`;
    }
  }

  async function loadTimer(): Promise<void> {
    try {
      timer = await api.getTimer();
    } catch {
      // タイマー表示は補助情報なので、失敗しても本体の操作は続けさせる。
    }
  }

  async function save(): Promise<void> {
    if (busy) return;
    busy = true;
    try {
      await api.setSchedule($state.snapshot(config));
      dirty = false;
      status = '保存しました';
    } catch (e) {
      status = `保存に失敗しました: ${message(e)}`;
    } finally {
      busy = false;
    }
  }

  function touch(): void {
    dirty = true;
    status = '';
  }

  function addRule(): void {
    const rule: ScheduleRule = {
      id: crypto.randomUUID().replace(/-/g, ''),
      enabled: true,
      name: '',
      at: '07:00',
      days: DAYS_EVERY,
      date: null,
      power: null,
      operationMode: null,
      degrees: null,
      dehumidification: null,
      offAfterMinutes: null,
    };
    config.rules.push(rule);
    openId = rule.id;
    touch();
  }

  function removeRule(id: string): void {
    config.rules = config.rules.filter((r) => r.id !== id);
    if (openId === id) openId = null;
    touch();
  }

  function toggleDay(rule: ScheduleRule, bit: number): void {
    rule.days = rule.days & bit ? rule.days & ~bit : rule.days | bit;
    // 曜日を選んだら繰り返し扱いにするので、単発予約の日付は捨てる。
    if (rule.days !== DAYS_NONE) rule.date = null;
    touch();
  }

  function setPreset(rule: ScheduleRule, days: number): void {
    rule.days = days;
    rule.date = null;
    touch();
  }

  /** 単発予約に切り替える。曜日指定とは排他。 */
  function setOnce(rule: ScheduleRule): void {
    rule.days = DAYS_NONE;
    rule.date ??= new Date().toISOString().slice(0, 10);
    touch();
  }

  /** 空文字を null (＝変更しない) に落とす。 */
  function num(value: string): number | null {
    const n = Number(value);
    return value.trim() === '' || Number.isNaN(n) ? null : n;
  }

  function summarize(rule: ScheduleRule): string {
    const parts: string[] = [];
    if (rule.power === true) parts.push('電源ON');
    if (rule.power === false) parts.push('電源OFF');
    if (rule.operationMode !== null)
      parts.push(MODE_OPTIONS.find((m) => m.value === rule.operationMode)?.label ?? '');
    if (rule.degrees !== null) parts.push(`${rule.degrees}℃`);
    if (rule.dehumidification !== null)
      parts.push(`除湿${DEHUMID_OPTIONS.find((d) => d.value === rule.dehumidification)?.label}`);
    if (rule.offAfterMinutes !== null) parts.push(`${rule.offAfterMinutes}分後にOFF`);
    return parts.length ? parts.join(' / ') : '動作が未設定';
  }

  function repeatLabel(rule: ScheduleRule): string {
    if (rule.days === DAYS_NONE) return rule.date ? `${rule.date} に1回` : '日付未設定';
    if (rule.days === DAYS_EVERY) return '毎日';
    if (rule.days === DAYS_WEEKDAYS) return '平日';
    if (rule.days === DAYS_WEEKEND) return '土日';
    return DAY_LABELS.filter((_, i) => rule.days & (1 << i)).join('・');
  }

  /** 発火予定までの残りを「1時間30分」形式で返す。 */
  function remaining(iso: string): string {
    const totalMin = Math.max(0, Math.round((Date.parse(iso) - nowMs) / 60_000));
    const h = Math.floor(totalMin / 60);
    return h > 0 ? `${h}時間${String(totalMin % 60).padStart(2, '0')}分` : `${totalMin}分`;
  }

  const countdowns = $derived(
    [
      { label: '切タイマー', iso: timer?.offAtUtc },
      { label: '入タイマー', iso: timer?.onAtUtc },
    ].filter((c): c is { label: string; iso: string } => !!c.iso),
  );
</script>

<section class="panel">
  {#if countdowns.length}
    <div class="card countdowns">
      {#each countdowns as c (c.label)}
        <div class="countdown">
          <span class="cd-label">{c.label}</span>
          <span class="cd-value">あと {remaining(c.iso)}</span>
        </div>
      {/each}
    </div>
  {/if}

  <div class="card">
    <label class="row master">
      <span class="text">
        <span class="title">スケジュール</span>
        <span class="desc">オフにすると、下のルールは一切実行されません。</span>
      </span>
      <input
        type="checkbox"
        role="switch"
        checked={config.enabled}
        disabled={!loaded || busy}
        onchange={(e) => {
          config.enabled = e.currentTarget.checked;
          touch();
        }}
      />
    </label>
  </div>

  {#each config.rules as rule (rule.id)}
    {@const isOpen = openId === rule.id}
    <div class="card rule" class:disabled={!rule.enabled}>
      <div class="rule-head">
        <input
          type="checkbox"
          role="switch"
          checked={rule.enabled}
          onchange={(e) => {
            rule.enabled = e.currentTarget.checked;
            touch();
          }}
        />
        <button type="button" class="head-main" onclick={() => (openId = isOpen ? null : rule.id)}>
          <span class="at">{rule.at}</span>
          <span class="meta">
            <span class="rule-name">{rule.name || '(名前なし)'}</span>
            <span class="sub">{repeatLabel(rule)} · {summarize(rule)}</span>
          </span>
          <span class="chevron" class:open={isOpen}>▾</span>
        </button>
      </div>

      {#if isOpen}
        <div class="body">
          <label class="field">
            <span class="label">名前</span>
            <input
              type="text"
              placeholder="就寝前に冷房を弱める など"
              value={rule.name}
              oninput={(e) => {
                rule.name = e.currentTarget.value;
                touch();
              }}
            />
          </label>

          <label class="field">
            <span class="label">時刻</span>
            <input
              type="time"
              value={rule.at}
              onchange={(e) => {
                rule.at = e.currentTarget.value;
                touch();
              }}
            />
          </label>

          <div class="field">
            <span class="label">繰り返し</span>
            <div class="days">
              {#each DAY_LABELS as day, i (day)}
                <button
                  type="button"
                  class="day"
                  class:selected={(rule.days & (1 << i)) !== 0}
                  onclick={() => toggleDay(rule, 1 << i)}>{day}</button
                >
              {/each}
            </div>
            <div class="presets">
              {#each PRESETS as p (p.label)}
                <button
                  type="button"
                  class="preset"
                  class:selected={rule.days === p.days}
                  onclick={() => setPreset(rule, p.days)}>{p.label}</button
                >
              {/each}
              <button
                type="button"
                class="preset"
                class:selected={rule.days === DAYS_NONE}
                onclick={() => setOnce(rule)}>単発</button
              >
            </div>
            {#if rule.days === DAYS_NONE}
              <input
                type="date"
                value={rule.date ?? ''}
                onchange={(e) => {
                  rule.date = e.currentTarget.value || null;
                  touch();
                }}
              />
              <span class="hint">実行すると自動で無効になります。</span>
            {/if}
          </div>

          <div class="divider"></div>
          <span class="section-label">この時刻にやること (未指定の項目は変更しません)</span>

          <label class="field">
            <span class="label">電源</span>
            <select
              value={rule.power === null ? '' : String(rule.power)}
              onchange={(e) => {
                const v = e.currentTarget.value;
                rule.power = v === '' ? null : v === 'true';
                touch();
              }}
            >
              <option value="">変更しない</option>
              <option value="true">入れる</option>
              <option value="false">切る</option>
            </select>
          </label>

          <label class="field">
            <span class="label">運転モード</span>
            <select
              value={rule.operationMode === null ? '' : String(rule.operationMode)}
              onchange={(e) => {
                const v = e.currentTarget.value;
                rule.operationMode = v === '' ? null : (Number(v) as OperationMode);
                touch();
              }}
            >
              <option value="">変更しない</option>
              {#each MODE_OPTIONS as m (m.value)}
                <option value={String(m.value)}>{m.label}</option>
              {/each}
            </select>
          </label>

          <label class="field">
            <span class="label">設定温度</span>
            <input
              type="number"
              inputmode="numeric"
              min={MIN_DEGREE}
              max={MAX_DEGREE}
              placeholder="変更しない"
              value={rule.degrees ?? ''}
              oninput={(e) => {
                rule.degrees = num(e.currentTarget.value);
                touch();
              }}
            />
            <span class="hint">冷房・暖房のときだけ反映されます。</span>
          </label>

          <label class="field">
            <span class="label">除湿強度</span>
            <select
              value={rule.dehumidification === null ? '' : String(rule.dehumidification)}
              onchange={(e) => {
                const v = e.currentTarget.value;
                rule.dehumidification = v === '' ? null : (Number(v) as Dehumidification);
                touch();
              }}
            >
              <option value="">変更しない</option>
              {#each DEHUMID_OPTIONS as d (d.value)}
                <option value={String(d.value)}>{d.label}</option>
              {/each}
            </select>
          </label>

          <label class="field">
            <span class="label">何分後にOFF</span>
            <input
              type="number"
              inputmode="numeric"
              min="1"
              placeholder="タイマーは変更しない"
              value={rule.offAfterMinutes ?? ''}
              oninput={(e) => {
                rule.offAfterMinutes = num(e.currentTarget.value);
                touch();
              }}
            />
            <span class="hint">この時刻から数え直します。以降の設定変更では延びません。</span>
          </label>

          <button type="button" class="remove" onclick={() => removeRule(rule.id)}>
            このルールを削除
          </button>
        </div>
      {/if}
    </div>
  {/each}

  {#if loaded && config.rules.length === 0}
    <div class="card empty">ルールがありません。</div>
  {/if}

  <div class="actions">
    <button type="button" onclick={addRule} disabled={!loaded || busy}>ルールを追加</button>
    <button type="button" class="selected" onclick={save} disabled={!dirty || busy}>
      {busy ? '保存中…' : '保存'}
    </button>
  </div>

  <div class="status" aria-live="polite">
    {status || (dirty ? '未保存の変更があります' : '待機中')}
  </div>
</section>

<style>
  .panel {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0.75rem;
  }

  .countdowns {
    display: grid;
    gap: 0.5rem;
  }

  .countdown {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 1rem;
  }

  .cd-label {
    font-size: 0.85rem;
    color: var(--muted);
  }

  .cd-value {
    font-size: 1.1rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  }

  .row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .text {
    display: grid;
    gap: 0.2rem;
  }

  .title {
    font-size: 1rem;
  }

  .desc,
  .hint {
    font-size: 0.8rem;
    color: var(--muted);
  }

  .rule.disabled {
    opacity: 0.55;
  }

  .rule-head {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .head-main {
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 0.75rem;
    background: none;
    border: none;
    padding: 0;
    text-align: left;
    cursor: pointer;
  }

  .at {
    font-size: 1.35rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    flex: none;
  }

  .meta {
    display: grid;
    gap: 0.15rem;
    min-width: 0;
    flex: 1;
  }

  .rule-name {
    font-size: 0.9rem;
  }

  .sub {
    font-size: 0.75rem;
    color: var(--muted);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .chevron {
    color: var(--muted);
    flex: none;
    transition: transform 0.2s ease;
  }

  .chevron.open {
    transform: rotate(180deg);
  }

  .body {
    display: grid;
    gap: 0.8rem;
    margin-top: 1rem;
    padding-top: 0.9rem;
    border-top: 1px solid var(--line);
  }

  .field {
    display: grid;
    gap: 0.35rem;
  }

  .label,
  .section-label {
    font-size: 0.8rem;
    color: var(--muted);
  }

  .section-label {
    font-weight: 600;
  }

  .divider {
    border-top: 1px solid var(--line);
    margin-top: 0.2rem;
  }

  input[type='text'],
  input[type='time'],
  input[type='date'],
  input[type='number'],
  select {
    width: 100%;
    min-width: 0;
    padding: 0.6rem 0.7rem;
    font: inherit;
    font-size: 0.95rem;
    color: var(--text);
    background: var(--surface-2);
    border: 1px solid var(--line);
    border-radius: 10px;
  }

  input:focus-visible,
  select:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: 1px;
  }

  .days {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: minmax(0, 1fr);
    gap: 0.25rem;
  }

  .day {
    min-width: 0;
    padding: 0.5rem 0;
    font-size: 0.85rem;
  }

  .presets {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: minmax(0, 1fr);
    gap: 0.25rem;
  }

  .preset {
    min-width: 0;
    padding: 0.4rem 0;
    font-size: 0.78rem;
  }

  .remove {
    margin-top: 0.2rem;
    font-size: 0.85rem;
    color: var(--err);
  }

  .empty {
    font-size: 0.85rem;
    color: var(--muted);
    text-align: center;
  }

  .actions {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: minmax(0, 1fr);
    gap: 0.5rem;
  }

  .actions button {
    padding: 0.8rem 0.3rem;
  }

  .status {
    padding: 0 0.25rem;
    font-size: 0.85rem;
    color: var(--muted);
  }

  input[type='checkbox'] {
    appearance: none;
    flex: none;
    width: 3.25rem;
    height: 1.9rem;
    border-radius: 999px;
    background: var(--surface-2);
    border: 1px solid var(--line);
    position: relative;
    cursor: pointer;
    transition:
      background 0.15s ease,
      border-color 0.15s ease;
  }

  input[type='checkbox']::after {
    content: '';
    position: absolute;
    top: 50%;
    left: 0.2rem;
    width: 1.35rem;
    height: 1.35rem;
    border-radius: 50%;
    background: var(--muted);
    transform: translateY(-50%);
    transition:
      left 0.15s ease,
      background 0.15s ease;
  }

  input[type='checkbox']:checked {
    background: var(--accent-weak);
    border-color: var(--accent);
  }

  input[type='checkbox']:checked::after {
    left: calc(100% - 1.55rem);
    background: #fff;
  }

  input[type='checkbox']:disabled {
    opacity: 0.5;
  }
</style>
