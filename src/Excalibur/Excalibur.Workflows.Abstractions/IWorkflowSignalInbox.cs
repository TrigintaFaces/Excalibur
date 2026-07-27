// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// A dedup-keyed inbox that admits external signals for a workflow instance without writing the instance's
/// own journal. The running instance is the sole journal writer; it drains admitted signals into its journal
/// at a deterministic replay boundary.
/// </summary>
/// <remarks>
/// <para>
/// This mailbox exists so that no external actor addresses the instance journal directly: a signal enters
/// only here, keyed by <c>(instanceId, signalId)</c>, and the instance decides — deterministically, at its
/// own step ordinals — when and in what order to consume it. Admission is a conditional insert, so
/// redelivering the same <c>signalId</c> is idempotent for as long as the admitting inbox retains the key.
/// </para>
/// <para>
/// <strong>Durability is a property of the implementation, not of this contract.</strong> An implementation
/// backed by process memory loses both admitted signals and their deduplication keys when the process ends:
/// a signal admitted but not yet drained is lost, and a producer's redelivery of the same
/// <c>(instanceId, signalId)</c> after a restart is admitted a second time. The idempotence promised by
/// <see cref="TryEnqueueAsync"/> holds for the lifetime of the inbox instance and no longer. Match the
/// inbox's durability to the journal's: a durable journal paired with an in-process inbox is durable only
/// for the workflow's own decisions, never for the signals that reach it.
/// </para>
/// </remarks>
public interface IWorkflowSignalInbox
{
	/// <summary>
	/// Admits a signal for an instance if one with the same <paramref name="signalId"/> is not already
	/// present. This is a conditional insert (never an upsert), so redelivery is idempotent.
	/// </summary>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="signalId">The stable, producer-supplied signal identifier used for deduplication.</param>
	/// <param name="signalName">The signal name a workflow body awaits.</param>
	/// <param name="payloadJson">The serialized signal payload, if any.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns><see langword="true"/> if the signal was newly admitted; <see langword="false"/> if a signal
	/// with the same identifier was already present.</returns>
	ValueTask<bool> TryEnqueueAsync(
		string instanceId,
		string signalId,
		string signalName,
		string? payloadJson,
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns the admitted signals for an instance in ascending inbox-sequence (arrival) order.
	/// </summary>
	/// <remarks>
	/// Ordering is the inbox's own monotonic append sequence, never a wall-clock timestamp, so consumption
	/// order is deterministic and reproducible across producers and processes.
	/// </remarks>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The admitted signals in arrival order.</returns>
	ValueTask<IReadOnlyList<WorkflowSignalEntry>> DrainAsync(string instanceId, CancellationToken cancellationToken);
}

/// <summary>
/// A single admitted signal in a workflow instance's signal inbox.
/// </summary>
public sealed record WorkflowSignalEntry
{
	/// <summary>
	/// Gets the inbox's monotonic arrival sequence for this entry.
	/// </summary>
	/// <value>The ascending arrival sequence.</value>
	public long Sequence { get; init; }

	/// <summary>
	/// Gets the stable, producer-supplied signal identifier.
	/// </summary>
	/// <value>The producer-supplied signal identifier.</value>
	public string SignalId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the signal name.
	/// </summary>
	/// <value>The signal name.</value>
	public string SignalName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the serialized signal payload, if any.
	/// </summary>
	/// <value>The serialized signal payload, or <see langword="null"/> when there is none.</value>
	public string? PayloadJson { get; init; }
}
