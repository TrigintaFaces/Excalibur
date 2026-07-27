<script lang="ts">
  import type { PanelProps } from "../panels";
  import Panel from "../Panel.svelte";

  // Default panel used until a subsystem has a dedicated implementation. Proves
  // capability-driven visibility: it only appears when the host advertises the
  // subsystem.
  let { title }: PanelProps = $props();
</script>

<Panel {title} status="available">
  <p class="hint">Live data renders here once this subsystem's panel ships.</p>
</Panel>

<style>
  .hint {
    margin: 0;
    color: var(--muted, #64748b);
    font-size: 0.85rem;
  }
</style>
