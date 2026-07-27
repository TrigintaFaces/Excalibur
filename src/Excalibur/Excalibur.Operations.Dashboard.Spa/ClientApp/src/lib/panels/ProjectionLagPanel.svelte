<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { ApiError, getJson } from "../api";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Projection/CDC lag (GET {api}/projections/lag -> ProjectionLagView, camelCase).
  // DTO frozen by the projection-lag endpoint lane.
  interface ProjectionLagEntry {
    subscriptionName: string;
    checkpointPosition: number;
    headPosition: number;
    lag: number;
  }
  interface ProjectionLagView {
    configured: boolean;
    streams: ProjectionLagEntry[];
    capturedAt: string;
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  // The endpoint may not be mapped yet (separate package); treat 404 as
  // not-configured so the panel fails open rather than erroring.
  const poller = new Poller<ProjectionLagView>(async (s) => {
    try {
      return await getJson<ProjectionLagView>("projections/lag", s);
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 404) {
        return { configured: false, streams: [], capturedAt: "" };
      }
      throw cause;
    }
  }, REFRESH_MS);

  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));
  const maxLag = $derived(data ? data.streams.reduce((m, s) => Math.max(m, s.lag), 0) : 0);

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data?.configured}
    {#if data.streams.length === 0}
      <p class="muted">No projection subscriptions with checkpoints.</p>
    {:else}
      <div class="table">
        <div class="row head">
          <span>Subscription</span>
          <span class="num">Checkpoint</span>
          <span class="num">Head</span>
          <span class="num">Lag</span>
        </div>
        {#each data.streams as stream (stream.subscriptionName)}
          <div class="row">
            <span class="name">{stream.subscriptionName}</span>
            <span class="num">{stream.checkpointPosition}</span>
            <span class="num">{stream.headPosition}</span>
            <span class="num lag" class:alert={stream.lag > 0} class:worst={stream.lag === maxLag && maxLag > 0}>
              {stream.lag}
            </span>
          </div>
        {/each}
      </div>
    {/if}
  {:else if data && !data.configured}
    <p class="muted">Projection-lag tracking is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load projection lag.</p>
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
    font-size: 0.8rem;
  }
  .row {
    display: grid;
    grid-template-columns: 1fr auto auto auto;
    gap: 0.75rem;
    padding: 0.35rem 0.2rem;
    border-bottom: 1px solid var(--panel-border, #f1f5f9);
  }
  .row:last-child {
    border-bottom: none;
  }
  .row.head {
    font-size: 0.68rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--muted, #94a3b8);
  }
  .name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .num {
    text-align: right;
    font-variant-numeric: tabular-nums;
    min-width: 3.5rem;
  }
  .lag.alert {
    font-weight: 600;
  }
  .lag.worst {
    color: #dc2626;
  }
</style>
