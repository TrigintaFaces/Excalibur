// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Defines retry policies for DataRequest execution with resilience capabilities.
/// </summary>
public interface IDataRequestRetryPolicy
{
	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	int MaxRetryAttempts { get; }

	/// <summary>
	/// Gets the base delay between retry attempts.
	/// </summary>
	/// <value> The base delay between retry attempts. </value>
	TimeSpan BaseRetryDelay { get; }

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

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception represents a transient failure; otherwise, false. </returns>
	bool ShouldRetry(Exception exception);
}
