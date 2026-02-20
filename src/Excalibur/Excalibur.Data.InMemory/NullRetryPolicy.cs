// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Null retry policy implementation for in-memory provider.
/// </summary>
internal sealed class NullRetryPolicy : IDataRequestRetryPolicy
{
	/// <inheritdoc />
	public int MaxRetryAttempts => 0;

	/// <inheritdoc />
	public static TimeSpan InitialDelay => TimeSpan.Zero;

	/// <inheritdoc />
	public static TimeSpan MaxDelay => TimeSpan.Zero;

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay => TimeSpan.Zero;

	/// <inheritdoc />
	// R0.8: Remove unused parameter - interface contract requires these parameters even though null policy never retries
#pragma warning disable IDE0060

	public static bool ShouldRetry(Exception exception, int attemptNumber) => false;

#pragma warning restore IDE0060

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) => false;

	/// <inheritdoc />
	// R0.8: Remove unused parameter - interface contract requires this parameter even though null policy always returns zero
#pragma warning disable IDE0060

	public static TimeSpan GetDelay(int attemptNumber) => TimeSpan.Zero;

#pragma warning restore IDE0060

	/// <inheritdoc />
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		// No retry logic, just execute once
		var connection = await connectionFactory().ConfigureAwait(false);
		return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		// No retry logic, just execute once
		var connection = await connectionFactory().ConfigureAwait(false);
		return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
	}
}
