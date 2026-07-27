import type { Component } from "svelte";

import DlqPanel from "./panels/DlqPanel.svelte";
import InboxPanel from "./panels/InboxPanel.svelte";
import LeaderPanel from "./panels/LeaderPanel.svelte";
import OutboxPanel from "./panels/OutboxPanel.svelte";
import PlaceholderPanel from "./panels/PlaceholderPanel.svelte";
import ProjectionLagPanel from "./panels/ProjectionLagPanel.svelte";
import SagaPanel from "./panels/SagaPanel.svelte";
import ThroughputPanel from "./panels/ThroughputPanel.svelte";
import WorkflowPanel from "./panels/WorkflowPanel.svelte";

/** Props every subsystem panel component receives. */
export interface PanelProps {
  /** The capability/subsystem key this panel renders (e.g. "outbox"). */
  subsystem: string;
  /** The human-readable panel title. */
  title: string;
  /**
   * Whether the host advertises mutating actions (e.g. dead-letter replay) as enabled. Panels use
   * this to render opt-in action controls; the endpoints themselves are auth-gated and only mapped
   * server-side when this is true.
   */
  mutatingActionsEnabled: boolean;
}

/** A registered dashboard panel: how to title and render a subsystem. */
export interface PanelDescriptor {
  /** Human-readable title. */
  title: string;
  /** The Svelte component that renders the subsystem's data. */
  component: Component<PanelProps>;
}

// Friendly titles for the subsystem keys the read API can advertise. Unknown
// keys fall back to a title-cased version of the key, so a newly-added
// subsystem still renders without a code change here.
const titles: Record<string, string> = {
  outbox: "Outbox",
  inbox: "Inbox",
  dlq: "Dead-letter queue",
  saga: "Sagas",
  "projection-lag": "Projection lag",
  leader: "Leader election",
  workflows: "Workflows",
  throughput: "Throughput",
};

// Dedicated panels per subsystem key. Subsystems without a dedicated panel yet
// render the placeholder, so capability-driven visibility works end to end.
const components: Record<string, Component<PanelProps>> = {
  outbox: OutboxPanel,
  inbox: InboxPanel,
  dlq: DlqPanel,
  saga: SagaPanel,
  leader: LeaderPanel,
  // The projection-lag module's capability key is registered under both spellings
  // until confirmed, so the dedicated panel renders regardless.
  "projection-lag": ProjectionLagPanel,
  projections: ProjectionLagPanel,
  workflows: WorkflowPanel,
  throughput: ThroughputPanel,
};

function titleFor(subsystem: string): string {
  return (
    titles[subsystem] ??
    subsystem
      .split(/[-_]/)
      .map((part) => (part.length > 0 ? part[0].toUpperCase() + part.slice(1) : part))
      .join(" ")
  );
}

/** Resolves the panel descriptor for a subsystem key, falling back to a placeholder. */
export function resolvePanel(subsystem: string): PanelDescriptor {
  return {
    title: titleFor(subsystem),
    component: components[subsystem] ?? PlaceholderPanel,
  };
}

/** Registers a dedicated panel component for a subsystem key (called by panel lanes). */
export function registerPanel(subsystem: string, component: Component<PanelProps>): void {
  components[subsystem] = component;
}
