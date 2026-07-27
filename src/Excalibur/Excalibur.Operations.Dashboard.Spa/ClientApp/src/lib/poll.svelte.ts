// Reusable polling helper for read-only dashboard data.
//
// Wraps an async fetcher in reactive Svelte 5 state and refreshes it on a fixed
// interval. Polling pauses while the tab is hidden (no wasted requests) and
// resumes — with an immediate refresh — when it becomes visible again. A single
// in-flight request is tracked and aborted on refresh/stop so slow responses
// never overwrite newer data.

/** A fetcher invoked by {@link Poller}; receives an abort signal for cancellation. */
export type Fetcher<T> = (signal: AbortSignal) => Promise<T>;

export class Poller<T> {
  data = $state<T | undefined>(undefined);
  error = $state<Error | undefined>(undefined);
  loading = $state(false);
  lastUpdated = $state<Date | undefined>(undefined);

  readonly #fetcher: Fetcher<T>;
  readonly #intervalMs: number;
  #timer: ReturnType<typeof setInterval> | undefined;
  #inFlight: AbortController | undefined;
  #onVisibility: (() => void) | undefined;

  constructor(fetcher: Fetcher<T>, intervalMs: number) {
    this.#fetcher = fetcher;
    this.#intervalMs = Math.max(1000, intervalMs);
  }

  /** Starts polling: an immediate refresh followed by refreshes every interval, pausing while hidden. */
  start(): void {
    if (this.#timer !== undefined) {
      return;
    }

    void this.refresh();
    this.#timer = setInterval(() => {
      if (document.visibilityState === "visible") {
        void this.refresh();
      }
    }, this.#intervalMs);

    this.#onVisibility = () => {
      if (document.visibilityState === "visible") {
        void this.refresh();
      }
    };
    document.addEventListener("visibilitychange", this.#onVisibility);
  }

  /** Stops polling and aborts any in-flight request. */
  stop(): void {
    if (this.#timer !== undefined) {
      clearInterval(this.#timer);
      this.#timer = undefined;
    }
    if (this.#onVisibility !== undefined) {
      document.removeEventListener("visibilitychange", this.#onVisibility);
      this.#onVisibility = undefined;
    }
    this.#inFlight?.abort();
    this.#inFlight = undefined;
  }

  /** Performs a single refresh now, superseding any in-flight request. */
  async refresh(): Promise<void> {
    this.#inFlight?.abort();
    const controller = new AbortController();
    this.#inFlight = controller;
    this.loading = true;

    try {
      const result = await this.#fetcher(controller.signal);
      if (controller.signal.aborted) {
        return;
      }
      this.data = result;
      this.error = undefined;
      this.lastUpdated = new Date();
    } catch (cause) {
      if (cause instanceof DOMException && cause.name === "AbortError") {
        return;
      }
      this.error = cause instanceof Error ? cause : new Error(String(cause));
    } finally {
      if (this.#inFlight === controller) {
        this.#inFlight = undefined;
        this.loading = false;
      }
    }
  }
}
