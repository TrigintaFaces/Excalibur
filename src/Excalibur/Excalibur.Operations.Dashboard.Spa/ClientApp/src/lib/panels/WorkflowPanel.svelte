<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getJson } from "../api";
  import Metrics from "../Metrics.svelte";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Durable-workflow read model (GET {api}/workflows -> WorkflowView, camelCase).
  // The workflow admin store is an optional extension point (IWorkflowStoreAdmin);
  // when none is registered the endpoint fails open with configured:false, so an
  // empty panel reads as "listing unavailable", not "zero workflows".
  interface WorkflowView {
    configured: boolean;
    running: number;
    completed: number;
    faulted: number;
    total: number;
    capturedAt: string;
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<WorkflowView>((signal) => getJson<WorkflowView>("workflows", signal), REFRESH_MS);
  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data?.configured}
    <Metrics
      metrics={[
        { label: "Running", value: data.running },
        { label: "Completed", value: data.completed },
        { label: "Faulted", value: data.faulted, alert: data.faulted > 0 },
        { label: "Total", value: data.total },
      ]}
    />
  {:else if data && !data.configured}
    <p class="muted">No workflow admin store is registered in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load workflow state.</p>
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
</style>
