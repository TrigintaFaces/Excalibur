// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Spanner.Data;

namespace Excalibur.Data.Spanner;

/// <summary>
/// Creates Google Cloud Spanner connections and executes work under Spanner's optimistic-concurrency model,
/// transparently replaying transactions that Spanner surfaces as retryable (<c>ABORTED</c>).
/// </summary>
/// <remarks>
/// Spanner has no pessimistic row locks (<c>FOR UPDATE ... SKIP LOCKED</c> does not exist); concurrent writers
/// are serialized optimistically and the loser observes an <c>ABORTED</c> transaction. The
/// <c>Excalibur.*.Spanner</c> stores build their append-with-expected-version and reserve semantics on
/// <see cref="ExecuteInRetryableTransactionAsync{T}"/> so a lost update is retried rather than lost.
/// </remarks>
public interface ISpannerConnectionProvider
{
	/// <summary>Creates a new, unopened Spanner connection for the configured database.</summary>
	/// <returns>A new <see cref="SpannerConnection"/>. The caller owns its lifetime and must dispose it.</returns>
	SpannerConnection CreateConnection();

	/// <summary>
	/// Opens a connection and runs <paramref name="operation"/>, replaying it with exponential backoff when
	/// Spanner reports the transaction as retryable, up to the configured abort-retry limit.
	/// </summary>
	/// <typeparam name="T">The operation result type.</typeparam>
	/// <param name="operation">The work to run against an open connection.</param>
	/// <param name="cancellationToken">A token to cancel the operation and any pending retry backoff.</param>
	/// <returns>The result produced by <paramref name="operation"/>.</returns>
	Task<T> ExecuteInRetryableTransactionAsync<T>(
		Func<SpannerConnection, CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken);
}
