// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Applies committed events to persisted asynchronous projections on demand.</summary>
/// <remarks>
/// The built-in processor and background worker share a fair, process-local gate for one finite page
/// at a time. Manual calls can make progress while the worker is running. Other processes cooperate
/// through positioned projection writes and a durable checkpoint compare-and-set, without inheriting
/// ambient leader election. All participants sharing a subscription name must bind the same processing
/// definition: projection types, source/destination scope and consumer revision. External handler effects
/// remain at-least-once and must be idempotent on the event identifier.
/// </remarks>
public interface IProjectionProcessor
{
	/// <summary>Processes through the stream head observed when this invocation begins, then returns.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of processed stream events and the reason the invocation stopped.</returns>
	/// <remarks>
	/// A finite target prevents concurrent producers from keeping the invocation alive indefinitely.
	/// Each page starts from the shared durable checkpoint. A competing checkpoint advance is reconciled
	/// internally; CaughtUp means that the shared subscription reached the captured target, including work
	/// completed by another compatible processor. A poison event, apply decline/fault or checkpoint failure
	/// returns Halted without acknowledging the failed page. Cancellation returns Cancelled. Partially
	/// completed pages can have effects that are not included in the returned count and are redelivered.
	/// A handler that swallows cancellation cannot acknowledge that page once the caller's token is signalled.
	/// </remarks>
	Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken);
}

/// <summary>What one projection-processing invocation did.</summary>
/// <param name="EventsApplied">
/// The number of stream events in pages processed by this invocation, including tombstones and contested
/// pages. This is not a count of unique handler effects or a measure of another processor's work.
/// </param>
/// <param name="Outcome">Why the invocation stopped.</param>
public readonly record struct ProjectionProcessingResult(long EventsApplied, ProjectionProcessingOutcome Outcome);

/// <summary>Why a projection-processing invocation stopped.</summary>
/// <remarks>Read the outcome as well as the count: no pending work, a halt and cancellation are different results.</remarks>
public enum ProjectionProcessingOutcome
{
	/// <summary>The shared subscription durably reached the stream head captured at invocation entry.</summary>
	CaughtUp = 0,

	/// <summary>Reserved for custom processors using explicit leadership; the built-in cooperative processor does not emit this result.</summary>
	NotLeader = 1,

	/// <summary>Another reader advanced the checkpoint. The built-in pull processor reconciles this internally before continuing.</summary>
	Contested = 2,

	/// <summary>Processing could not establish the next prefix; the failed page remains unacknowledged and retryable.</summary>
	Halted = 3,

	/// <summary>Reserved for custom exclusive drivers; the built-in processor admits manual and background work between pages.</summary>
	BackgroundWorkerOwnsCursor = 4,

	/// <summary>The caller cancelled before processing completed. Unacknowledged work may be delivered again.</summary>
	Cancelled = 5,
}
