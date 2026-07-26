<script lang="ts" generics="T extends string | number">
  /** 排他選択のボタン列 (運転モード・タイマー種別・除湿強度で共用)。 */
  interface Props {
    options: readonly { value: T; label: string }[];
    value: T;
    disabled?: boolean;
    /** 選択肢が多い・幅が狭い場所 (グラフの表示範囲切り替えなど) 向けの小さい見た目。 */
    compact?: boolean;
    onselect: (value: T) => void;
  }

  let { options, value, disabled = false, compact = false, onselect }: Props = $props();
</script>

<div class="segmented" class:compact role="group">
  {#each options as option (option.value)}
    <button
      type="button"
      class:selected={option.value === value}
      aria-pressed={option.value === value}
      {disabled}
      onclick={() => onselect(option.value)}
    >
      {option.label}
    </button>
  {/each}
</div>

<style>
  .segmented {
    display: grid;
    grid-auto-flow: column;
    /* 1fr だと min-content で押し広げられるので minmax(0, 1fr) にする。 */
    grid-auto-columns: minmax(0, 1fr);
    gap: 0.5rem;
  }

  button {
    min-width: 0;
    padding: 0.8rem 0.3rem;
    font-size: 0.95rem;
    white-space: nowrap;
  }

  .segmented.compact {
    gap: 0.3rem;
  }

  .segmented.compact button {
    padding: 0.4rem 0.15rem;
    font-size: 0.78rem;
  }
</style>
