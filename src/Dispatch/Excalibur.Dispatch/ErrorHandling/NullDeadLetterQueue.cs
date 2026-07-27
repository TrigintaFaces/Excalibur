// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// A no-op implementation of <see cref="IDeadLetterQueue"/> that discards every message handed to it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is not a default.</strong> Nothing registers it on your behalf; a host that enables
/// dead-letter routing without a store is refused at composition rather than given this type. Register it
/// explicitly — <c>services.AddSingleton&lt;IDeadLetterQueue&gt;(NullDeadLetterQueue.Instance)</c> — to state
/// that exhausted messages should be dropped.
/// </para>
/// <para>
/// Every caller in the framework checks for this type and reports a discard rather than a dead-letter
/// routing, so <see cref="EnqueueAsync"/> is never invoked on it and its <see cref="Guid.Empty"/> return is
/// never handed to a caller as an entry id. Preserve that discipline in your own code: an entry id from this
/// type names no entry, and a log line claiming a message was dead-lettered through it would be false.
/// </para>
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
	Justification = "Represents a dead letter queue implementation.")]
public sealed class NullDeadLetterQueue : IDeadLetterQueue, IDeadLetterQueueAdmin
{
	private NullDeadLetterQueue()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the null dead letter queue.
	/// </summary>
	public static NullDeadLetterQueue Instance { get; } = new();

	/// <inheritdoc />
	public Task<Guid> EnqueueAsync<T>(
		T message,
		DeadLetterReason reason,
		CancellationToken cancellationToken,
		Exception? exception = null,
		IDictionary<string, string>? metadata = null) =>
		Task.FromResult(Guid.Empty);

	/// <inheritdoc />
	public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
		CancellationToken cancellationToken,
		DeadLetterQueryFilter? filter = null,
		int limit = 100) =>
		Task.FromResult<IReadOnlyList<DeadLetterEntry>>(Array.Empty<DeadLetterEntry>());

	/// <inheritdoc />
	public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
		Task.FromResult<DeadLetterEntry?>(null);

	/// <inheritdoc />
	public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken) =>
		Task.FromResult(false);

	/// <inheritdoc />
	public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
		Task.FromResult(0L);

	/// <inheritdoc />
	Task<ReplayBatchResult> IDeadLetterQueueAdmin.ReplayBatchAsync(
		DeadLetterQueryFilter filter,
		int limit,
		CancellationToken cancellationToken) =>
		// Nothing is stored, so nothing was enumerated and nothing was cut short: Truncated is false because
		// the queue is genuinely drained, not because the question was skipped.
		Task.FromResult(new ReplayBatchResult(Enumerated: 0, Replayed: 0, Truncated: false));

	/// <inheritdoc />
	Task<bool> IDeadLetterQueueAdmin.PurgeAsync(Guid entryId, CancellationToken cancellationToken) =>
		Task.FromResult(false);

	/// <inheritdoc />
	Task<int> IDeadLetterQueueAdmin.PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken) =>
		Task.FromResult(0);
}
