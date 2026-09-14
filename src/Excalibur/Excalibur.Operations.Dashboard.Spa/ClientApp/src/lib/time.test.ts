import { describe, it, expect } from "vitest";

import { formatServerInstant, hasExplicitOffset, parseServerInstant } from "./time";

// The requirement: a server timestamp is an INSTANT, and the dashboard must either render that
// instant correctly or render nothing. The predicate each test asserts is named in its title,
// because "it parses dates" would pass on the defect this module exists to prevent.
describe("parseServerInstant", () => {
  // THE DEFECT ARM. A naive string is not a parse failure in JavaScript -- it succeeds, and
  // yields a different instant per viewer. Rejecting is the only outcome that cannot mislead.
  it("REJECTS a naive string, because JavaScript would silently read it as the viewer's local time", () => {
    expect(parseServerInstant("2026-07-25T14:00:00")).toBeNull();
    expect(parseServerInstant("2026-07-25T14:00")).toBeNull();
    expect(parseServerInstant("2026-07-25 14:00:00")).toBeNull();
  });

  // THE LIVENESS ARM. Without this, a module that returned null for everything would pass above.
  it("accepts an offset-bearing string and yields the instant it names, not a local reading", () => {
    expect(parseServerInstant("2026-07-25T14:00:00Z")!.toISOString()).toBe("2026-07-25T14:00:00.000Z");
    expect(parseServerInstant("2026-07-25T14:00:00.500Z")!.toISOString()).toBe("2026-07-25T14:00:00.500Z");
    // +02:00 at 16:00 local is the same instant as 14:00Z -- proves the offset is honoured
    // rather than ignored, which a substring check for "Z" would not catch.
    expect(parseServerInstant("2026-07-25T16:00:00+02:00")!.toISOString()).toBe("2026-07-25T14:00:00.000Z");
    expect(parseServerInstant("2026-07-25T12:00:00-02:00")!.toISOString()).toBe("2026-07-25T14:00:00.000Z");
  });

  it("returns null for absent or blank values rather than Invalid Date", () => {
    expect(parseServerInstant(null)).toBeNull();
    expect(parseServerInstant(undefined)).toBeNull();
    expect(parseServerInstant("   ")).toBeNull();
  });

  it("returns null for a string that carries an offset but is not a real time", () => {
    expect(parseServerInstant("2026-13-45T99:99:99Z")).toBeNull();
  });
});

describe("hasExplicitOffset", () => {
  it("distinguishes the two forms ECMAScript treats differently", () => {
    expect(hasExplicitOffset("2026-07-25T14:00:00Z")).toBe(true);
    expect(hasExplicitOffset("2026-07-25T14:00:00+05:30")).toBe(true);
    expect(hasExplicitOffset("2026-07-25T14:00:00")).toBe(false);
  });
});

describe("formatServerInstant", () => {
  it("renders the visible placeholder rather than a plausible wrong time", () => {
    expect(formatServerInstant("2026-07-25T14:00:00")).toBe("—");
    expect(formatServerInstant(null)).toBe("—");
  });

  it("renders a real value when the contract is met", () => {
    // Not asserting the locale string itself -- that varies by host -- only that a conforming
    // timestamp produces something other than the placeholder.
    expect(formatServerInstant("2026-07-25T14:00:00Z")).not.toBe("—");
  });
});
