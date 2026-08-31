// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Specialized persistence provider for SQL databases that handles DataRequest execution
/// with SQL-specific capabilities. Implementation-specific services are available via
/// <see cref="IPersistenceProvider.GetService"/>.
/// </summary>
public interface ISqlPersistenceProvider : IPersistenceProvider
{
	/// <summary>
	/// Gets the database type (e.g., "Postgres", "SqlServer", "MySQL").
	/// </summary>
	/// <value>
	/// The database type (e.g., "Postgres", "SqlServer", "MySQL").
	/// </value>
	string DatabaseType { get; }

	/// <summary>
	/// Executes a batch of DataRequests as a single unit for improved performance. All requests must succeed or the entire batch will
	/// be rolled back.
	/// </summary>
	/// <param name="requests"> The collection of DataRequests to execute as a batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of results corresponding to each request in the batch. </returns>
	Task<IEnumerable<object>> ExecuteBatchAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a batch of DataRequests within a transaction scope.
	/// </summary>
	/// <param name="requests"> The collection of DataRequests to execute as a batch. </param>
	/// <param name="transactionScope"> The transaction scope to use. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of results corresponding to each request in the batch. </returns>
	Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that a DataRequest is compatible with this SQL provider.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The DataRequest to validate. </param>
	/// <returns> True if the request is valid for this provider; otherwise, false. </returns>
	bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request);
}
