<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { getCapabilities, type DashboardCapabilities } from "./lib/api";
  import { Poller } from "./lib/poll.svelte";
  import { resolvePanel } from "./lib/panels";

  // Capability discovery drives which panels render (fail-open: an absent or
  // unreachable subsystem simply yields no panel, never an error page). Panels
  // themselves poll their own subsystem data; capabilities change rarely, so
  // they refresh on a slower cadence.
  const CAPABILITY_INTERVAL_MS = 15000;

  const poller = new Poller<DashboardCapabilities>(getCapabilities, CAPABILITY_INTERVAL_MS);

  const subsystems = $derived(poller.data?.subsystems ?? []);
  const mutatingActionsEnabled = $derived(poller.data?.mutatingActionsEnabled ?? false);
  const panels = $derived(subsystems.map((s) => ({ subsystem: s, ...resolvePanel(s) })));

  function lastUpdatedLabel(at: Date | undefined): string {
    return at ? `updated ${at.toLocaleTimeString()}` : "connecting…";
  }

  onMount(() => poller.start());
  onDestroy(() => poller.stop());
</script>

<div class="shell">
  <header class="topbar">
    <div class="brand">
      <h1>Excalibur Operations</h1>
      <span class="badge">read-only</span>
    </div>
    <nav aria-label="Subsystems">
      {#each panels as panel (panel.subsystem)}
        <a href={`#panel-${panel.subsystem}`}>{panel.title}</a>
      {/each}
    </nav>
    <div class="refresh">
      <span class="status" class:error={poller.error !== undefined}>
        {poller.error ? "disconnected" : lastUpdatedLabel(poller.lastUpdated)}
      </span>
      <button type="button" onclick={() => poller.refresh()} disabled={poller.loading}>
        Refresh
      </button>
    </div>
  </header>

  <main>
    {#if poller.error && poller.data === undefined}
      <div class="notice error" role="alert">
        Cannot reach the dashboard API. Retrying automatically…
      </div>
    {:else if poller.data === undefined}
      <div class="notice" aria-busy="true">Loading dashboard…</div>
    {:else if panels.length === 0}
      <div class="notice">No dashboard subsystems are configured in this host.</div>
    {:else}
      <div class="grid">
        {#each panels as panel (panel.subsystem)}
          {@const Panel = panel.component}
          <div id={`panel-${panel.subsystem}`}>
            <Panel subsystem={panel.subsystem} title={panel.title} {mutatingActionsEnabled} />
          </div>
        {/each}
      </div>
    {/if}
  </main>
</div>

<style>
  .shell {
    max-width: 72rem;
    margin: 0 auto;
    padding: 1rem 1.25rem 3rem;
    font-family: system-ui, sans-serif;
  }
  .topbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem 1.25rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid var(--panel-border, #e2e8f0);
  }
  .brand {
    display: flex;
    align-items: center;
    gap: 0.6rem;
  }
  h1 {
    font-size: 1.15rem;
    margin: 0;
  }
  .badge {
    font-size: 0.68rem;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    padding: 0.12rem 0.45rem;
    border-radius: 999px;
    background: #e2e8f0;
    color: #334155;
  }
  nav {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
    flex: 1;
  }
  nav a {
    font-size: 0.85rem;
    color: #2563eb;
    text-decoration: none;
  }
  nav a:hover {
    text-decoration: underline;
  }
  .refresh {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    margin-left: auto;
  }
  .status {
    font-size: 0.75rem;
    color: var(--muted, #94a3b8);
  }
  .status.error {
    color: #dc2626;
  }
  button {
    font: inherit;
    font-size: 0.8rem;
    padding: 0.25rem 0.7rem;
    border: 1px solid var(--panel-border, #cbd5e1);
    border-radius: 0.4rem;
    background: transparent;
    color: inherit;
    cursor: pointer;
  }
  button:disabled {
    opacity: 0.5;
    cursor: default;
  }
  main {
    margin-top: 1.25rem;
  }
  .grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(20rem, 1fr));
    gap: 1rem;
  }
  .notice {
    padding: 1.25rem;
    border-radius: 0.6rem;
    background: var(--panel-bg, #f1f5f9);
    color: var(--muted, #475569);
    font-size: 0.9rem;
  }
  .notice.error {
    background: #fef2f2;
    color: #b91c1c;
  }
  @media (prefers-color-scheme: dark) {
    .badge {
      background: #1e293b;
      color: #cbd5e1;
    }
    .notice {
      --panel-bg: #111a2e;
      --muted: #94a3b8;
    }
    .notice.error {
      background: #2a1416;
      color: #fca5a5;
    }
  }
</style>
