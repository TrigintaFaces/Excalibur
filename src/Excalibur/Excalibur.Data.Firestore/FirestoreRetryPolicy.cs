// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;

using Grpc.Core;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Retry policy implementation for Firestore operations.
/// </summary>
internal sealed class FirestoreRetryPolicy : IDataRequestRetryPolicy
{
	private FirestoreRetryPolicy()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the Firestore retry policy.
	/// </summary>
	public static FirestoreRetryPolicy Instance { get; } = new();

	/// <inheritdoc/>
	public int MaxRetryAttempts => 3;

	/// <inheritdoc/>
	public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(100);

	/// <inheritdoc/>
	public bool ShouldRetry(Exception exception)
	{
		// Firestore SDK uses gRPC, so we handle RpcException
		return exception switch
		{
			RpcException rpcEx => IsTransientStatusCode(rpcEx.StatusCode),
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
			"Use cloud-native specific methods (GetByIdAsync, CreateAsync, etc.) for Firestore operations.");
	}

	/// <inheritdoc/>
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		await Task.CompletedTask.ConfigureAwait(false);
		throw new NotSupportedException(
			"Use cloud-native specific methods (GetByIdAsync, CreateAsync, etc.) for Firestore operations.");
	}

	private static bool IsTransientStatusCode(StatusCode statusCode) =>
		statusCode switch
		{
			StatusCode.Unavailable => true,
			StatusCode.DeadlineExceeded => true,
			StatusCode.Aborted => true,
			StatusCode.ResourceExhausted => true,
			StatusCode.Internal => true,
			_ => false
		};
}
