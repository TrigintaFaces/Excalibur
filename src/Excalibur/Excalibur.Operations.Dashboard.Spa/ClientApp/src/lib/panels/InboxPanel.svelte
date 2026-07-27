<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getJson } from "../api";
  import Metrics from "../Metrics.svelte";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Inbox read model (GET {api}/inbox -> InboxView, camelCase).
  interface InboxView {
    configured: boolean;
    total: number;
    processed: number;
    failed: number;
    pending: number;
    capturedAt: string;
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<InboxView>((signal) => getJson<InboxView>("inbox", signal), REFRESH_MS);
  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data?.configured}
    <Metrics
      metrics={[
        { label: "Total", value: data.total },
        { label: "Processed", value: data.processed },
        { label: "Pending", value: data.pending },
        { label: "Failed", value: data.failed, alert: data.failed > 0 },
      ]}
    />
  {:else if data && !data.configured}
    <p class="muted">Inbox is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load inbox state.</p>
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
