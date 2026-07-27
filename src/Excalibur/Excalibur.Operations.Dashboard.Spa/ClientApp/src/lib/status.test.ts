import { describe, it, expect } from "vitest";

import { panelStatus } from "./status";

describe("panelStatus", () => {
  it("reports 'disconnected' when there is an error, regardless of other state", () => {
    expect(panelStatus(true, true, new Date())).toBe("disconnected");
    expect(panelStatus(true, undefined, undefined)).toBe("disconnected");
    expect(panelStatus(true, false, undefined)).toBe("disconnected");
  });

  it("reports 'loading…' while configured state is unknown", () => {
    expect(panelStatus(false, undefined, undefined)).toBe("loading…");
  });

  it("reports 'not configured' when the subsystem is absent", () => {
    expect(panelStatus(false, false, undefined)).toBe("not configured");
  });

  it("reports the last-updated time when configured and a timestamp is present", () => {
    const at = new Date(2026, 0, 1, 13, 45, 30);
    expect(panelStatus(false, true, at)).toBe(`updated ${at.toLocaleTimeString()}`);
  });

  it("reports a bare 'updated' when configured but no timestamp yet", () => {
    expect(panelStatus(false, true, undefined)).toBe("updated");
  });

  it("prioritises error over not-configured (error wins the branch order)", () => {
    // A disconnected panel whose last-known state was not-configured still reads 'disconnected'.
    expect(panelStatus(true, false, undefined)).toBe("disconnected");
  });
});
