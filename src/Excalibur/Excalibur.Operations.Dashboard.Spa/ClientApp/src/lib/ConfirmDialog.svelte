<script lang="ts">
  // Minimal accessible confirmation modal for opt-in mutating actions.
  interface Props {
    title: string;
    message: string;
    confirmLabel?: string;
    busy?: boolean;
    onconfirm: () => void;
    oncancel: () => void;
  }

  let { title, message, confirmLabel = "Confirm", busy = false, onconfirm, oncancel }: Props = $props();
</script>

<svelte:window
  onkeydown={(e) => {
    if (e.key === "Escape" && !busy) {
      oncancel();
    }
  }}
/>

<div class="backdrop" role="presentation">
  <div
    class="dialog"
    role="alertdialog"
    aria-modal="true"
    aria-labelledby="confirm-title"
    aria-describedby="confirm-body"
  >
    <h2 id="confirm-title">{title}</h2>
    <p id="confirm-body">{message}</p>
    <div class="actions">
      <button type="button" onclick={oncancel} disabled={busy}>Cancel</button>
      <button type="button" class="danger" onclick={onconfirm} disabled={busy}>
        {busy ? "Working…" : confirmLabel}
      </button>
    </div>
  </div>
</div>

<style>
  .backdrop {
    position: fixed;
    inset: 0;
    background: rgba(15, 23, 42, 0.55);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 1rem;
    z-index: 50;
  }
  .dialog {
    background: var(--panel-bg, #ffffff);
    color: inherit;
    border-radius: 0.6rem;
    padding: 1.25rem;
    max-width: 26rem;
    width: 100%;
    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
  }
  h2 {
    margin: 0 0 0.5rem;
    font-size: 1rem;
  }
  p {
    margin: 0 0 1.1rem;
    font-size: 0.85rem;
    color: var(--muted, #475569);
  }
  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.6rem;
  }
  button {
    font: inherit;
    font-size: 0.82rem;
    padding: 0.35rem 0.9rem;
    border: 1px solid var(--panel-border, #cbd5e1);
    border-radius: 0.4rem;
    background: transparent;
    color: inherit;
    cursor: pointer;
  }
  button.danger {
    background: #dc2626;
    border-color: #dc2626;
    color: #ffffff;
  }
  button:disabled {
    opacity: 0.6;
    cursor: default;
  }
  @media (prefers-color-scheme: dark) {
    .dialog {
      --panel-bg: #111a2e;
    }
  }
</style>
