// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Transactions;

/// <summary>
/// Coordinates distributed transactions using the two-phase commit (2PC) protocol.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft pattern from <c>System.Transactions.TransactionManager</c>,
/// simplified to ≤5 async methods for the ISP quality gate. The coordinator manages the lifecycle
/// of a distributed transaction across multiple <see cref="ITransactionParticipant"/> instances.
/// </para>
/// <para>
/// Phase 1 (Prepare): The coordinator asks each participant to prepare. If all vote "yes", proceed to Phase 2.
/// Phase 2 (Commit/Rollback): The coordinator instructs all participants to commit or, if any voted "no", to roll back.
/// </para>
/// </remarks>
public interface IDistributedTransactionCoordinator
{
	/// <summary>
	/// Begins a new distributed transaction.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A unique transaction identifier.</returns>
	Task<string> BeginAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Enlists a participant in the current distributed transaction.
	/// </summary>
	/// <param name="participant">The transaction participant to enlist.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous enlist operation.</returns>
	Task EnlistAsync(ITransactionParticipant participant, CancellationToken cancellationToken);

	/// <summary>
	/// Commits the distributed transaction using the two-phase commit protocol.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous commit operation.</returns>
	/// <exception cref="DistributedTransactionException">
	/// Thrown when one or more participants fail to prepare or commit.
	/// </exception>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the distributed transaction, instructing all participants to abort.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous rollback operation.</returns>
	Task RollbackAsync(CancellationToken cancellationToken);
}
