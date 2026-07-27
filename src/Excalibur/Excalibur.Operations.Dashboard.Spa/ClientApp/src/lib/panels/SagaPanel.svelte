<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { ApiError, getJson } from "../api";
  import Metrics from "../Metrics.svelte";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Saga summary (GET {api}/saga -> SagaView, camelCase).
  interface SagaView {
    configured: boolean;
    running: number;
    completed: number;
    total: number;
    capturedAt: string;
  }

  // Stuck sagas (GET {api}/saga/stuck -> StuckSagaView). Capability-gated on
  // ISagaTimeoutStore; absent -> "stuck unavailable", never breaks the panel.
  interface StuckSagaEntry {
    sagaId: string;
    sagaType: string;
    dueAt: string;
  }
  interface StuckSagaView {
    available: boolean;
    stuck: StuckSagaEntry[];
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<SagaView>((s) => getJson<SagaView>("saga", s), REFRESH_MS);
  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));

  // Stuck sagas polled on a slower cadence; unavailable is a normal state.
  let stuck = $state<StuckSagaEntry[] | undefined>(undefined);
  let stuckAvailable = $state<boolean | undefined>(undefined);

  const stuckPoller = new Poller<StuckSagaView>(async (s) => {
    try {
      return await getJson<StuckSagaView>("saga/stuck", s);
    } catch (cause) {
      // 404 / not-mapped -> the timeout-store capability isn't present.
      if (cause instanceof ApiError && (cause.status === 404 || cause.status === 0)) {
        return { available: false, stuck: [] };
      }
      throw cause;
    }
  }, REFRESH_MS * 2);

  $effect(() => {
    stuck = stuckPoller.data?.stuck;
    stuckAvailable = stuckPoller.data?.available;
  });

  onMount(() => {
    poller.start();
    stuckPoller.start();
  });
  onDestroy(() => {
    poller.stop();
    stuckPoller.stop();
  });
</script>

<Panel {title} {status}>
  {#if data?.configured}
    <Metrics
      metrics={[
        { label: "Running", value: data.running },
        { label: "Completed", value: data.completed },
        { label: "Total", value: data.total },
        { label: "Stuck", value: stuck?.length ?? (stuckAvailable === false ? "—" : "…"), alert: (stuck?.length ?? 0) > 0 },
      ]}
    />
    {#if stuck && stuck.length > 0}
      <div class="stuck" role="alert">
        <h3>Stuck (timeout due)</h3>
        <ul>
          {#each stuck as s (s.sagaId)}
            <li>
              <span class="type">{s.sagaType}</span>
              <span class="due">due {new Date(s.dueAt).toLocaleString()}</span>
            </li>
          {/each}
        </ul>
      </div>
    {:else if stuckAvailable === false}
      <p class="muted">Stuck detection unavailable (no timeout store configured).</p>
    {/if}
  {:else if data && !data.configured}
    <p class="muted">Saga store is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load saga state.</p>
  {:else}
    <p class="muted">Loading…</p>
  {/if}
</Panel>

<style>
  .muted {
    margin: 0.5rem 0 0;
    color: var(--muted, #64748b);
    font-size: 0.85rem;
  }
  .stuck {
    margin-top: 0.85rem;
    border: 1px solid #fca5a5;
    background: #fef2f2;
    border-radius: 0.4rem;
    padding: 0.6rem 0.75rem;
  }
  .stuck h3 {
    margin: 0 0 0.4rem;
    font-size: 0.72rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: #b91c1c;
  }
  .stuck ul {
    margin: 0;
    padding: 0;
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }
  .stuck li {
    display: flex;
    justify-content: space-between;
    gap: 0.75rem;
    font-size: 0.8rem;
    color: #7f1d1d;
  }
  .due {
    color: #b91c1c;
    font-variant-numeric: tabular-nums;
  }
  @media (prefers-color-scheme: dark) {
    .stuck {
      background: #2a1416;
      border-color: #7f1d1d;
    }
    .stuck li {
      color: #fca5a5;
    }
    .due {
      color: #fca5a5;
    }
  }
</style>
