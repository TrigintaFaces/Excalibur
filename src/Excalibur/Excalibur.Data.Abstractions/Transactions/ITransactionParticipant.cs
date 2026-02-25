// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Transactions;

/// <summary>
/// Represents a participant in a distributed two-phase commit (2PC) transaction.
/// </summary>
/// <remarks>
/// <para>
/// Each participant is responsible for its own local resource (database, message broker, etc.)
/// and must be able to prepare, commit, or roll back its portion of a distributed transaction.
/// </para>
/// <para>
/// Follows the <c>System.Transactions.IEnlistmentNotification</c> pattern from the .NET BCL,
/// simplified to 3 async methods for modern usage with ≤5-method interface gate.
/// </para>
/// </remarks>
public interface ITransactionParticipant
{
	/// <summary>
	/// Gets the unique identifier for this participant.
	/// </summary>
	/// <value>The unique identifier for this participant.</value>
	string ParticipantId { get; }

	/// <summary>
	/// Prepares the participant's local resource for commit (vote phase of 2PC).
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> if the participant votes to commit; <see langword="false"/> if it votes to abort.
	/// </returns>
	Task<bool> PrepareAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Commits the participant's local resource (commit phase of 2PC).
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous commit operation.</returns>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the participant's local resource.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous rollback operation.</returns>
	Task RollbackAsync(CancellationToken cancellationToken);
}
