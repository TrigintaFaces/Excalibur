// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Retry policy implementation for Cosmos DB operations.
/// Cosmos DB SDK has built-in retry logic for rate-limited requests,
/// so this policy handles additional transient failures not covered by the SDK.
/// </summary>
internal sealed class CosmosDbRetryPolicy : IDataRequestRetryPolicy
{
	private CosmosDbRetryPolicy()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the Cosmos DB retry policy.
	/// </summary>
	public static CosmosDbRetryPolicy Instance { get; } = new();

	/// <inheritdoc/>
	public int MaxRetryAttempts => 9; // Default Cosmos DB SDK value

	/// <inheritdoc/>
	public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(100);

	/// <inheritdoc/>
	public bool ShouldRetry(Exception exception)
	{
		// Cosmos DB SDK handles retries internally for rate-limited (429) requests.
		// We handle additional transient failures here.
		return exception switch
		{
			CosmosException cosmosEx => IsTransientStatusCode(cosmosEx.StatusCode),
			HttpRequestException => true,
			TaskCanceledException => false, // Don't retry cancellations
			OperationCanceledException => false,
			_ => false
		};
	}

	/// <inheritdoc/>
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		// Cosmos DB doesn't use traditional connection patterns.
		// The SDK manages connections internally. This method throws because
		// CosmosDbPersistenceProvider uses its own cloud-native methods.
		await Task.CompletedTask.ConfigureAwait(false);
		throw new NotSupportedException(
			ErrorMessages.UseCloudNativeMethodsForCosmosDbOperations);
	}

	/// <inheritdoc/>
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		// Cosmos DB doesn't use traditional connection patterns.
		// The SDK manages connections internally. This method throws because
		// CosmosDbPersistenceProvider uses its own cloud-native methods.
		await Task.CompletedTask.ConfigureAwait(false);
		throw new NotSupportedException(
			ErrorMessages.UseCloudNativeMethodsForCosmosDbOperations);
	}

	private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
		statusCode switch
		{
			HttpStatusCode.RequestTimeout => true,
			HttpStatusCode.ServiceUnavailable => true,
			HttpStatusCode.GatewayTimeout => true,
			HttpStatusCode.TooManyRequests => true, // 429 - though SDK handles this
			_ => false
		};
}
