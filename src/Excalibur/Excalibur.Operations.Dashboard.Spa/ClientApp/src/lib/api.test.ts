import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { getJson, postJson, getCapabilities, ApiError } from "./api";

// A minimal Response stand-in good enough for the api client (ok/status/json()).
function jsonResponse(body: unknown, init?: { ok?: boolean; status?: number }): Response {
  return {
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    json: async () => body,
  } as unknown as Response;
}

describe("api client", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  describe("getJson", () => {
    it("returns the parsed JSON on a successful response", async () => {
      vi.mocked(fetch).mockResolvedValue(jsonResponse({ configured: true, staged: 3 }));

      const result = await getJson<{ configured: boolean; staged: number }>("outbox");

      expect(result).toEqual({ configured: true, staged: 3 });
      expect(fetch).toHaveBeenCalledOnce();
      const [, init] = vi.mocked(fetch).mock.calls[0];
      expect((init as RequestInit).headers).toMatchObject({ Accept: "application/json" });
      expect((init as RequestInit).credentials).toBe("same-origin");
    });

    it("throws an ApiError carrying the HTTP status on a non-ok response", async () => {
      vi.mocked(fetch).mockResolvedValue(jsonResponse(null, { ok: false, status: 503 }));

      const error = await getJson("outbox").catch((e) => e);

      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(503);
    });

    it("wraps a network failure as an ApiError with status 0", async () => {
      vi.mocked(fetch).mockRejectedValue(new TypeError("network down"));

      const error = await getJson("outbox").catch((e) => e);

      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(0);
    });

    it("re-throws an AbortError unchanged so pollers can distinguish cancellation", async () => {
      const abort = new DOMException("aborted", "AbortError");
      vi.mocked(fetch).mockRejectedValue(abort);

      const error = await getJson("outbox").catch((e) => e);

      expect(error).toBe(abort);
      expect(error).not.toBeInstanceOf(ApiError);
    });
  });

  describe("postJson", () => {
    it("POSTs a JSON body with a Content-Type header", async () => {
      vi.mocked(fetch).mockResolvedValue(jsonResponse({ replayed: true, count: 1 }));

      const result = await postJson<{ replayed: boolean }>("dlq/replay-batch", { messageType: "X" });

      expect(result).toEqual({ replayed: true, count: 1 });
      const [, init] = vi.mocked(fetch).mock.calls[0];
      expect((init as RequestInit).method).toBe("POST");
      expect((init as RequestInit).headers).toMatchObject({ "Content-Type": "application/json" });
      expect((init as RequestInit).body).toBe(JSON.stringify({ messageType: "X" }));
    });

    it("omits the Content-Type header and body when no body is supplied", async () => {
      vi.mocked(fetch).mockResolvedValue(jsonResponse({ replayed: true, count: 1 }));

      await postJson("dlq/00000000-0000-0000-0000-000000000000/replay");

      const [, init] = vi.mocked(fetch).mock.calls[0];
      expect((init as RequestInit).body).toBeUndefined();
      expect((init as RequestInit).headers).not.toHaveProperty("Content-Type");
    });

    it("throws an ApiError with the status on a non-ok POST", async () => {
      vi.mocked(fetch).mockResolvedValue(jsonResponse(null, { ok: false, status: 401 }));

      const error = await postJson("dlq/x/replay").catch((e) => e);

      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(401);
    });
  });

  describe("getCapabilities", () => {
    it("GETs the capability-discovery root path", async () => {
      vi.mocked(fetch).mockResolvedValue(
        jsonResponse({ subsystems: ["outbox", "dlq"], mutatingActionsEnabled: true }),
      );

      const caps = await getCapabilities();

      expect(caps.subsystems).toContain("outbox");
      expect(caps.mutatingActionsEnabled).toBe(true);
      // The discovery path is the API base itself (empty sub-path).
      const [url] = vi.mocked(fetch).mock.calls[0];
      expect(String(url)).toMatch(/\/api\/$/);
    });
  });
});
