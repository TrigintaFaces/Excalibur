<script lang="ts">
  // A compact label/value metric grid shared by the subsystem panels, so every
  // panel presents its counters consistently.
  interface Metric {
    label: string;
    value: string | number;
    /** Optional emphasis for values that signal a problem (e.g. failed > 0). */
    alert?: boolean;
  }

  let { metrics }: { metrics: Metric[] } = $props();
</script>

<dl class="metrics">
  {#each metrics as metric (metric.label)}
    <div class="metric">
      <dt>{metric.label}</dt>
      <dd class:alert={metric.alert}>{metric.value}</dd>
    </div>
  {/each}
</dl>

<style>
  .metrics {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(6rem, 1fr));
    gap: 0.75rem;
    margin: 0;
  }
  .metric {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }
  dt {
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--muted, #94a3b8);
  }
  dd {
    margin: 0;
    font-size: 1.35rem;
    font-variant-numeric: tabular-nums;
    font-weight: 600;
  }
  dd.alert {
    color: #dc2626;
  }
</style>
