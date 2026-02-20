// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// A no-op implementation of <see cref="IDeadLetterQueue"/> that silently discards all operations.
/// </summary>
/// <remarks>
/// This implementation follows the Null Object pattern to provide a safe default when
/// dead letter queue functionality is not configured or not needed. All operations
/// complete successfully without side effects.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
	Justification = "Represents a dead letter queue implementation.")]
public sealed class NullDeadLetterQueue : IDeadLetterQueue
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
	public Task<int> ReplayBatchAsync(DeadLetterQueryFilter filter, CancellationToken cancellationToken) =>
		Task.FromResult(0);

	/// <inheritdoc />
	public Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken) =>
		Task.FromResult(false);

	/// <inheritdoc />
	public Task<int> PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken) =>
		Task.FromResult(0);

	/// <inheritdoc />
	public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
		Task.FromResult(0L);
}
