// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Execution;

/// <summary>
/// Defines a provider-neutral contract for querying a data store.
/// Implementations must be async, streaming-friendly where applicable,
/// and free of provider-specific types.
/// </summary>
public interface IQueryExecutor
{
	/// <summary>
	/// Executes a query asynchronously and returns a stream of typed results.
	/// </summary>
	/// <typeparam name="T">Result item type.</typeparam>
	/// <param name="queryText">The query text to execute.</param>
	/// <param name="parameters">Optional named parameters for the query.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>An async stream of <typeparamref name="T"/> items.</returns>
	IAsyncEnumerable<T> QueryAsync<T>(
		string queryText,
		IReadOnlyDictionary<string, object?>? parameters,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a query asynchronously and returns a single typed result or null if none.
	/// </summary>
	/// <typeparam name="T">Result item type.</typeparam>
	/// <param name="queryText">The query text to execute.</param>
	/// <param name="parameters">Optional named parameters for the query.</param>
	/// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
	/// <returns>A single result or null.</returns>
	Task<T?> QuerySingleAsync<T>(
		string queryText,
		IReadOnlyDictionary<string, object?>? parameters,
		CancellationToken cancellationToken);
}
