// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Retry policy implementation for DynamoDB operations.
/// AWS SDK has built-in retry logic, so this policy handles additional transient failures.
/// </summary>
internal sealed class DynamoDbRetryPolicy : IDataRequestRetryPolicy
{
	private DynamoDbRetryPolicy()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the DynamoDB retry policy.
	/// </summary>
	public static DynamoDbRetryPolicy Instance { get; } = new();

	/// <inheritdoc/>
	public int MaxRetryAttempts => 3;

	/// <inheritdoc/>
	public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(100);

	/// <inheritdoc/>
	public bool ShouldRetry(Exception exception)
	{
		// AWS SDK handles most retries internally.
		// We handle additional transient failures here.
		return exception switch
		{
			AmazonDynamoDBException dynamoEx => IsTransientStatusCode(dynamoEx.StatusCode),
			HttpRequestException => true,
			TaskCanceledException => false,
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
		await Task.CompletedTask.ConfigureAwait(false);
		throw new NotSupportedException(
			"Use cloud-native specific methods (GetByIdAsync, CreateAsync, etc.) for DynamoDB operations.");
	}

	/// <inheritdoc/>
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		await Task.CompletedTask.ConfigureAwait(false);
		throw new NotSupportedException(
			"Use cloud-native specific methods (GetByIdAsync, CreateAsync, etc.) for DynamoDB operations.");
	}

	private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
		statusCode switch
		{
			HttpStatusCode.RequestTimeout => true,
			HttpStatusCode.ServiceUnavailable => true,
			HttpStatusCode.GatewayTimeout => true,
			HttpStatusCode.TooManyRequests => true,
			HttpStatusCode.InternalServerError => true,
			_ => false
		};
}
