<script lang="ts">
  import { onMount } from 'svelte';
  import { AircondStore } from '../aircond.svelte';
  import { Dehumidification, OperationMode, TimerMode } from '../types';
  import Segmented from './Segmented.svelte';
  import Stepper from './Stepper.svelte';

  const ac = new AircondStore();

  onMount(() => ac.start());

  const MODES = [
    { value: OperationMode.COOLING, label: '冷房' },
    { value: OperationMode.DEHUMIDIFICATION, label: '除湿' },
    { value: OperationMode.HEATING, label: '暖房' },
    { value: OperationMode.VENTILATION, label: '送風' },
  ] as const;

  const DEHUMID = [
    { value: Dehumidification.WEAK, label: '弱' },
    { value: Dehumidification.NORMAL, label: '標準' },
    { value: Dehumidification.STRONG, label: '強' },
  ] as const;

  const TIMERS = [
    { value: TimerMode.NONE, label: 'なし' },
    { value: TimerMode.OFFTIMER, label: '切タイマー' },
    { value: TimerMode.ONTIMER, label: '入タイマー' },
  ] as const;

  const tone = $derived(ac.state.operationMode === OperationMode.HEATING ? 'heat' : 'cool');
</script>

<section class="panel">
  <div class="card power">
    <div class="power-state">
      <span class="label">電源</span>
      <span class="value" class:on={ac.state.power}>{ac.state.power ? 'ON' : 'OFF'}</span>
    </div>
    <button
      type="button"
      class="power-button"
      class:on={ac.state.power}
      aria-pressed={ac.state.power}
      onclick={() => ac.togglePower()}
    >
      {ac.state.power ? '停止' : '運転'}
    </button>
  </div>

  <div class="card">
    <h2>運転モード</h2>
    <Segmented options={MODES} value={ac.state.operationMode} onselect={(m) => ac.setMode(m)} />
  </div>

  {#if ac.showTemperature}
    <div class="card">
      <h2>設定温度</h2>
      <Stepper
        text={ac.temperatureText}
        unit={ac.canEditTemperature ? '℃' : ''}
        disabled={!ac.canEditTemperature}
        {tone}
        onstep={(d) => ac.changeTemperature(d)}
      />
      {#if !ac.canEditTemperature}
        <p class="hint">送風では温度を設定できません。</p>
      {/if}
    </div>
  {:else}
    <div class="card">
      <h2>除湿強度</h2>
      <Segmented
        options={DEHUMID}
        value={ac.state.dehumidification}
        onselect={(d) => ac.setDehumidification(d)}
      />
    </div>
  {/if}

  <div class="card">
    <h2>タイマー</h2>
    <Segmented
      options={TIMERS}
      value={ac.state.timerMode}
      onselect={(m) => ac.setTimerMode(m)}
    />
    <div class="timer-value">
      <Stepper
        text={ac.timerText}
        compact
        disabled={!ac.canEditTimer}
        onstep={(d) => ac.changeTimer(d)}
      />
    </div>
    {#if ac.canEditTimer}
      <p class="hint">タイマーは送信した時刻を起点に開始します (30 分単位)。</p>
    {/if}
  </div>

  <div class="status" aria-live="polite">
    <span class="dot" class:ok={ac.connected} class:busy={ac.busy}></span>
    <span class="text">{ac.status || '待機中'}</span>
    <button type="button" class="refresh" disabled={ac.busy} onclick={() => ac.load()}>
      再取得
    </button>
  </div>
</section>

<style>
  .panel {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0.75rem;
  }

  .power {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .power-state .label {
    display: block;
    font-size: 0.8rem;
    color: var(--muted);
  }

  .power-state .value {
    font-size: 1.8rem;
    font-weight: 700;
    color: var(--muted);
  }

  .power-state .value.on {
    color: var(--ok);
  }

  .power-button {
    min-width: 7rem;
    height: 3.5rem;
    font-size: 1.1rem;
    font-weight: 600;
  }

  .power-button.on {
    background: var(--accent-weak);
    border-color: var(--accent);
  }

  .timer-value {
    margin-top: 0.75rem;
  }

  .hint {
    margin: 0.6rem 0 0;
    font-size: 0.8rem;
    color: var(--muted);
  }

  .status {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0 0.25rem;
    font-size: 0.85rem;
    color: var(--muted);
  }

  .status .text {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .dot {
    width: 0.6rem;
    height: 0.6rem;
    border-radius: 50%;
    background: var(--err);
    flex: none;
  }

  .dot.ok {
    background: var(--ok);
  }

  .dot.busy {
    background: var(--accent);
  }

  .refresh {
    padding: 0.4rem 0.75rem;
    font-size: 0.8rem;
    border-radius: 8px;
  }
</style>
