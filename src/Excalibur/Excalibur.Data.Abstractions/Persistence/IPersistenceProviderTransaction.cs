// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides transaction coordination capabilities for persistence providers.
/// Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IPersistenceProviderTransaction)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the ISP pattern — consumers that only need simple
/// data request execution use <see cref="IPersistenceProvider"/> directly.
/// Transaction-heavy workflows use this sub-interface.
/// </para>
/// <para>
/// Reference: <c>System.Data.IDbConnection.BeginTransaction</c> — transaction creation is
/// a separate concern from connection/query execution.
/// </para>
/// </remarks>
public interface IPersistenceProviderTransaction
{
	/// <summary>
	/// Gets the connection string or connection configuration for the provider.
	/// </summary>
	/// <value>
	/// The connection string or connection configuration for the provider.
	/// </value>
	string ConnectionString { get; }

	/// <summary>
	/// Gets the retry policy used for DataRequest execution.
	/// </summary>
	/// <value>
	/// The retry policy used for DataRequest execution.
	/// </value>
	IDataRequestRetryPolicy RetryPolicy { get; }

	/// <summary>
	/// Executes a DataRequest within a transaction scope with retry logic.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="transactionScope"> The transaction scope to use. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable;

	/// <summary>
	/// Creates a new transaction scope for coordinating operations across multiple DataRequests.
	/// </summary>
	/// <param name="isolationLevel"> The transaction isolation level. </param>
	/// <param name="timeout"> The transaction timeout. </param>
	/// <returns> A new transaction scope. </returns>
	ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null);
}
