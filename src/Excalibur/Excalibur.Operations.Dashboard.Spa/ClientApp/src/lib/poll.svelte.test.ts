import { describe, it, expect, vi, afterEach } from "vitest";

import { Poller } from "./poll.svelte";

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("Poller", () => {
  it("populates data, clears error, and stamps lastUpdated on a successful refresh", async () => {
    const poller = new Poller(async () => ({ value: 42 }), 1000);

    await poller.refresh();

    expect(poller.data).toEqual({ value: 42 });
    expect(poller.error).toBeUndefined();
    expect(poller.loading).toBe(false);
    expect(poller.lastUpdated).toBeInstanceOf(Date);
  });

  it("captures a non-abort failure as error and leaves data untouched", async () => {
    const poller = new Poller<number>(async () => {
      throw new Error("boom");
    }, 1000);

    await poller.refresh();

    expect(poller.error).toBeInstanceOf(Error);
    expect(poller.error?.message).toBe("boom");
    expect(poller.data).toBeUndefined();
    expect(poller.loading).toBe(false);
  });

  it("swallows an AbortError without recording it as an error", async () => {
    const poller = new Poller<number>(async () => {
      throw new DOMException("aborted", "AbortError");
    }, 1000);

    await poller.refresh();

    expect(poller.error).toBeUndefined();
  });

  it("does not overwrite newer data when a superseded (aborted) request finally resolves", async () => {
    let firstSignal: AbortSignal | undefined;
    let call = 0;
    const poller = new Poller<string>(async (signal) => {
      call += 1;
      if (call === 1) {
        firstSignal = signal;
        // Resolve to a value the caller must ignore because this request was aborted.
        return "stale";
      }
      return "fresh";
    }, 1000);

    // Kick off the first refresh but abort it via a second refresh before asserting.
    const firstDone = poller.refresh();
    await poller.refresh(); // aborts the first controller
    await firstDone;

    expect(firstSignal?.aborted).toBe(true);
    expect(poller.data).toBe("fresh");
  });

  it("clamps a sub-second interval up to the 1000ms floor", async () => {
    const fetcher = vi.fn(async () => 1);
    vi.useFakeTimers();
    const poller = new Poller(fetcher, 10); // requested 10ms → clamped to 1000ms

    poller.start(); // immediate refresh (call 1)
    await vi.advanceTimersByTimeAsync(999);
    expect(fetcher).toHaveBeenCalledTimes(1); // no tick before the 1000ms floor

    await vi.advanceTimersByTimeAsync(1);
    expect(fetcher).toHaveBeenCalledTimes(2); // one tick at the floor

    poller.stop();
  });

  it("stops polling and aborts a genuinely in-flight request on stop()", async () => {
    let capturedSignal: AbortSignal | undefined;
    // A fetcher that stays pending until its signal aborts, so stop() has something to cancel.
    const poller = new Poller<number>(
      (signal) =>
        new Promise<number>((_, reject) => {
          capturedSignal = signal;
          signal.addEventListener("abort", () =>
            reject(new DOMException("aborted", "AbortError")),
          );
        }),
      1000,
    );

    const inFlight = poller.refresh(); // pending — never resolves on its own
    poller.stop(); // must abort the in-flight controller
    await inFlight;

    expect(capturedSignal?.aborted).toBe(true);
    expect(poller.error).toBeUndefined(); // an aborted request is not an error
  });
});
