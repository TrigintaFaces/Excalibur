// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>A payload-free capability for atomic acquisition-scoped dead-letter completion.</summary>
/// <remarks>
/// Resolve through the store's IServiceProvider capability seam. This is separate from ordinary claim
/// completion because a store may support delivery without advertising a dead-letter transition.
/// <para>
/// This facet operates on the same owning IOutboxStore rows already subject to its row-partition
/// capability gate; it is not an independently registered tenant store. The globally persisted ID and
/// exact acquisition address the row. Retain or copy that row's tenant provenance during the terminal
/// transition, without adding a caller-supplied or ambient tenant predicate to the cross-tenant drain.
/// Tenant-facing downstream work establishes its owner scope from the recorded row.
/// </para>
/// </remarks>
public interface IClaimScopedDeadLetterableOutboxStore
{
	/// <summary>Dead-letters a message only while the caller holds its current acquisition.</summary>
	/// <param name="messageId">The persisted identifier captured at acquisition.</param>
	/// <param name="reason">The reason for the terminal disposition.</param>
	/// <param name="attemptCount">Nonnegative logical attempt count; applied transitions retain its maximum.</param>
	/// <param name="claimIdentity">The nonblank identity captured from the acquisition, never a process ID.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The outcome decided atomically with the guarded transition.</returns>
	/// <remarks>
	/// Validate arguments before mutation. Missing/released/different leases return ClaimLost, retained
	/// terminal rows AlreadyTerminal and absent rows MessageNotFound. Refusals are returned, not thrown.
	/// No completion may classify its outcome by rereading after a separate mutation.
	/// </remarks>
	ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
		string messageId,
		string reason,
		int attemptCount,
		string claimIdentity,
		CancellationToken cancellationToken);
}
