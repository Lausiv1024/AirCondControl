<script lang="ts">
  import { onMount } from 'svelte';
  import { slide } from 'svelte/transition';
  import { api, ApiError } from '../api';
  import type { SensorReading } from '../types';
  import Segmented from './Segmented.svelte';

  const POLL_MS = 30_000;

  /** グラフの表示範囲 (分)。値を Segmented にそのまま渡すので number 型を明示する。 */
  const RANGES: readonly { value: number; label: string }[] = [
    { value: 30, label: '30分' },
    { value: 60, label: '1時間' },
    { value: 360, label: '6時間' },
    { value: 720, label: '12時間' },
    { value: 1440, label: '24時間' },
  ];

  let latest = $state<SensorReading | undefined>(undefined);
  let history = $state<SensorReading[]>([]);
  let status = $state('読み込み中…');
  let connected = $state(false);
  let expandedKey = $state<string | null>(null);
  let tooltip = $state<{ visible: boolean; idx: number } | null>(null);
  let rangeMinutes = $state(1440);
  let loading = $state(false);

  let pollTimer: ReturnType<typeof setInterval> | undefined;
  /** 表示範囲を切り替えた直後は、前の範囲の応答が遅れて届くことがあるので捨てる。 */
  let loadSeq = 0;

  async function load(): Promise<void> {
    const seq = ++loadSeq;
    loading = true;
    try {
      const [nextLatest, nextHistory] = await Promise.all([
        api.getLatestSensor(),
        api.getSensorHistory(rangeMinutes),
      ]);
      if (seq !== loadSeq) return;
      latest = nextLatest;
      history = nextHistory ?? [];
      connected = true;
      status = latest ? '' : 'まだセンサーデータがありません';
    } catch (e) {
      if (seq !== loadSeq) return;
      connected = false;
      status = e instanceof ApiError ? e.message : '不明なエラー';
    } finally {
      if (seq === loadSeq) loading = false;
    }
  }

  function selectRange(minutes: number): void {
    if (minutes === rangeMinutes) return;
    rangeMinutes = minutes;
    tooltip = null;
    void load();
  }

  onMount(() => {
    void load();
    pollTimer = setInterval(() => void load(), POLL_MS);
    return () => clearInterval(pollTimer);
  });

  /** サーバは UTC を返すが、タイムゾーン指定が付かない場合があるので補う。 */
  function parseUtc(iso: string): Date {
    return new Date(/([Zz]|[+-]\d{2}:?\d{2})$/.test(iso) ? iso : iso + 'Z');
  }

  function formatTime(iso: string): string {
    return parseUtc(iso).toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' });
  }

  function toEpoch(timestamps: string[]): number[] {
    return timestamps.map((t) => parseUtc(t).getTime());
  }

  /**
   * 計測間隔の中央値の3倍を超える空きを「欠測」とみなす閾値。
   * ここを超えた区間は線をつながず、センサー停止が一直線に化けるのを防ぐ。
   */
  function gapThreshold(times: number[]): number {
    const deltas: number[] = [];
    for (let i = 1; i < times.length; i++) deltas.push(times[i] - times[i - 1]);
    if (deltas.length === 0) return Infinity;
    deltas.sort((a, b) => a - b);
    const median = deltas[Math.floor(deltas.length / 2)];
    return Math.max(median * 3, 60_000);
  }

  function sparkPath(
    values: number[],
    timestamps: string[],
    width: number,
    height: number,
    pad = 4,
  ): string {
    if (values.length < 2) return '';
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;

    // X は配列の添字ではなく実時刻に比例させる (欠測があっても位置がずれない)。
    const times = toEpoch(timestamps);
    const tMin = times[0];
    const tSpan = times[times.length - 1] - tMin || 1;
    const gap = gapThreshold(times);

    return values
      .map((v, i) => {
        const x = pad + (width - pad * 2) * ((times[i] - tMin) / tSpan);
        const y = pad + (height - pad * 2) * (1 - (v - min) / span);
        const cmd = i === 0 || times[i] - times[i - 1] > gap ? 'M' : 'L';
        return `${cmd}${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }

  /** 目盛りの候補間隔 (ms)。1分〜24時間。 */
  const TICK_STEPS_MS = [
    60_000, 2 * 60_000, 5 * 60_000, 10 * 60_000, 15 * 60_000, 30 * 60_000, 3_600_000,
    2 * 3_600_000, 3 * 3_600_000, 6 * 3_600_000, 12 * 3_600_000, 24 * 3_600_000,
  ];

  /** ローカル時刻のきりのいい位置に目盛りを置く。 */
  function timeTicks(tMin: number, tMax: number, target = 5): number[] {
    const span = tMax - tMin;
    if (span <= 0) return [tMin];
    const step =
      TICK_STEPS_MS.find((s) => s >= span / target) ?? TICK_STEPS_MS[TICK_STEPS_MS.length - 1];
    // getTimezoneOffset を打ち消してからの丸めで、JST の 00:00 / 06:00 等に揃える。
    const offset = new Date(tMin).getTimezoneOffset() * 60_000;
    const ticks: number[] = [];
    for (let t = Math.ceil((tMin - offset) / step) * step + offset; t <= tMax; t += step) {
      ticks.push(t);
    }
    return ticks;
  }

  /** 24時間を跨ぐと時刻だけでは日が判別できないので、日付境界は M/D で示す。 */
  function formatTick(epoch: number): string {
    const d = new Date(epoch);
    if (d.getHours() === 0 && d.getMinutes() === 0) {
      return `${d.getMonth() + 1}/${d.getDate()}`;
    }
    return d.toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' });
  }

  interface ChartData {
    /** 欠測で分断された区間ごとの線と塗り。 */
    segments: { line: string; area: string }[];
    /** 前後が欠測で孤立した点 (線として描けないので丸で示す)。 */
    dots: { x: number; y: number }[];
    points: { x: number; y: number }[];
    yTicks: { y: number; label: string }[];
    xTicks: { x: number; label: string }[];
    padLeft: number;
    padTop: number;
    padBottom: number;
    viewWidth: number;
    viewHeight: number;
    min: number;
    max: number;
    avg: string;
  }

  function buildDetailChart(values: number[], timestamps: string[], decimals: number): ChartData {
    const viewWidth = 560;
    const viewHeight = 180;
    const padLeft = 52, padRight = 12, padTop = 12, padBottom = 30;
    const w = viewWidth - padLeft - padRight;
    const h = viewHeight - padTop - padBottom;

    const min = Math.min(...values);
    const max = Math.max(...values);

    let niceMin: number, niceMax: number, niceStep: number;
    const tickCount = 5;
    if (max === min) {
      const base = Math.abs(min) < 0.0001 ? 1 : Math.abs(min) * 0.1;
      niceStep = base / (tickCount - 1);
      niceMin = min - base / 2;
      niceMax = min + base / 2;
    } else {
      const rawStep = (max - min) / (tickCount - 1);
      const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
      niceStep = Math.ceil(rawStep / magnitude) * magnitude;
      niceMin = Math.floor(min / niceStep) * niceStep;
      niceMax = niceMin + niceStep * (tickCount - 1);
    }
    const niceSpan = niceMax - niceMin;

    const toY = (v: number) => padTop + h * (1 - (v - niceMin) / niceSpan);

    // X は配列の添字ではなく実時刻に比例させる。センサーが停止していた区間も
    // 実際の長さぶん横軸を占めるので、グラフが圧縮されない。
    const times = toEpoch(timestamps);
    const tMin = times[0];
    const tSpan = times[times.length - 1] - tMin || 1;
    const toX = (t: number) => padLeft + w * ((t - tMin) / tSpan);

    const points = values.map((v, i) => ({ x: toX(times[i]), y: toY(v) }));

    // 欠測をまたいで線を引くと、存在しない計測値を捏造したように見える。区間で分ける。
    const gap = gapThreshold(times);
    const runs: { x: number; y: number }[][] = [];
    let run: { x: number; y: number }[] = [];
    points.forEach((p, i) => {
      if (i > 0 && times[i] - times[i - 1] > gap) {
        runs.push(run);
        run = [];
      }
      run.push(p);
    });
    runs.push(run);

    const bottomY = padTop + h;
    const segments = runs
      .filter((r) => r.length >= 2)
      .map((r) => {
        const line = r
          .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
          .join(' ');
        const area =
          line +
          ` L${r[r.length - 1].x.toFixed(1)},${bottomY} L${r[0].x.toFixed(1)},${bottomY} Z`;
        return { line, area };
      });
    const dots = runs.filter((r) => r.length === 1).map((r) => r[0]);

    const yTicks = Array.from({ length: tickCount }, (_, i) => {
      const v = niceMin + i * niceStep;
      return { y: toY(v), label: v.toFixed(decimals) };
    });

    const xTicks = timeTicks(tMin, times[times.length - 1]).map((t) => ({
      x: toX(t),
      label: formatTick(t),
    }));

    const avg = (values.reduce((s, v) => s + v, 0) / values.length).toFixed(decimals);

    return {
      segments,
      dots,
      points,
      yTicks,
      xTicks,
      padLeft,
      padTop,
      padBottom,
      viewWidth,
      viewHeight,
      min,
      max,
      avg,
    };
  }

  const metrics = $derived([
    {
      key: 'temperatureC',
      label: '温度',
      unit: '℃',
      decimals: 1,
      value: latest ? latest.temperatureC.toFixed(1) : '--',
      values: history.map((r) => r.temperatureC),
      timestamps: history.map((r) => r.timestampUtc),
      color: 'var(--cool)',
    },
    {
      key: 'humidityPercent',
      label: '湿度',
      unit: '%',
      decimals: 0,
      value: latest ? latest.humidityPercent.toFixed(0) : '--',
      values: history.map((r) => r.humidityPercent),
      timestamps: history.map((r) => r.timestampUtc),
      color: 'var(--accent)',
    },
    {
      key: 'pressureHpa',
      label: '気圧',
      unit: 'hPa',
      decimals: 1,
      value: latest ? latest.pressureHpa.toFixed(1) : '--',
      values: history.map((r) => r.pressureHpa),
      timestamps: history.map((r) => r.timestampUtc),
      color: 'var(--muted)',
    },
    {
      key: 'co2Ppm',
      label: 'CO2濃度',
      unit: 'ppm',
      decimals: 0,
      value: latest ? String(latest.co2Ppm) : '--',
      values: history.map((r) => r.co2Ppm),
      timestamps: history.map((r) => r.timestampUtc),
      color: 'var(--heat)',
    },
  ]);
</script>

<section class="panel">
  <div class="card status">
    <span class="dot" class:on={connected}></span>
    <span class="text">
      {#if latest}
        最終更新: {formatTime(latest.timestampUtc)} ({latest.deviceId})
      {:else}
        {status}
      {/if}
    </span>
  </div>

  {#each metrics as m (m.key)}
    {@const isExpanded = expandedKey === m.key}
    <div
      class="card metric"
      class:expanded={isExpanded}
      role="button"
      tabindex="0"
      onclick={(e) => {
        // 詳細エリア内 (表示範囲の切り替え・グラフ) のクリックでは畳まない。
        if ((e.target as Element).closest('.detail')) return;
        expandedKey = isExpanded ? null : m.key;
        tooltip = null;
      }}
      onkeydown={(e) => {
        // 範囲切り替えボタン上での Enter / Space はボタン側の操作。畳まない。
        if ((e.target as Element).closest('.detail')) return;
        if (e.key === 'Enter' || e.key === ' ') {
          expandedKey = isExpanded ? null : m.key;
          tooltip = null;
          e.preventDefault();
        }
      }}
    >
      <div class="metric-header">
        <h2>{m.label}</h2>
        <span class="chevron" class:open={isExpanded}>▾</span>
      </div>
      <div class="row">
        <div class="value">{m.value}<span class="unit">{m.unit}</span></div>
        {#if m.values.length >= 2 && !isExpanded}
          <svg class="spark" viewBox="0 0 160 40" preserveAspectRatio="none">
            <path
              d={sparkPath(m.values, m.timestamps, 160, 40)}
              fill="none"
              stroke={m.color}
              stroke-width="2"
            />
          </svg>
        {/if}
      </div>

      {#if isExpanded}
        <div class="detail" transition:slide={{ duration: 250 }}>
          <!-- データ不足でグラフが出せないときも範囲を変えられるよう、条件の外に置く。 -->
          <div class="range" class:busy={loading}>
            <Segmented options={RANGES} value={rangeMinutes} compact onselect={selectRange} />
          </div>

          {#if m.values.length >= 2}
            {@const chart = buildDetailChart(m.values, m.timestamps, m.decimals)}
            <div class="stats">
              <span>最小: <strong>{chart.min.toFixed(m.decimals)}{m.unit}</strong></span>
              <span>平均: <strong>{chart.avg}{m.unit}</strong></span>
              <span>最大: <strong>{chart.max.toFixed(m.decimals)}{m.unit}</strong></span>
            </div>
            <svg
              class="detail-chart"
              viewBox="0 0 {chart.viewWidth} {chart.viewHeight}"
              onmousemove={(e) => {
                const svgEl = e.currentTarget as SVGSVGElement;
                const rect = svgEl.getBoundingClientRect();
                const svgX = (e.clientX - rect.left) * (chart.viewWidth / rect.width);
                // 点は等間隔ではないので、X 座標が最も近い点を探す。
                let idx = 0;
                for (let i = 1; i < chart.points.length; i++) {
                  if (
                    Math.abs(chart.points[i].x - svgX) < Math.abs(chart.points[idx].x - svgX)
                  ) {
                    idx = i;
                  }
                }
                tooltip = { visible: true, idx };
              }}
              onmouseleave={() => {
                tooltip = null;
              }}
            >
              <defs>
                <linearGradient id="grad-{m.key}" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" style="stop-color: {m.color}; stop-opacity: 0.28" />
                  <stop offset="100%" style="stop-color: {m.color}; stop-opacity: 0.02" />
                </linearGradient>
              </defs>

              {#each chart.yTicks as tick}
                <line
                  x1={chart.padLeft}
                  y1={tick.y}
                  x2={chart.viewWidth - 12}
                  y2={tick.y}
                  style="stroke: var(--line); stroke-width: 1"
                />
              {/each}

              {#each chart.segments as seg}
                <path d={seg.area} style="fill: url(#grad-{m.key})" />
              {/each}
              {#each chart.segments as seg}
                <path
                  d={seg.line}
                  style="fill: none; stroke: {m.color}; stroke-width: 2"
                  stroke-linejoin="round"
                  stroke-linecap="round"
                />
              {/each}
              {#each chart.dots as dot}
                <circle cx={dot.x} cy={dot.y} r="2" style="fill: {m.color}" />
              {/each}

              {#each chart.yTicks as tick}
                <text
                  x={chart.padLeft - 5}
                  y={tick.y}
                  text-anchor="end"
                  dominant-baseline="middle"
                  class="axis-label">{tick.label}</text
                >
              {/each}
              {#each chart.xTicks as tick}
                <text
                  x={tick.x}
                  y={chart.viewHeight - 6}
                  text-anchor="middle"
                  class="axis-label">{tick.label}</text
                >
              {/each}

              {#if tooltip?.visible}
                {@const pt = chart.points[tooltip.idx]}
                {@const tipW = 108}
                {@const tipH = 30}
                {@const tipX = Math.min(pt.x + 8, chart.viewWidth - 12 - tipW)}
                {@const tipY = Math.max(chart.padTop + 4, pt.y - tipH - 6)}
                <line
                  x1={pt.x}
                  y1={chart.padTop}
                  x2={pt.x}
                  y2={chart.viewHeight - chart.padBottom}
                  style="stroke: var(--text); stroke-width: 1; stroke-dasharray: 3,3; opacity: 0.4"
                />
                <circle cx={pt.x} cy={pt.y} r="3.5" style="fill: {m.color}" />
                <rect
                  x={tipX}
                  y={tipY}
                  width={tipW}
                  height={tipH}
                  rx="4"
                  style="fill: var(--surface-2); stroke: var(--line)"
                />
                <text x={tipX + 8} y={tipY + 11} class="tip-value"
                  >{m.values[tooltip.idx].toFixed(m.decimals)}{m.unit}</text
                >
                <text x={tipX + 8} y={tipY + 23} class="tip-time"
                  >{formatTime(m.timestamps[tooltip.idx])}</text
                >
              {/if}
            </svg>
          {:else}
            <p class="no-data">グラフ表示に十分なデータがありません</p>
          {/if}
        </div>
      {/if}
    </div>
  {/each}
</section>

<style>
  .panel {
    display: grid;
    gap: 0.9rem;
  }

  .status {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.85rem;
    color: var(--muted);
  }

  .dot {
    width: 0.6rem;
    height: 0.6rem;
    border-radius: 50%;
    background: var(--err);
    flex: none;
  }

  .dot.on {
    background: var(--ok);
  }

  .metric {
    cursor: pointer;
    user-select: none;
    transition: border-color 0.15s ease;
  }

  .metric:hover {
    border-color: var(--muted);
  }

  .metric.expanded {
    border-color: var(--accent);
  }

  .metric-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 0.75rem;
  }

  .metric-header h2 {
    margin: 0;
    font-size: 0.95rem;
    font-weight: 600;
    color: var(--muted);
    letter-spacing: 0.04em;
  }

  .chevron {
    color: var(--muted);
    font-size: 1.1rem;
    line-height: 1;
    display: inline-block;
    transition: transform 0.25s ease;
  }

  .chevron.open {
    transform: rotate(180deg);
  }

  .metric .row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .value {
    font-size: 1.6rem;
    font-weight: 700;
  }

  .unit {
    font-size: 0.9rem;
    font-weight: 500;
    color: var(--muted);
    margin-left: 0.2rem;
  }

  .spark {
    width: 8rem;
    height: 2rem;
    flex: none;
  }

  .detail {
    margin-top: 0.9rem;
    overflow: hidden;
  }

  .range {
    margin-bottom: 0.75rem;
    transition: opacity 0.15s ease;
  }

  /* 読み込み中は薄くして、古い範囲のグラフが残っていることを示す。 */
  .range.busy {
    opacity: 0.55;
  }

  .stats {
    display: flex;
    gap: 1.5rem;
    flex-wrap: wrap;
    font-size: 0.8rem;
    color: var(--muted);
    margin-bottom: 0.6rem;
  }

  .stats strong {
    color: var(--text);
  }

  .detail-chart {
    width: 100%;
    height: auto;
    display: block;
    cursor: crosshair;
  }

  .axis-label {
    font-size: 11px;
    fill: var(--muted);
    font-family:
      system-ui,
      -apple-system,
      sans-serif;
  }

  .tip-value {
    font-size: 12px;
    font-weight: 700;
    fill: var(--text);
    font-family:
      system-ui,
      -apple-system,
      sans-serif;
  }

  .tip-time {
    font-size: 10px;
    fill: var(--muted);
    font-family:
      system-ui,
      -apple-system,
      sans-serif;
  }

  .no-data {
    font-size: 0.85rem;
    color: var(--muted);
    text-align: center;
    margin: 0.5rem 0;
  }
</style>
