<script lang="ts">
  /** −/+ で値を増減する大きな表示 (設定温度・タイマー時間で共用)。 */
  interface Props {
    text: string;
    unit?: string;
    disabled?: boolean;
    /** 表示の色味 (暖房時は暖色にする)。 */
    tone?: 'cool' | 'heat' | 'neutral';
    /** 「12時間00分」のような長い表示用に文字を小さくする。 */
    compact?: boolean;
    onstep: (delta: number) => void;
  }

  let {
    text,
    unit = '',
    disabled = false,
    tone = 'neutral',
    compact = false,
    onstep,
  }: Props = $props();
</script>

<div class="stepper">
  <button type="button" aria-label="下げる" {disabled} onclick={() => onstep(-1)}>−</button>

  <div class="value" class:compact data-tone={tone} aria-live="polite">
    <span class="number">{text}</span>
    {#if unit}<span class="unit">{unit}</span>{/if}
  </div>

  <button type="button" aria-label="上げる" {disabled} onclick={() => onstep(1)}>＋</button>
</div>

<style>
  .stepper {
    display: grid;
    grid-template-columns: 4rem minmax(0, 1fr) 4rem;
    align-items: center;
    gap: 0.75rem;
  }

  .stepper button {
    height: 4rem;
    font-size: 1.6rem;
    line-height: 1;
  }

  .value {
    text-align: center;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .number {
    font-size: 2.6rem;
    font-weight: 600;
  }

  /* 狭い画面でも溢れないよう、長い表示は文字を詰める。 */
  .value.compact .number {
    font-size: clamp(1.4rem, 7vw, 1.9rem);
  }

  .unit {
    font-size: 1.2rem;
    color: var(--muted);
    margin-left: 0.15rem;
  }

  .value[data-tone='cool'] .number {
    color: var(--cool);
  }

  .value[data-tone='heat'] .number {
    color: var(--heat);
  }
</style>
