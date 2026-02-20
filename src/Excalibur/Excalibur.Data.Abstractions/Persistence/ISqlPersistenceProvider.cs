// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Specialized persistence provider for SQL databases that handles DataRequest execution
/// with SQL-specific capabilities. Advanced features (bulk operations, stored procedures,
/// statistics) are available via <see cref="GetService"/>.
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

	/// <summary>
	/// Gets an implementation-specific service. Use to access advanced SQL features
	/// such as <see cref="ISqlBulkOperations"/>, <see cref="ISqlStoredProcedures"/>,
	/// or <see cref="ISqlStatistics"/>.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	new object? GetService(Type serviceType) => null;
}

/// <summary>
/// Provides SQL bulk operation capabilities. Obtain via
/// <see cref="ISqlPersistenceProvider.GetService"/>.
/// </summary>
public interface ISqlBulkOperations
{
	/// <summary>
	/// Gets a value indicating whether this provider supports bulk operations.
	/// </summary>
	bool SupportsBulkOperations { get; }

	/// <summary>
	/// Executes a bulk DataRequest optimized for large data operations.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="bulkRequest"> The bulk DataRequest to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the bulk operation. </returns>
	Task<TResult> ExecuteBulkAsync<TResult>(
		IDataRequest<IDbConnection, TResult> bulkRequest,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides SQL stored procedure execution capabilities. Obtain via
/// <see cref="ISqlPersistenceProvider.GetService"/>.
/// </summary>
public interface ISqlStoredProcedures
{
	/// <summary>
	/// Gets a value indicating whether this provider supports stored procedures.
	/// </summary>
	bool SupportsStoredProcedures { get; }

	/// <summary>
	/// Executes a stored procedure DataRequest with support for output parameters.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="storedProcedureRequest"> The stored procedure DataRequest to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the stored procedure execution. </returns>
	Task<TResult> ExecuteStoredProcedureAsync<TResult>(
		IDataRequest<IDbConnection, TResult> storedProcedureRequest,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides SQL database statistics and schema information. Obtain via
/// <see cref="ISqlPersistenceProvider.GetService"/>.
/// </summary>
public interface ISqlStatistics
{
	/// <summary>
	/// Gets the database version.
	/// </summary>
	string DatabaseVersion { get; }

	/// <summary>
	/// Gets comprehensive database statistics and performance metrics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> Database statistics including query performance, connection pool status, and resource usage. </returns>
	Task<IDictionary<string, object>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets database schema information for a specific table or view.
	/// </summary>
	/// <param name="tableName"> The name of the table or view. </param>
	/// <param name="schemaName"> The schema name (optional). </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> Schema information including columns, indexes, and constraints. </returns>
	Task<IDictionary<string, object>> GetSchemaInfoAsync(
		string tableName,
		string? schemaName,
		CancellationToken cancellationToken);
}
