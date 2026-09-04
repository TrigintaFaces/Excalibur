// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Workflows;

/// <summary>
/// The append-only journal event base for a durable workflow instance. Each entry records a single
/// deterministic decision or side-effect boundary; replaying the ordered sequence reconstructs the
/// workflow's state.
/// </summary>
/// <remarks>
/// <see cref="AggregateId"/> carries the workflow instance identifier and <see cref="Version"/> carries the
/// monotonic replay cursor within that instance. Payloads are kept as strings and primitives so entries
/// serialize deterministically through the canonical serializer.
/// </remarks>
public abstract record WorkflowJournalEvent : IDomainEvent
{
	/// <summary>
	/// Gets the unique identifier for this journal entry.
	/// </summary>
	/// <value>The unique identifier for this journal entry.</value>
	public string EventId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the workflow instance identifier this entry belongs to.
	/// </summary>
	/// <value>The workflow instance identifier.</value>
	public string AggregateId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the replay cursor position of this entry within the workflow instance.
	/// </summary>
	/// <value>The monotonic replay cursor for this entry.</value>
	public long Version { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this entry was journaled.
	/// </summary>
	/// <value>The UTC timestamp when this entry was journaled.</value>
	public DateTimeOffset OccurredAt { get; init; }


	/// <summary>
	/// Gets optional metadata for cross-cutting concerns.
	/// </summary>
	/// <value>Optional metadata for cross-cutting concerns.</value>
	public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Records that a workflow instance has started.
/// </summary>
[MessageName("Excalibur.Workflows.WorkflowStarted")]
public sealed record WorkflowStarted : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the registered workflow name that was started.
	/// </summary>
	/// <value>The registered workflow name.</value>
	public string WorkflowName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the workflow definition version this instance is pinned to for its entire lifetime.
	/// </summary>
	/// <remarks>
	/// The version is stamped into this first journal entry at start and never re-resolved: an in-flight
	/// instance always replays against the definition it began on, even after a newer version is registered.
	/// Only new instances bind the latest registered version. This is the determinism anchor for workflow
	/// versioning.
	/// </remarks>
	/// <value>The pinned definition version. Defaults to <c>1</c>.</value>
	public int DefinitionVersion { get; init; } = 1;
}

/// <summary>
/// Records that an activity has been scheduled for execution.
/// </summary>
[MessageName("Excalibur.Workflows.ActivityScheduled")]
public sealed record ActivityScheduled : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the registered activity name that was scheduled.
	/// </summary>
	/// <value>The registered activity name.</value>
	public string ActivityName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the ordinal of this activity step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }
}

/// <summary>
/// Records that a scheduled activity completed successfully.
/// </summary>
[MessageName("Excalibur.Workflows.ActivityCompleted")]
public sealed record ActivityCompleted : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the registered activity name that completed.
	/// </summary>
	/// <value>The registered activity name.</value>
	public string ActivityName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the ordinal of this activity step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }

	/// <summary>
	/// Gets the serialized activity result, if any.
	/// </summary>
	/// <value>The serialized activity result, or <see langword="null"/> when there is none.</value>
	public string? ResultJson { get; init; }
}

/// <summary>
/// Records that a scheduled activity failed.
/// </summary>
[MessageName("Excalibur.Workflows.ActivityFailed")]
public sealed record ActivityFailed : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the registered activity name that failed.
	/// </summary>
	/// <value>The registered activity name.</value>
	public string ActivityName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the ordinal of this activity step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }

	/// <summary>
	/// Gets the failure detail, if any.
	/// </summary>
	/// <value>The failure detail, or <see langword="null"/> when there is none.</value>
	public string? Error { get; init; }
}

/// <summary>
/// Records that a durable timer has been created.
/// </summary>
[MessageName("Excalibur.Workflows.TimerCreated")]
public sealed record TimerCreated : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the duration to wait before the timer fires.
	/// </summary>
	/// <value>The timer delay.</value>
	public TimeSpan Delay { get; init; }

	/// <summary>
	/// Gets the ordinal of this timer step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }
}

/// <summary>
/// Records that a durable timer has fired.
/// </summary>
[MessageName("Excalibur.Workflows.TimerFired")]
public sealed record TimerFired : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the ordinal of the timer step that fired within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }
}

/// <summary>
/// Records a deterministic UTC time read performed inside a workflow body. The recorded instant is the
/// journal entry's <see cref="WorkflowJournalEvent.OccurredAt"/>, so replay reproduces the value the workflow
/// observed on first execution.
/// </summary>
[MessageName("Excalibur.Workflows.WorkflowTimeRead")]
public sealed record WorkflowTimeRead : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the ordinal of this time-read step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }
}

/// <summary>
/// Records a deterministic identifier generated inside a workflow body, so replay reproduces the same
/// identifier rather than generating a new one.
/// </summary>
[MessageName("Excalibur.Workflows.WorkflowGuidCreated")]
public sealed record WorkflowGuidCreated : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the ordinal of this identifier-generation step within the workflow instance.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }

	/// <summary>
	/// Gets the generated identifier in 32-digit hexadecimal ("N") form.
	/// </summary>
	/// <value>The generated identifier as a 32-character hexadecimal string.</value>
	public string Value { get; init; } = string.Empty;
}

/// <summary>
/// Records that an external signal has been received.
/// </summary>
[MessageName("Excalibur.Workflows.SignalReceived")]
public sealed record SignalReceived : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the name of the received signal.
	/// </summary>
	/// <value>The signal name.</value>
	public string SignalName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the stable, producer-supplied identifier of the consumed signal, used to deduplicate delivery.
	/// </summary>
	/// <value>The producer-supplied signal identifier.</value>
	public string SignalId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the ordinal of the workflow step at which this signal was consumed.
	/// </summary>
	/// <value>The zero-based step ordinal.</value>
	public int StepOrdinal { get; init; }

	/// <summary>
	/// Gets the serialized signal payload, if any.
	/// </summary>
	/// <value>The serialized signal payload, or <see langword="null"/> when there is none.</value>
	public string? PayloadJson { get; init; }
}

/// <summary>
/// Records that a workflow instance has completed.
/// </summary>
[MessageName("Excalibur.Workflows.WorkflowCompleted")]
public sealed record WorkflowCompleted : WorkflowJournalEvent
{

	/// <summary>
	/// Gets the serialized workflow result, if any.
	/// </summary>
	/// <value>The serialized workflow result, or <see langword="null"/> when there is none.</value>
	public string? ResultJson { get; init; }
}
