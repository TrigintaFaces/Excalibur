// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>Stores reader routing in the same atomic boundary as subscription member lifetimes.</summary>
/// <remarks>Writers remain pinned to their configured destination. A referenced binding cannot be retired.
/// Physical destination deletion and draining in-flight readers are separate lifecycle operations.</remarks>
public interface IProjectionGenerationStore
{
	/// <summary>Reads the active pointer and referenced member in one consistent snapshot.</summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="readerName">The stable reader name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The active generation, or null when no pointer exists.</returns>
	Task<ProjectionGeneration?> ReadGenerationAsync(string subscriptionName, string readerName, CancellationToken cancellationToken);

	/// <summary>Atomically installs a pointer only if its revision and the target lifetime's progress match.</summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="readerName">The stable reader name.</param>
	/// <param name="expectedRevision">The observed pointer revision; null requires an absent pointer.</param>
	/// <param name="projectionKey">The target member key.</param>
	/// <param name="bindingId">The exact target lifetime.</param>
	/// <param name="minimumPosition">The minimum required contiguous checkpoint; an absent checkpoint never qualifies.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if installed; false if not applied because a precondition did not match or a database concurrency conflict rolled back the mutation.</returns>
	/// <remarks>Each successful transition receives a new revision, including rollback. A connection failure can
	/// leave the outcome unknown: reread authoritative state before retrying. A successful switch does not drain readers.</remarks>
	Task<bool> TrySetGenerationAsync(string subscriptionName, string readerName, Guid? expectedRevision,
		string projectionKey, Guid bindingId, long minimumPosition, CancellationToken cancellationToken);

	/// <summary>Decommissions a reader pointer if its revision still matches.</summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="readerName">The reader being decommissioned.</param>
	/// <param name="expectedRevision">The exact observed pointer revision.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True when removed; false when absent, superseded, or not applied because of a database concurrency conflict.</returns>
	/// <remarks>Stop users of this reader before removal. This does not delete a destination or drain in-flight reads.</remarks>
	Task<bool> TryRemoveGenerationAsync(string subscriptionName, string readerName, Guid expectedRevision, CancellationToken cancellationToken);
}

/// <summary>An atomic observation of a reader pointer and its live member.</summary>
/// <param name="Revision">The pointer transition identity.</param>
/// <param name="Member">The referenced member, including its destination, binding and progress.</param>
public sealed record ProjectionGeneration(Guid Revision, SubscriptionMember Member);
