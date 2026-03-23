// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Extends <see cref="IDataRequestRetryPolicy"/> with document database request execution.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by document database providers (MongoDB, Redis, InMemory) and dual-mode
/// providers (Postgres, MySql) that support the
/// <see cref="IDocumentDataRequest{TConnection, TResult}"/> execution pattern.
/// </para>
/// <para>
/// Cloud-native providers (DynamoDb, CosmosDb, Firestore) do NOT implement this interface
/// because they use their own SDK-native execution patterns.
/// </para>
/// </remarks>
public interface IDocumentDataRequestRetryPolicy : IDataRequestRetryPolicy
{
	/// <summary>
	/// Executes a document DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The document data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document data request execution. </returns>
	Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken);
}
