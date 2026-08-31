// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Provides transaction coordination capabilities for persistence providers.
/// Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IPersistenceProviderTransaction)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the ISP pattern — consumers that only need simple
/// data request execution use <see cref="IPersistenceProvider"/> directly.
/// Transaction-heavy workflows use this sub-interface. Connection details and the retry policy are a
/// separate capability, <see cref="IPersistenceProviderConnection"/>, because a provider can supply
/// those without being able to run a transaction.
/// </para>
/// <para>
/// Reference: <c>System.Data.IDbConnection.BeginTransaction</c> — transaction creation is
/// a separate concern from connection/query execution.
/// </para>
/// <para>
/// <strong>Offer this capability only if you can honour it.</strong> The scope returned by
/// <see cref="CreateTransactionScope"/> is ambient and open-ended: it is created before the provider
/// knows which requests will enrol in it, and those requests are then executed against it. A store
/// whose atomicity requires the full write set up front, a fixed partition key, or a retryable
/// callback cannot express that contract, and should decline this capability by returning
/// <see langword="null"/> from <see cref="IPersistenceProvider.GetService"/> rather than throwing
/// here. Declining is visible at discovery, where a caller can still choose another path; throwing is
/// only visible at the point of use, after the caller has committed to a design that cannot work.
/// </para>
/// </remarks>
public interface IPersistenceProviderTransaction
{
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
