// Typed client for the dashboard read API.
//
// The SPA is served under a consumer-configurable prefix (default /dashboard)
// and the read API lives at "{prefix}/api". The prefix is not known at build
// time, so it is derived at runtime from this bundle's own URL: the entry
// script is served from "{prefix}/assets/<name>.js", so stripping the trailing
// "/assets/<file>" yields "{prefix}", and the API base is "{prefix}/api/".

function resolveApiBase(): string {
  const script = new URL(import.meta.url);
  const prefix = script.pathname.replace(/\/assets\/[^/]*$/, "");
  // A served bundle has an http(s) origin; resolve the API base against it. When the origin is
  // opaque/null (e.g. a file:// bundle opened directly, a sandboxed iframe, or a test module URL),
  // `new URL(path, "null")` throws — fall back to a document-relative "{prefix}/api/" so the app
  // still resolves requests against its own host rather than white-screening at load.
  if (script.origin && script.origin !== "null") {
    return new URL(`${prefix}/api/`, script.origin).toString();
  }
  // Opaque/null module origin: resolve document-relative against the current page instead.
  if (typeof location !== "undefined" && location.href) {
    return new URL(`${prefix}/api/`, location.href).toString();
  }
  return `${prefix}/api/`;
}

/** Absolute base URL of the dashboard read API (with a trailing slash). */
export const apiBase = resolveApiBase();

/** Capability-discovery payload: which subsystems are available and whether mutating actions are on. */
export interface DashboardCapabilities {
  subsystems: string[];
  mutatingActionsEnabled: boolean;
}

/** Raised when a dashboard API request fails or returns a non-success status. */
export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

/**
 * GETs a JSON document from a path relative to {@link apiBase}.
 * @param path API sub-path (e.g. "" for capability discovery, "outbox" for the outbox panel).
 * @param signal Optional abort signal so pollers can cancel in-flight requests.
 */
export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const url = new URL(path, apiBase).toString();
  let response: Response;
  try {
    response = await fetch(url, {
      signal,
      headers: { Accept: "application/json" },
      credentials: "same-origin",
    });
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === "AbortError") {
      throw cause;
    }
    throw new ApiError(`Request to ${path || "/"} failed`, 0);
  }

  if (!response.ok) {
    throw new ApiError(`Request to ${path || "/"} returned ${response.status}`, response.status);
  }

  return (await response.json()) as T;
}

/** Fetches the dashboard capabilities used to drive panel visibility. */
export function getCapabilities(signal?: AbortSignal): Promise<DashboardCapabilities> {
  return getJson<DashboardCapabilities>("", signal);
}

/**
 * POSTs a JSON body (optional) to a path relative to {@link apiBase} and returns the parsed response.
 * Used by the opt-in mutating actions (dead-letter replay), which are auth-gated server-side.
 * @param path API sub-path (e.g. "dlq/{id}/replay").
 * @param body Optional request body serialized as JSON.
 */
export async function postJson<T>(path: string, body?: unknown): Promise<T> {
  const url = new URL(path, apiBase).toString();
  let response: Response;
  try {
    response = await fetch(url, {
      method: "POST",
      headers: {
        Accept: "application/json",
        ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: "same-origin",
    });
  } catch {
    throw new ApiError(`POST ${path} failed`, 0);
  }

  if (!response.ok) {
    throw new ApiError(`POST ${path} returned ${response.status}`, response.status);
  }

  return (await response.json()) as T;
}
