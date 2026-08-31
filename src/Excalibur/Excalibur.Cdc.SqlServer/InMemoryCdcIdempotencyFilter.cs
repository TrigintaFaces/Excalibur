// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Data.SqlServer.Diagnostics;

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

	/// <summary>
	/// Counter incremented whenever the filter reaches capacity and deduplication degrades — either a
	/// not-yet-seen event fails closed in <see cref="IsProcessedAsync"/>, or a processed event cannot be
	/// tracked in <see cref="MarkProcessedAsync"/>. The canonical, alertable cross-module degradation signal.
	/// </summary>
	private static readonly Counter<long> CapacityExceededCounter = CdcTelemetryConstants.Meter.CreateCounter<long>(
		CdcTelemetryConstants.MetricNames.IdempotencyCapacityExceeded,
		"{event}",
		"Count of times the in-memory CDC idempotency filter reached capacity and deduplication degraded.");

	private readonly ConcurrentDictionary<CdcEventKey, DateTimeOffset> _processedEvents = new();
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
	public Task<bool> IsProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		string consumerId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		var key = BuildKey(tableName, lsn, seqVal, consumerId);
		var isProcessed = _processedEvents.ContainsKey(key);

		if (isProcessed)
		{
			LogDuplicateEventSkipped(tableName, CdcChangeDetector.ByteArrayToHex(lsn), CdcChangeDetector.ByteArrayToHex(seqVal));
			return Task.FromResult(true);
		}

		// Fail-closed pre-process gate: a not-yet-seen event at capacity cannot be tracked, so we cannot
		// guarantee it will be deduplicated on a later redelivery. Throwing here (BEFORE the handler runs)
		// causes the batch to be redelivered rather than processed un-tracked — the only safe point to fail
		// closed. (MarkProcessedAsync, which runs AFTER the handler, must never throw — that would double-
		// process an event whose side effects already happened.)
		if (_processedEvents.Count >= _capacity)
		{
			CapacityExceededCounter.Add(1);
			LogCapacityReached(_capacity);
			throw new CdcIdempotencyCapacityExceededException(_capacity);
		}

		return Task.FromResult(false);
	}

	/// <inheritdoc />
	public Task MarkProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		string consumerId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		// Best-effort by contract: MarkProcessedAsync runs AFTER the handler has already executed, so it must
		// never throw (that would re-run a completed event on redelivery). At capacity we skip tracking and
		// emit the degradation signal; the pre-process gate in IsProcessedAsync is the fail-closed point. In
		// practice a new key rarely reaches here at saturation because that gate already failed it closed.
		if (_processedEvents.Count >= _capacity)
		{
			CapacityExceededCounter.Add(1);
			LogCapacityReached(_capacity);
			return Task.CompletedTask;
		}

		var key = BuildKey(tableName, lsn, seqVal, consumerId);
		_ = _processedEvents.TryAdd(key, DateTimeOffset.UtcNow);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets the current number of tracked events.
	/// </summary>
	internal int Count => _processedEvents.Count;

	/// <summary>
	/// Builds the composite key identifying one CDC event for one consumer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The terms were previously joined as <c>{consumerId}:{tableName}:{hexLsn}:{hexSeqVal}</c>. That join
	/// is not injective. The LSN and sequence terms are hex and cannot contain a colon, but the consumer id
	/// and table name are both unvalidated caller strings, so consumer <c>"a:b"</c> on table <c>"c"</c> and
	/// consumer <c>"a"</c> on table <c>"b:c"</c> rendered the same key and shared one entry. This is a
	/// deduplication filter, so the collision does not throw: one consumer's processed marker suppresses
	/// the other consumer's genuinely new event, which is then skipped and never processed.
	/// </para>
	/// <para>
	/// A tuple removes the join, so no term can cross a delimiter and injectivity holds for every string
	/// input by construction. The LSN and sequence bytes are still hex-encoded, because a <c>byte[]</c>
	/// compares by reference and two equal LSNs in different arrays would otherwise miss.
	/// </para>
	/// <para>
	/// The key is in-process only and is never persisted, so no stored state is keyed by the old shape.
	/// </para>
	/// </remarks>
	private static CdcEventKey BuildKey(string tableName, byte[] lsn, byte[] seqVal, string consumerId)
		=> new(consumerId, tableName, Convert.ToHexString(lsn), Convert.ToHexString(seqVal));

	/// <summary>
	/// The four terms that together identify one CDC event for one consumer.
	/// </summary>
	private readonly record struct CdcEventKey(string ConsumerId, string TableName, string LsnHex, string SeqValHex);

	[LoggerMessage(Excalibur.Data.SqlServer.Diagnostics.DataSqlServerEventId.CdcIdempotencyDuplicateSkipped, LogLevel.Debug,
		"Duplicate CDC event skipped: table={TableName}, LSN={Lsn}, SeqVal={SeqVal}")]
	private partial void LogDuplicateEventSkipped(string tableName, string lsn, string seqVal);

	[LoggerMessage(Excalibur.Data.SqlServer.Diagnostics.DataSqlServerEventId.CdcIdempotencyCapacityReached, LogLevel.Warning,
		"CDC idempotency filter capacity reached ({Capacity}). New events will not be tracked for deduplication.")]
	private partial void LogCapacityReached(int capacity);
}
