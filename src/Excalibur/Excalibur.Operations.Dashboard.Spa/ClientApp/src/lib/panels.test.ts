import { describe, it, expect } from "vitest";

import { resolvePanel, registerPanel, type PanelProps } from "./panels";
import PlaceholderPanel from "./panels/PlaceholderPanel.svelte";
import DlqPanel from "./panels/DlqPanel.svelte";
import ProjectionLagPanel from "./panels/ProjectionLagPanel.svelte";
import WorkflowPanel from "./panels/WorkflowPanel.svelte";
import ThroughputPanel from "./panels/ThroughputPanel.svelte";
import type { Component } from "svelte";

describe("resolvePanel", () => {
  it("maps each known subsystem key to a friendly title", () => {
    expect(resolvePanel("outbox").title).toBe("Outbox");
    expect(resolvePanel("inbox").title).toBe("Inbox");
    expect(resolvePanel("dlq").title).toBe("Dead-letter queue");
    expect(resolvePanel("saga").title).toBe("Sagas");
    expect(resolvePanel("projection-lag").title).toBe("Projection lag");
    expect(resolvePanel("leader").title).toBe("Leader election");
    expect(resolvePanel("workflows").title).toBe("Workflows");
    expect(resolvePanel("throughput").title).toBe("Throughput");
  });

  it("resolves a dedicated component for a known subsystem", () => {
    expect(resolvePanel("dlq").component).toBe(DlqPanel);
  });

  it("resolves the dedicated Workflow and Throughput panels (no longer the placeholder)", () => {
    // o7bw8f: workflows/throughput previously fell back to PlaceholderPanel; now they render real panels.
    expect(resolvePanel("workflows").component).toBe(WorkflowPanel);
    expect(resolvePanel("workflows").component).not.toBe(PlaceholderPanel);
    expect(resolvePanel("throughput").component).toBe(ThroughputPanel);
    expect(resolvePanel("throughput").component).not.toBe(PlaceholderPanel);
  });

  it("resolves the projection-lag panel under BOTH the hyphenated and plural keys", () => {
    // The capability key is advertised under both spellings until confirmed; both must render the panel.
    expect(resolvePanel("projection-lag").component).toBe(ProjectionLagPanel);
    expect(resolvePanel("projections").component).toBe(ProjectionLagPanel);
  });

  it("falls back to the placeholder component for an unknown subsystem", () => {
    expect(resolvePanel("brand-new-subsystem").component).toBe(PlaceholderPanel);
  });

  it("title-cases an unknown hyphen/underscore key so it renders without a code change", () => {
    expect(resolvePanel("dead_letter-audit").title).toBe("Dead Letter Audit");
    expect(resolvePanel("cdc").title).toBe("Cdc");
  });

  it("tolerates empty segments in an unknown key without throwing", () => {
    expect(() => resolvePanel("a--b")).not.toThrow();
    expect(resolvePanel("a--b").title).toBe("A  B");
  });
});

describe("registerPanel", () => {
  it("registers a dedicated component for a previously-unknown key", () => {
    const key = "custom-metrics";
    expect(resolvePanel(key).component).toBe(PlaceholderPanel);

    // A minimal stand-in component; identity is all resolvePanel returns.
    const fake = DlqPanel as unknown as Component<PanelProps>;
    registerPanel(key, fake);

    expect(resolvePanel(key).component).toBe(fake);
  });
});
