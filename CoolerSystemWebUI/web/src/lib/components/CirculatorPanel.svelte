<script lang="ts">
  import { onMount } from 'svelte';
  import { api } from '../api';
  import { message } from '../aircond.svelte';

  /**
   * サーキュレーターは状態を持たない単発 IR 送信のみ (人が本体を見ながら押す前提)。
   * ボタンは GET /circulatorconfig の commands キーから作る。キーは RPi 側 circulator-ir.json と一致する。
   */
  const LABELS: Record<string, string> = {
    power: '電源',
    timer: 'タイマー',
    swing: '首振り',
    mode: 'モード',
    plus: '風量 ＋',
    minus: '風量 －',
  };

  /** 設定が取れない場合に使う既定のコマンド。 */
  const FALLBACK = ['power', 'timer', 'swing', 'mode', 'plus', 'minus'];

  let commands = $state<string[]>(FALLBACK);
  let busy = $state(false);
  let status = $state('');

  onMount(async () => {
    try {
      const config = await api.getCirculatorConfig();
      const keys = Object.keys(config?.commands ?? {});
      if (keys.length > 0) commands = keys;
    } catch {
      // 設定が取れなくても既定のボタンで操作はできるので、エラー表示はしない。
    }
  });

  async function send(id: string) {
    if (busy) return;
    busy = true;
    try {
      await api.sendCirculator(id);
      status = `${LABELS[id] ?? id} を送信しました`;
    } catch (e) {
      status = `送信に失敗しました: ${message(e)}`;
    } finally {
      busy = false;
    }
  }
</script>

<section class="panel">
  <div class="card">
    <h2>サーキュレーター</h2>
    <div class="grid">
      {#each commands as id (id)}
        <button type="button" disabled={busy} onclick={() => send(id)}>
          {LABELS[id] ?? id}
        </button>
      {/each}
    </div>
    <p class="hint">押すたびに IR コードを 1 回送ります (現在の状態は保持されません)。</p>
  </div>

  <div class="status" aria-live="polite">{status || '待機中'}</div>
</section>

<style>
  .panel {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0.75rem;
  }

  .grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.6rem;
  }

  .grid button {
    height: 3.75rem;
    font-size: 1rem;
  }

  .hint,
  .status {
    margin: 0.75rem 0 0;
    font-size: 0.85rem;
    color: var(--muted);
  }

  .status {
    padding: 0 0.25rem;
  }
</style>
