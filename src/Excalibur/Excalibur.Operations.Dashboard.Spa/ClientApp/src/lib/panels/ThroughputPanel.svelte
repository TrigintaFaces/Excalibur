<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getJson } from "../api";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Per-subsystem throughput (GET {api}/{subsystem}/throughput -> ThroughputView, camelCase),
  // derived read-only from the existing OpenTelemetry counters via ThroughputCollector. The
  // collector tracks a fixed set of message subsystems (outbox/inbox/saga); this panel polls each
  // and renders its events-per-second rate. A subsystem whose meters are not bridged reports
  // configured:false and is shown as unavailable rather than a spurious zero rate.
  interface ThroughputView {
    subsystem: string;
    configured: boolean;
    ratePerSecond: number;
    windowSeconds: number;
    capturedAt: string;
  }

  // The subsystems ThroughputCollector bridges (Excalibur.Outbox.Store/Inbox/Saga meters).
  const SUBSYSTEMS = ["outbox", "inbox", "saga"] as const;
  const LABELS: Record<string, string> = { outbox: "Outbox", inbox: "Inbox", saga: "Sagas" };
  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<ThroughputView[]>(
    (signal) => Promise.all(SUBSYSTEMS.map((s) => getJson<ThroughputView>(`${s}/throughput`, signal))),
    REFRESH_MS,
  );
  const data = $derived(poller.data);
  // The panel is "configured" when the throughput collector is bridged for at least one subsystem.
  const anyConfigured = $derived(data ? data.some((r) => r.configured) : undefined);
  const status = $derived(panelStatus(poller.error !== undefined, anyConfigured, poller.lastUpdated));

  function rate(reading: ThroughputView): string {
    return reading.configured ? `${reading.ratePerSecond.toFixed(1)}/s` : "—";
  }

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data && anyConfigured}
    <div class="table">
      {#each data as reading (reading.subsystem)}
        <div class="row">
          <span class="name">{LABELS[reading.subsystem] ?? reading.subsystem}</span>
          <span class="num" class:muted={!reading.configured}>{rate(reading)}</span>
        </div>
      {/each}
    </div>
  {:else if data && !anyConfigured}
    <p class="muted">Throughput tracking is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load throughput.</p>
  {:else}
    <p class="muted">Loading…</p>
  {/if}
</Panel>

<style>
  .muted {
    margin: 0;
    color: var(--muted, #64748b);
    font-size: 0.85rem;
  }
  .table {
    display: flex;
    flex-direction: column;
    font-size: 0.85rem;
  }
  .row {
    display: grid;
    grid-template-columns: 1fr auto;
    gap: 0.75rem;
    padding: 0.35rem 0.2rem;
    border-bottom: 1px solid var(--panel-border, #f1f5f9);
  }
  .row:last-child {
    border-bottom: none;
  }
  .name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .num {
    text-align: right;
    font-variant-numeric: tabular-nums;
    font-weight: 600;
    min-width: 4rem;
  }
</style>
