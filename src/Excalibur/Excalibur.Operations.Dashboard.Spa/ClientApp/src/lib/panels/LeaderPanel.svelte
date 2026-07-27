<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getJson } from "../api";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Leader election snapshot (GET {api}/leader -> LeaderView, camelCase).
  interface LeaderView {
    configured: boolean;
    isLeader: boolean;
    candidateId: string | null;
    currentLeaderId: string | null;
    fencingToken: number | null;
    resource: string | null;
    capturedAt: string;
  }

  const REFRESH_MS = 5000;

  let { title }: PanelProps = $props();

  const poller = new Poller<LeaderView>((s) => getJson<LeaderView>("leader", s), REFRESH_MS);
  const data = $derived(poller.data);
  const status = $derived(panelStatus(poller.error !== undefined, data?.configured, poller.lastUpdated));

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<Panel {title} {status}>
  {#if data?.configured}
    <div class="lead">
      <span class="badge" class:is-leader={data.isLeader}>
        {data.isLeader ? "this instance is leader" : "follower"}
      </span>
    </div>
    <dl class="detail">
      <div><dt>Current leader</dt><dd>{data.currentLeaderId ?? "—"}</dd></div>
      <div><dt>This candidate</dt><dd>{data.candidateId ?? "—"}</dd></div>
      <div><dt>Fencing token</dt><dd>{data.fencingToken ?? "—"}</dd></div>
      <div><dt>Resource</dt><dd>{data.resource ?? "—"}</dd></div>
    </dl>
  {:else if data && !data.configured}
    <p class="muted">Leader election is not configured in this host.</p>
  {:else if poller.error}
    <p class="muted">Unable to load leader state.</p>
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
  .lead {
    margin-bottom: 0.75rem;
  }
  .badge {
    font-size: 0.72rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    padding: 0.18rem 0.55rem;
    border-radius: 999px;
    background: #e2e8f0;
    color: #475569;
  }
  .badge.is-leader {
    background: #dcfce7;
    color: #166534;
  }
  .detail {
    margin: 0;
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    gap: 0.5rem 1rem;
    font-size: 0.82rem;
  }
  dt {
    font-size: 0.68rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--muted, #94a3b8);
  }
  dd {
    margin: 0;
    word-break: break-word;
    font-variant-numeric: tabular-nums;
  }
  @media (prefers-color-scheme: dark) {
    .badge {
      background: #1e293b;
      color: #cbd5e1;
    }
    .badge.is-leader {
      background: #14331f;
      color: #86efac;
    }
  }
</style>
