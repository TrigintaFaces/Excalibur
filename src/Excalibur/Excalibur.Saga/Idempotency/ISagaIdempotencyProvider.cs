// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Idempotency;

/// <summary>
/// Tracks processed message idempotency keys to prevent duplicate processing
/// in saga message handling.
/// </summary>
/// <remarks>
/// <para>
/// Saga handlers may receive the same message more than once due to at-least-once
/// delivery semantics in distributed systems. This interface provides a mechanism
/// to detect and skip duplicate messages by tracking unique idempotency keys.
/// </para>
/// <para>
/// Implementations should be durable (e.g., backed by a database) for production use.
/// In-memory implementations are suitable only for testing and development.
/// </para>
/// </remarks>
public interface ISagaIdempotencyProvider
{
	/// <summary>
	/// Checks whether a message with the specified idempotency key has already been processed.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="idempotencyKey">The unique idempotency key for the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the key has been previously marked as processed;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	Task<bool> IsProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message idempotency key as processed for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="idempotencyKey">The unique idempotency key for the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This operation should be idempotent. Marking an already-processed key
	/// should succeed without error.
	/// </remarks>
	Task MarkProcessedAsync(string sagaId, string idempotencyKey, CancellationToken cancellationToken);
}
