// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Extends <see cref="IDataRequestRetryPolicy"/> with relational (SQL) data request execution.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by relational database providers (SqlServer, Postgres, MySql) that support
/// the <see cref="IDataRequest{TConnection, TResult}"/> connection-factory execution pattern.
/// </para>
/// <para>
/// Cloud-native providers (DynamoDb, CosmosDb, Firestore) do NOT implement this interface
/// because they use their own SDK-native execution patterns.
/// </para>
/// </remarks>
public interface IRelationalDataRequestRetryPolicy : IDataRequestRetryPolicy
{
	/// <summary>
	/// Executes a DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken);
}
