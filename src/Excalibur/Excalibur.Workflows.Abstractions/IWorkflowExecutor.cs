// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// The replay-engine seam that drives durable workflow instances: starting them, delivering external
/// signals, and reporting status.
/// </summary>
/// <remarks>
/// The engine records every non-deterministic decision to the workflow journal and reconstructs instance
/// state by replaying it. The idempotency key for each step is derived deterministically as
/// <c>instanceId + ":" + stepOrdinal</c>; deduplication is journal-native — a journaled
/// <see cref="ActivityCompleted"/> short-circuits re-execution on replay.
/// </remarks>
public interface IWorkflowExecutor
{
	/// <summary>
	/// Starts a new durable workflow instance.
	/// </summary>
	/// <param name="workflowName">The registered workflow name to start.</param>
	/// <param name="instanceId">The caller-assigned workflow instance identifier.</param>
	/// <param name="input">The workflow input.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task that completes when the instance has been durably started.</returns>
	ValueTask StartAsync(string workflowName, string instanceId, object input, CancellationToken cancellationToken);

	/// <summary>
	/// Delivers an external signal to a running workflow instance, idempotently.
	/// </summary>
	/// <remarks>
	/// Delivery is exactly-once into the workflow journal: the signal is admitted to a dedup-keyed signal
	/// inbox on <paramref name="signalId"/>, so re-delivering the same <paramref name="signalId"/> is a
	/// no-op. The producer supplies a stable <paramref name="signalId"/> (a server-generated one would
	/// defeat deduplication). The running instance drains the inbox into its journal at a deterministic
	/// replay boundary; the signal never writes the instance journal directly.
	/// </remarks>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="signalName">The signal name a workflow body awaits via <c>WaitForSignalAsync</c>.</param>
	/// <param name="signalId">A stable, producer-supplied identifier used to deduplicate redelivery.</param>
	/// <param name="payload">The signal payload.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task that completes when the signal has been durably admitted.</returns>
	ValueTask SignalAsync(
		string instanceId,
		string signalName,
		string signalId,
		object payload,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current status of a workflow instance, or <see langword="null"/> when no instance exists.
	/// </summary>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>
	/// The current <see cref="WorkflowStatus"/> of the instance, or <see langword="null"/> when no instance with
	/// the given identifier exists. A caller must distinguish the two: an unknown instance is not a running one.
	/// </returns>
	/// <remarks>
	/// The absent case is reported as <see langword="null"/> rather than a status value so that a never-started,
	/// expired, or mistyped identifier can never be mistaken for a live instance. This mirrors
	/// <see cref="GetStateAsync"/>, which returns <see langword="null"/> for the same condition.
	/// </remarks>
	ValueTask<WorkflowStatus?> GetStatusAsync(string instanceId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a read-only projection of a workflow instance's current state, or <see langword="null"/> when no
	/// instance with the given identifier exists.
	/// </summary>
	/// <remarks>
	/// The projection is reconstructed from the journal without mutating the instance. An unknown instance
	/// identifier returns <see langword="null"/> rather than throwing, so callers use the nullable result to
	/// distinguish a missing instance from an existing one.
	/// </remarks>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The projected <see cref="WorkflowState"/>, or <see langword="null"/> when the instance does not exist.</returns>
	ValueTask<WorkflowState?> GetStateAsync(string instanceId, CancellationToken cancellationToken);
}
