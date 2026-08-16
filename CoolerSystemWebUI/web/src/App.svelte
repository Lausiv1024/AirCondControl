<script lang="ts">
  import AircondPanel from './lib/components/AircondPanel.svelte';
  import AutomationPanel from './lib/components/AutomationPanel.svelte';
  import CirculatorPanel from './lib/components/CirculatorPanel.svelte';
  import SchedulePanel from './lib/components/SchedulePanel.svelte';
  import SensorPanel from './lib/components/SensorPanel.svelte';

  type Tab = 'aircond' | 'circulator' | 'schedule' | 'automation' | 'sensor';

  const TABS: { id: Tab; label: string }[] = [
    { id: 'aircond', label: 'エアコン' },
    { id: 'circulator', label: 'サーキュレーター' },
    { id: 'schedule', label: '予約' },
    { id: 'automation', label: '自動化' },
    { id: 'sensor', label: 'センサー' },
  ];

  /** #circulator のようなハッシュで開くタブを直接指定できる。 */
  function tabFromHash(): Tab {
    const hash = location.hash.replace('#', '');
    return TABS.some((t) => t.id === hash) ? (hash as Tab) : 'aircond';
  }

  let tab = $state<Tab>(tabFromHash());

  function select(next: Tab) {
    tab = next;
    history.replaceState(null, '', `#${next}`);
  }
</script>

<div class="app">
  <header>
    <h1>エアコンリモート</h1>
  </header>

  <nav>
    {#each TABS as t (t.id)}
      <button
        type="button"
        class:selected={tab === t.id}
        aria-current={tab === t.id ? 'page' : undefined}
        onclick={() => select(t.id)}
      >
        {t.label}
      </button>
    {/each}
  </nav>

  <main>
    {#if tab === 'aircond'}
      <AircondPanel />
    {:else if tab === 'circulator'}
      <CirculatorPanel />
    {:else if tab === 'schedule'}
      <SchedulePanel />
    {:else if tab === 'automation'}
      <AutomationPanel />
    {:else}
      <SensorPanel />
    {/if}
  </main>
</div>

<style>
  .app {
    max-width: 520px;
    margin: 0 auto;
    padding: 1rem 1rem calc(1.5rem + env(safe-area-inset-bottom));
    display: grid;
    /* auto だと子の min-content 幅でカラムが押し広げられ、画面幅を超えてしまう。 */
    grid-template-columns: minmax(0, 1fr);
    gap: 0.9rem;
  }

  header h1 {
    margin: 0.25rem 0;
    font-size: 1.15rem;
    font-weight: 600;
    letter-spacing: 0.02em;
  }

  nav {
    display: grid;
    grid-auto-flow: column;
    grid-auto-columns: minmax(0, 1fr);
    gap: 0.5rem;
  }

  nav button {
    min-width: 0;
    padding: 0.7rem 0.35rem;
    font-size: 0.85rem;
    white-space: nowrap;
  }
</style>
