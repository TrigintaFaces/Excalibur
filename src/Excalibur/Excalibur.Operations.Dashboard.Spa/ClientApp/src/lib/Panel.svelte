<script lang="ts">
  import type { Snippet } from "svelte";

  // Generic card wrapper shared by every subsystem panel: a titled, bordered
  // region with an optional status line and a body slot.
  interface Props {
    title: string;
    status?: string;
    children: Snippet;
  }

  let { title, status, children }: Props = $props();
</script>

<section class="panel">
  <header>
    <h2>{title}</h2>
    {#if status}<span class="status">{status}</span>{/if}
  </header>
  <div class="body">
    {@render children()}
  </div>
</section>

<style>
  .panel {
    border: 1px solid var(--panel-border, #e2e8f0);
    border-radius: 0.6rem;
    background: var(--panel-bg, #ffffff);
    overflow: hidden;
  }
  header {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 0.5rem;
    padding: 0.7rem 1rem;
    border-bottom: 1px solid var(--panel-border, #e2e8f0);
  }
  h2 {
    font-size: 0.95rem;
    margin: 0;
  }
  .status {
    font-size: 0.72rem;
    color: var(--muted, #94a3b8);
  }
  .body {
    padding: 1rem;
  }
  @media (prefers-color-scheme: dark) {
    .panel {
      --panel-border: #1e293b;
      --panel-bg: #111a2e;
    }
  }
</style>
