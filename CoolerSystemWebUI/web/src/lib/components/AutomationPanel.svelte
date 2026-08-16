<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '../api';
  import { message } from '../aircond.svelte';
  import type { AutomationConfig } from '../types';

  let config = $state<AutomationConfig>({ aircondPwrLink: false });
  let busy = $state(false);
  let status = $state('');

  onMount(async () => {
    try {
      config = await api.getAutomation();
      status = '現在の設定を取得しました';
    } catch (e) {
      status = `取得に失敗しました: ${message(e)}`;
    }
  });

  /** トグルは即時反映する。失敗したら元に戻す。 */
  async function toggle(key: keyof AutomationConfig) {
    if (busy) return;
    const previous = config[key];
    config[key] = !previous;

    busy = true;
    try {
      await api.setAutomation({ ...config });
      status = '保存しました';
    } catch (e) {
      config[key] = previous;
      status = `保存に失敗しました: ${message(e)}`;
    } finally {
      busy = false;
    }
  }
</script>

<section class="panel">
  <div class="card">
    <h2>自動化</h2>

    <label class="row">
      <span class="text">
        <span class="title">電源連動</span>
        <span class="desc">エアコンの ON/OFF に合わせてサーキュレーターの電源も送る。</span>
      </span>
      <input
        type="checkbox"
        role="switch"
        checked={config.aircondPwrLink}
        disabled={busy}
        onchange={() => toggle('aircondPwrLink')}
      />
    </label>
  </div>

  <div class="status" aria-live="polite">{status || '待機中'}</div>
</section>

<style>
  .panel {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0.75rem;
  }

  .row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    padding: 0.9rem 0;
    border-top: 1px solid var(--line);
  }

  .row:first-of-type {
    border-top: none;
  }

  .text {
    display: grid;
    gap: 0.2rem;
  }

  .title {
    font-size: 1rem;
  }

  .desc {
    font-size: 0.8rem;
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

  .status {
    padding: 0 0.25rem;
    font-size: 0.85rem;
    color: var(--muted);
  }
</style>
