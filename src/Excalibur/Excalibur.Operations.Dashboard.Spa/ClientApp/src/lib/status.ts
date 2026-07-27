// Shared status-line text for subsystem panels, so every panel reports its
// connection/configuration state consistently.
export function panelStatus(
  error: boolean,
  configured: boolean | undefined,
  lastUpdated: Date | undefined,
): string {
  if (error) {
    return "disconnected";
  }
  if (configured === undefined) {
    return "loading…";
  }
  if (!configured) {
    return "not configured";
  }
  return lastUpdated ? `updated ${lastUpdated.toLocaleTimeString()}` : "updated";
}
