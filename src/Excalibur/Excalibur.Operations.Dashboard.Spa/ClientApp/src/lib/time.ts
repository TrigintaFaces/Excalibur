/**
 * The dashboard timestamp contract.
 *
 * THE SERVER MUST EMIT AN OFFSET. Every timestamp the API returns is a `DateTimeOffset` on the
 * server and must reach us as an ISO-8601 string carrying `Z` or `±hh:mm`.
 *
 * This is not a style preference, it is the difference between a correct instant and a silently
 * wrong one. ECMAScript parses a date-time string WITHOUT an offset as LOCAL time and the same
 * string WITH one as the instant it names:
 *
 *     new Date("2026-07-25T14:00:00")       -> 14:00 in the VIEWER's zone
 *     new Date("2026-07-25T14:00:00Z")      -> 14:00 UTC
 *
 * So a naive string does not fail. It renders an operator an overdue-saga time shifted by their
 * own UTC offset, with nothing anywhere reporting a problem. That is why this module refuses the
 * naive form instead of parsing it: a visible "invalid" beats a plausible wrong time.
 */

/** Matches an ISO-8601 date-time that carries an explicit UTC offset (`Z` or `±hh:mm`). */
const OffsetBearing = /^\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}(:\d{2}(\.\d+)?)?([Zz]|[+-]\d{2}:\d{2})$/;

/** True when `value` carries an explicit offset, so ECMAScript will read it as an instant. */
export function hasExplicitOffset(value: string): boolean {
  return OffsetBearing.test(value.trim());
}

/**
 * Parses a server-supplied timestamp, or returns null if the contract was not met.
 *
 * Returns null rather than a Date for a naive string, an unparseable one, or a null/blank field —
 * the caller renders the placeholder, which is visible, instead of a wrong time, which is not.
 */
export function parseServerInstant(value: string | null | undefined): Date | null {
  if (value === null || value === undefined) return null;

  const trimmed = value.trim();
  if (trimmed === "" || !hasExplicitOffset(trimmed)) return null;

  const parsed = new Date(trimmed);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

/**
 * Formats a server-supplied timestamp for display in the viewer's locale and zone, or returns
 * `placeholder` when the contract was not met. Converting a known instant into the viewer's zone
 * is correct and wanted; GUESSING that a naive string is in their zone is the defect.
 */
export function formatServerInstant(value: string | null | undefined, placeholder = "—"): string {
  const parsed = parseServerInstant(value);
  return parsed === null ? placeholder : parsed.toLocaleString();
}
