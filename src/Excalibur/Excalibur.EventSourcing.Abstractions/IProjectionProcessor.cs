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
/// <param name="HaltReason">
/// What caused a <see cref="ProjectionProcessingOutcome.Halted"/> outcome, and
/// <see cref="ProjectionHaltReason.None"/> for every other outcome. Required rather than defaulted: a
/// caller deciding whether to wait or to give up cannot act on <c>Halted</c> alone, and a default would
/// let a producer leave "not determined" looking indistinguishable from "did not halt".
/// </param>
public readonly record struct ProjectionProcessingResult(
	long EventsApplied,
	ProjectionProcessingOutcome Outcome,
	ProjectionHaltReason HaltReason);

/// <summary>What caused processing to halt, so a caller can tell a transient stop from a terminal one.</summary>
/// <remarks>
/// <para>
/// <see cref="ProjectionProcessingOutcome.Halted"/> alone is not actionable. It is produced for causes with
/// opposite correct responses: a prefix that is merely not readable yet clears on its own and should be
/// waited for, while a faulted projection, a poison event, or an unconfirmed write do not clear by waiting
/// and must never be reported to a caller as success.
/// </para>
/// <para>
/// This matters most on the inline save path, where the caller's events are ALREADY COMMITTED. Retrying a
/// transient halt there turns a needless "your durable write is pending" into a correct success; retrying a
/// terminal one turns an unconfirmed write into a reported success, which is the silent failure class the
/// positioned projection design exists to prevent. Only <see cref="PrefixUnavailable"/> is safe to retry.
/// </para>
/// </remarks>
public enum ProjectionHaltReason
{
	/// <summary>The invocation did not halt. The only valid value for a non-halted outcome.</summary>
	None = 0,

	/// <summary>
	/// The next contiguous prefix of the stream could not be read yet, which is the normal reading of an
	/// append still in flight. TRANSIENT: it clears without intervention, so waiting is the correct response.
	/// </summary>
	PrefixUnavailable = 1,

	/// <summary>
	/// An event in the page could not be deserialized into a domain event. Terminal: it will not deserialize
	/// on a later attempt, so the page is held rather than skipped and the event needs attention.
	/// </summary>
	EventUndeserializable = 2,

	/// <summary>
	/// A projection's apply threw, or its write could not be confirmed. Terminal for that projection: waiting
	/// does not resolve it, and treating an unconfirmed write as applied is precisely the silent loss this
	/// seam refuses.
	/// </summary>
	ProjectionFaulted = 3,

	/// <summary>
	/// The projection applied cleanly but its checkpoint could not be recorded, so the work is not durably
	/// acknowledged. Not necessarily permanent, but never a success: the page is redelivered.
	/// </summary>
	CheckpointWriteFailed = 4,
}

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
