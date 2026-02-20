// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines a transaction scope for managing distributed transactions across multiple providers.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the ISP pattern with ≤5 methods. Advanced capabilities
/// (savepoints, nested scopes, callbacks) are available via sub-interfaces:
/// </para>
/// <list type="bullet">
/// <item><see cref="ITransactionScopeCallbacks"/> — OnCommit/OnRollback/OnComplete lifecycle callbacks.</item>
/// <item><see cref="ITransactionScopeAdvanced"/> — Savepoints and nested transaction scopes.</item>
/// </list>
/// <para>
/// Implementations that support these capabilities should also implement the sub-interfaces.
/// Consumers can check via pattern matching: <c>if (scope is ITransactionScopeAdvanced advanced) { ... }</c>
/// </para>
/// </remarks>
public interface ITransactionScope : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Gets the unique identifier for this transaction scope.
	/// </summary>
	/// <value>
	/// The unique identifier for this transaction scope.
	/// </value>
	string TransactionId { get; }

	/// <summary>
	/// Gets the isolation level of the transaction.
	/// </summary>
	/// <value>
	/// The isolation level of the transaction.
	/// </value>
	IsolationLevel IsolationLevel { get; }

	/// <summary>
	/// Gets the transaction status.
	/// </summary>
	/// <value>
	/// The transaction status.
	/// </value>
	TransactionStatus Status { get; }

	/// <summary>
	/// Gets the transaction start time.
	/// </summary>
	/// <value>
	/// The transaction start time.
	/// </value>
	DateTimeOffset StartTime { get; }

	/// <summary>
	/// Gets or sets the transaction timeout.
	/// </summary>
	/// <value>
	/// The transaction timeout.
	/// </value>
	TimeSpan Timeout { get; set; }

	/// <summary>
	/// Enlists a persistence provider in the transaction.
	/// </summary>
	/// <param name="provider"> The provider to enlist. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken);

	/// <summary>
	/// Enlists a database connection in the transaction.
	/// </summary>
	/// <param name="connection"> The connection to enlist. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken);

	/// <summary>
	/// Commits the transaction across all enlisted providers.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task CommitAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back the transaction across all enlisted providers.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RollbackAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets all enlisted providers.
	/// </summary>
	/// <returns> Collection of enlisted providers. </returns>
	IEnumerable<IPersistenceProvider> GetEnlistedProviders();
}
