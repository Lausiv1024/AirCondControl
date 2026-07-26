<script lang="ts" generics="T extends string | number">
  /** 排他選択のボタン列 (運転モード・タイマー種別・除湿強度で共用)。 */
  interface Props {
    options: readonly { value: T; label: string }[];
    value: T;
    disabled?: boolean;
    onselect: (value: T) => void;
  }

  let { options, value, disabled = false, onselect }: Props = $props();
</script>

<div class="segmented" role="group">
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
</style>
