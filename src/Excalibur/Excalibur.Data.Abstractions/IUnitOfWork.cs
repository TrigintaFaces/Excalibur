// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Represents a unit of work for coordinating database operations within a transaction.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
	/// <summary>
	/// Gets the underlying database connection.
	/// </summary>
	/// <value>
	/// The underlying database connection.
	/// </value>
	IDbConnection Connection { get; }

	/// <summary>
	/// Gets the current database transaction if one has been started.
	/// </summary>
	/// <value>
	/// The current database transaction if one has been started.
	/// </value>
	IDbTransaction? Transaction { get; }

	/// <summary>
	/// Begins a new transaction on the connection.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the operation. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task BeginTransactionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Commits the active transaction.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the operation. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the active transaction.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the operation. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RollbackAsync(CancellationToken cancellationToken);
}
