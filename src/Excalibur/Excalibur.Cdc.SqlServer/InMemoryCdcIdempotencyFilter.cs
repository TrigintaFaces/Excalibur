// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// In-memory implementation of <see cref="ICdcIdempotencyFilter"/> using a bounded
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> with a capacity of 10,000 entries.
/// </summary>
/// <remarks>
/// <para>
/// Suitable for single-instance deployments where CDC events are processed by one consumer.
/// The filter uses the CDC-native <c>(tableName, LSN, seqVal)</c> composite key to track
/// processed events.
/// </para>
/// <para>
/// When the capacity limit is reached, new events are not tracked (skip-when-full pattern,
/// same as <c>InMemoryDeduplicator</c>). This ensures bounded memory usage without blocking
/// event processing.
/// </para>
/// <para>
/// This filter does not survive process restarts — it is purely in-memory. For durable
/// idempotency across restarts, use a persistent implementation (e.g., SQL Server-backed).
/// </para>
/// </remarks>
internal sealed partial class InMemoryCdcIdempotencyFilter : ICdcIdempotencyFilter
{
	/// <summary>
	/// Maximum number of tracked events. When reached, new events are processed
	/// without idempotency tracking (skip-when-full pattern).
	/// </summary>
	internal const int DefaultCapacity = 10_000;

	private readonly ConcurrentDictionary<string, DateTimeOffset> _processedEvents = new(StringComparer.Ordinal);
	private readonly int _capacity;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcIdempotencyFilter"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public InMemoryCdcIdempotencyFilter(ILogger<InMemoryCdcIdempotencyFilter> logger)
		: this(DefaultCapacity, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryCdcIdempotencyFilter"/> class
	/// with a custom capacity.
	/// </summary>
	/// <param name="capacity">The maximum number of tracked events.</param>
	/// <param name="logger">The logger instance.</param>
	internal InMemoryCdcIdempotencyFilter(int capacity, ILogger<InMemoryCdcIdempotencyFilter> logger)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		_capacity = capacity;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<bool> IsProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		var key = BuildKey(tableName, lsn, seqVal);
		var isProcessed = _processedEvents.ContainsKey(key);

		if (isProcessed)
		{
			LogDuplicateEventSkipped(tableName, CdcChangeDetector.ByteArrayToHex(lsn), CdcChangeDetector.ByteArrayToHex(seqVal));
		}

		return Task.FromResult(isProcessed);
	}

	/// <inheritdoc />
	public Task MarkProcessedAsync(string tableName, byte[] lsn, byte[] seqVal, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		if (_processedEvents.Count >= _capacity)
		{
			LogCapacityReached(_capacity);
			return Task.CompletedTask;
		}

		var key = BuildKey(tableName, lsn, seqVal);
		_ = _processedEvents.TryAdd(key, DateTimeOffset.UtcNow);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the current number of tracked events.
	/// </summary>
	internal int Count => _processedEvents.Count;

	/// <summary>
	/// Builds a composite key from the CDC event identity components.
	/// </summary>
	/// <remarks>
	/// Uses hex encoding of LSN and seqVal to avoid byte[] equality issues.
	/// Format: <c>{tableName}:{hexLsn}:{hexSeqVal}</c>.
	/// </remarks>
	private static string BuildKey(string tableName, byte[] lsn, byte[] seqVal)
		=> string.Create(
			tableName.Length + 1 + (lsn.Length * 2) + 1 + (seqVal.Length * 2),
			(tableName, lsn, seqVal),
			static (span, state) =>
			{
				state.tableName.AsSpan().CopyTo(span);
				var pos = state.tableName.Length;
				span[pos++] = ':';

				var lsnHex = Convert.ToHexString(state.lsn);
				lsnHex.AsSpan().CopyTo(span[pos..]);
				pos += lsnHex.Length;

				span[pos++] = ':';

				var seqHex = Convert.ToHexString(state.seqVal);
				seqHex.AsSpan().CopyTo(span[pos..]);
			});

	[LoggerMessage(Excalibur.Data.SqlServer.Diagnostics.DataSqlServerEventId.CdcIdempotencyDuplicateSkipped, LogLevel.Debug,
		"Duplicate CDC event skipped: table={TableName}, LSN={Lsn}, SeqVal={SeqVal}")]
	private partial void LogDuplicateEventSkipped(string tableName, string lsn, string seqVal);

	[LoggerMessage(Excalibur.Data.SqlServer.Diagnostics.DataSqlServerEventId.CdcIdempotencyCapacityReached, LogLevel.Warning,
		"CDC idempotency filter capacity reached ({Capacity}). New events will not be tracked for deduplication.")]
	private partial void LogCapacityReached(int capacity);
}
