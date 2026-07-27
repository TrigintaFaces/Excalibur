<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getJson } from "../api";
  import Metrics from "../Metrics.svelte";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Outbox read model (GET {api}/outbox -> OutboxView, camelCase).
  interface OutboxView {
    configured: boolean;
    staged: number;
    sending: number;
    sent: number;
    failed: number;
    scheduled: number;
    capturedAt: string;
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<OutboxView>((signal) => getJson<OutboxView>("outbox", signal), REFRESH_MS);
  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data?.configured}
    <Metrics
      metrics={[
        { label: "Staged", value: data.staged },
        { label: "Sending", value: data.sending },
        { label: "Sent", value: data.sent },
        { label: "Failed", value: data.failed, alert: data.failed > 0 },
        { label: "Scheduled", value: data.scheduled },
      ]}
    />
  {:else if data && !data.configured}
    <p class="muted">Outbox is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load outbox state.</p>
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
