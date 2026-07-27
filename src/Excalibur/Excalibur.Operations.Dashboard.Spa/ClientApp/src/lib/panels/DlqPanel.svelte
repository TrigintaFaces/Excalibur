<script lang="ts">
  import { onDestroy, onMount } from "svelte";

  import { ApiError, getJson, postJson } from "../api";
  import ConfirmDialog from "../ConfirmDialog.svelte";
  import Panel from "../Panel.svelte";
  import type { PanelProps } from "../panels";
  import { Poller } from "../poll.svelte";
  import { panelStatus } from "../status";

  // Dead-letter read models (camelCase, STJ source-gen).
  interface DeadLetterView {
    configured: boolean;
    count: number;
    capturedAt: string;
  }
  interface DeadLetterEntryView {
    id: string;
    messageType: string;
    reason: string;
    exceptionMessage: string | null;
    enqueuedAt: string;
    originalAttempts: number;
    correlationId: string | null;
    sourceQueue: string | null;
    isReplayed: boolean;
    replayedAt: string | null;
  }
  interface DeadLetterReplayResult {
    replayed: boolean;
    count: number;
  }
  type ReplayAction =
    | { kind: "single"; entry: DeadLetterEntryView }
    | { kind: "batch"; messageType: string };

  const SUMMARY_REFRESH_MS = 5000;
  const PAGE_SIZE = 20;

  let { title, mutatingActionsEnabled }: PanelProps = $props();

  // Summary count polls; the entry list is paged on demand (polling it would
  // fight the operator's paging/inspection).
  const summary = new Poller<DeadLetterView>((s) => getJson<DeadLetterView>("dlq", s), SUMMARY_REFRESH_MS);
  const summaryData = $derived(summary.data);
  const status = $derived(panelStatus(summary.error !== undefined, summaryData?.configured, summary.lastUpdated));

  let skip = $state(0);
  let entries = $state<DeadLetterEntryView[]>([]);
  let entriesError = $state<string | undefined>(undefined);
  let loadingEntries = $state(false);
  let filter = $state("");
  let selected = $state<DeadLetterEntryView | undefined>(undefined);

  const visibleEntries = $derived(
    filter.trim().length === 0
      ? entries
      : entries.filter((e) => {
          const needle = filter.trim().toLowerCase();
          return (
            e.messageType.toLowerCase().includes(needle) ||
            e.reason.toLowerCase().includes(needle) ||
            (e.correlationId?.toLowerCase().includes(needle) ?? false)
          );
        }),
  );

  async function loadEntries(): Promise<void> {
    loadingEntries = true;
    entriesError = undefined;
    try {
      entries = await getJson<DeadLetterEntryView[]>(`dlq/entries?skip=${skip}&limit=${PAGE_SIZE}`);
    } catch (cause) {
      entries = [];
      entriesError = cause instanceof ApiError ? cause.message : "Failed to load entries.";
    } finally {
      loadingEntries = false;
    }
  }

  function nextPage(): void {
    skip += PAGE_SIZE;
    selected = undefined;
    void loadEntries();
  }
  function prevPage(): void {
    skip = Math.max(0, skip - PAGE_SIZE);
    selected = undefined;
    void loadEntries();
  }

  // --- Opt-in replay (W3): only reachable when the host advertises mutating
  // actions; the endpoints are auth-gated + only mapped server-side when enabled.
  let pending = $state<ReplayAction | undefined>(undefined);
  let replaying = $state(false);
  let replayNote = $state<string | undefined>(undefined);

  const confirmMessage = $derived(
    pending === undefined
      ? ""
      : pending.kind === "single"
        ? `Replay this dead-lettered "${pending.entry.messageType}" message? It will be re-dispatched for processing.`
        : `Replay ALL un-replayed "${pending.messageType}" messages? Each will be re-dispatched.`,
  );

  async function runReplay(): Promise<void> {
    if (pending === undefined) {
      return;
    }
    replaying = true;
    replayNote = undefined;
    try {
      const result =
        pending.kind === "single"
          ? await postJson<DeadLetterReplayResult>(`dlq/${pending.entry.id}/replay`)
          : await postJson<DeadLetterReplayResult>("dlq/replay-batch", { messageType: pending.messageType });
      replayNote = `Replayed ${result.count} message${result.count === 1 ? "" : "s"}.`;
      pending = undefined;
      selected = undefined;
      await loadEntries();
      await summary.refresh();
    } catch (cause) {
      replayNote =
        cause instanceof ApiError
          ? `Replay failed (${cause.status || "network"}).`
          : "Replay failed.";
      pending = undefined;
    } finally {
      replaying = false;
    }
  }

  onMount(() => {
    summary.start();
    void loadEntries();
  });
  onDestroy(() => summary.stop());
</script>

<Panel {title} {status}>
  {#if summaryData && !summaryData.configured}
    <p class="muted">Dead-letter queue is not configured in this host.</p>
  {:else}
    <div class="head">
      <span class="count">{summaryData?.count ?? "—"} <small>dead-lettered</small></span>
      <input
        type="search"
        placeholder="Filter type / reason / correlation…"
        bind:value={filter}
        aria-label="Filter dead-letter entries"
      />
    </div>

    {#if entriesError}
      <p class="muted">{entriesError}</p>
    {:else if loadingEntries && entries.length === 0}
      <p class="muted">Loading entries…</p>
    {:else if visibleEntries.length === 0}
      <p class="muted">{entries.length === 0 ? "No dead-letter entries." : "No entries match the filter."}</p>
    {:else}
      <div class="table" role="table">
        {#each visibleEntries as entry (entry.id)}
          <button
            type="button"
            class="row"
            class:selected={selected?.id === entry.id}
            onclick={() => (selected = selected?.id === entry.id ? undefined : entry)}
          >
            <span class="type">{entry.messageType}</span>
            <span class="reason">{entry.reason}</span>
            <span class="attempts">{entry.originalAttempts}×</span>
          </button>
        {/each}
      </div>
    {/if}

    {#if selected}
      <dl class="detail">
        <div><dt>Id</dt><dd>{selected.id}</dd></div>
        <div><dt>Enqueued</dt><dd>{new Date(selected.enqueuedAt).toLocaleString()}</dd></div>
        <div><dt>Source</dt><dd>{selected.sourceQueue ?? "—"}</dd></div>
        <div><dt>Correlation</dt><dd>{selected.correlationId ?? "—"}</dd></div>
        <div><dt>Replayed</dt><dd>{selected.isReplayed ? `yes (${selected.replayedAt ? new Date(selected.replayedAt).toLocaleString() : "—"})` : "no"}</dd></div>
        {#if selected.exceptionMessage}
          <div class="full"><dt>Exception</dt><dd class="exception">{selected.exceptionMessage}</dd></div>
        {/if}
      </dl>

      {#if mutatingActionsEnabled && !selected.isReplayed}
        <div class="replay-actions">
          <button type="button" onclick={() => (pending = { kind: "single", entry: selected! })}>
            Replay entry
          </button>
          <button
            type="button"
            onclick={() => (pending = { kind: "batch", messageType: selected!.messageType })}
          >
            Replay all "{selected.messageType}"
          </button>
        </div>
      {/if}
    {/if}

    {#if replayNote}
      <p class="replay-note" role="status">{replayNote}</p>
    {/if}

    <div class="pager">
      <button type="button" onclick={prevPage} disabled={skip === 0 || loadingEntries}>Prev</button>
      <span class="range">{entries.length === 0 ? "0" : `${skip + 1}–${skip + entries.length}`}</span>
      <button type="button" onclick={nextPage} disabled={entries.length < PAGE_SIZE || loadingEntries}>Next</button>
    </div>
  {/if}
</Panel>

{#if pending}
  <ConfirmDialog
    title="Confirm replay"
    message={confirmMessage}
    confirmLabel="Replay"
    busy={replaying}
    onconfirm={runReplay}
    oncancel={() => (pending = undefined)}
  />
{/if}

<style>
  .muted {
    margin: 0;
    color: var(--muted, #64748b);
    font-size: 0.85rem;
  }
  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    margin-bottom: 0.75rem;
  }
  .count {
    font-size: 1.35rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  }
  .count small {
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--muted, #94a3b8);
    font-weight: 400;
  }
  input[type="search"] {
    font: inherit;
    font-size: 0.8rem;
    padding: 0.3rem 0.5rem;
    border: 1px solid var(--panel-border, #cbd5e1);
    border-radius: 0.4rem;
    background: transparent;
    color: inherit;
    min-width: 0;
    flex: 1;
    max-width: 16rem;
  }
  .table {
    display: flex;
    flex-direction: column;
    border: 1px solid var(--panel-border, #e2e8f0);
    border-radius: 0.4rem;
    overflow: hidden;
  }
  .row {
    display: grid;
    grid-template-columns: 1fr 1fr auto;
    gap: 0.5rem;
    align-items: center;
    padding: 0.45rem 0.6rem;
    font: inherit;
    font-size: 0.8rem;
    text-align: left;
    background: transparent;
    color: inherit;
    border: none;
    border-bottom: 1px solid var(--panel-border, #f1f5f9);
    cursor: pointer;
  }
  .row:last-child {
    border-bottom: none;
  }
  .row:hover,
  .row.selected {
    background: var(--row-hover, #f1f5f9);
  }
  .type {
    font-weight: 600;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .reason {
    color: var(--muted, #64748b);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .attempts {
    font-variant-numeric: tabular-nums;
    color: var(--muted, #94a3b8);
  }
  .detail {
    margin: 0.75rem 0 0;
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
    gap: 0.5rem 1rem;
    font-size: 0.8rem;
    padding: 0.6rem;
    border: 1px solid var(--panel-border, #e2e8f0);
    border-radius: 0.4rem;
  }
  .detail .full {
    grid-column: 1 / -1;
  }
  .detail dt {
    font-size: 0.68rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--muted, #94a3b8);
  }
  .detail dd {
    margin: 0;
    word-break: break-word;
  }
  .exception {
    font-family: ui-monospace, monospace;
    font-size: 0.72rem;
    white-space: pre-wrap;
  }
  .replay-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-top: 0.6rem;
  }
  .replay-note {
    margin: 0.6rem 0 0;
    font-size: 0.8rem;
    color: var(--muted, #475569);
  }
  .pager {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    margin-top: 0.75rem;
  }
  .range {
    font-size: 0.75rem;
    color: var(--muted, #94a3b8);
    font-variant-numeric: tabular-nums;
  }
  button:not(.row) {
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
  @media (prefers-color-scheme: dark) {
    .row:hover,
    .row.selected {
      --row-hover: #1e293b;
    }
  }
</style>
