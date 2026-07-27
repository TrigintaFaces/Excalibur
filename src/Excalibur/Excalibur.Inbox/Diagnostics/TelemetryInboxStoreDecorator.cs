// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch;

namespace Excalibur.Inbox.Diagnostics;

/// <summary>
/// Telemetry decorator for <see cref="IInboxStore"/> that instruments operations
/// with counters and histograms.
/// </summary>
internal sealed class TelemetryInboxStoreDecorator : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IBackoffSchedulableInboxStore, IInboxStoreCapabilities, IInboxStoreAdmin, ITransactionalInboxStore, IDisposable
{
	/// <summary>
	/// The meter name for inbox store telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Inbox";

	private readonly IInboxStore _inner;
	private readonly Meter _meter;
	private readonly Counter<long> _operationsCounter;
	private readonly Histogram<double> _operationDuration;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryInboxStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner inbox store to decorate.</param>
	/// <param name="meterFactory">The meter factory for creating instruments.</param>
	public TelemetryInboxStoreDecorator(IInboxStore inner, IMeterFactory? meterFactory = null)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

		_operationsCounter = _meter.CreateCounter<long>(
			"excalibur.inbox.operations",
			description: "Number of inbox store operations.");

		_operationDuration = _meter.CreateHistogram<double>(
			"excalibur.inbox.operation_duration",
			unit: "ms",
			description: "Duration of inbox store operations in milliseconds.");
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Reports the EFFECTIVE atomic-claim capability and composes through chains: telemetry can forward a claim
	/// only when its inner store is itself claim-capable (directly via <see cref="IClaimableInboxStore"/> or
	/// transitively via a nested <see cref="IInboxStoreCapabilities"/>), so the startup presence-guard rejects a
	/// telemetry-over-non-claimable-inner instead of throwing at first claim.
	/// </remarks>
	public bool SupportsClaim =>
		_inner is IClaimableInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsClaim);

	/// <inheritdoc/>
	/// <remarks>
	/// Reports the EFFECTIVE durable Processing-tracking capability and composes through chains (see
	/// <see cref="SupportsClaim"/>).
	/// </remarks>
	public bool SupportsProcessingTracking =>
		_inner is IProcessingTrackingInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsProcessingTracking);

	/// <inheritdoc/>
	/// <remarks>
	/// Reports the EFFECTIVE transactional handler+mark capability and composes through chains (see
	/// <see cref="SupportsClaim"/>).
	/// </remarks>
	public bool SupportsTransactional =>
		_inner is ITransactionalInboxStore || (_inner is IInboxStoreCapabilities capabilities && capabilities.SupportsTransactional);

	/// <inheritdoc/>
	public async ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<System.Data.IDbTransaction, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		// Forward the transactional handler+mark to the inner store. Fail LOUD (never a silent no-op) if the
		// inner store cannot enlist a transaction — a silent fallback would downgrade exactly-once to
		// at-least-once undetected. The SupportsTransactional presence-guard makes this path unreachable at
		// runtime for a correctly-validated configuration.
		if (_inner is not ITransactionalInboxStore transactional)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement ITransactionalInboxStore; " +
				"transactional handler+mark cannot be forwarded through the telemetry decorator.");
		}

		var start = Stopwatch.GetTimestamp();

		try
		{
			return await transactional.TryProcessTransactionallyAsync(messageId, handlerType, handler, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_process_transactionally", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("create_entry", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			await _inner.MarkProcessedAsync(messageId, handlerType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("mark_processed", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		// Forward the Processing-tracking capability to the inner store. Fail LOUD (never a silent no-op) if
		// the inner store cannot persist Processing — a silent skip would re-create the at-most-once silent-degrade.
		if (_inner is not IProcessingTrackingInboxStore tracker)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IProcessingTrackingInboxStore; " +
				"durable Processing tracking cannot be forwarded through the telemetry decorator.");
		}

		var start = Stopwatch.GetTimestamp();

		try
		{
			await tracker.MarkProcessingAsync(messageId, handlerType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("mark_processing", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.TryMarkAsProcessedAsync(messageId, handlerType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_mark_processed", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, TimeSpan leaseDuration, CancellationToken cancellationToken)
	{
		// Forward the lease-based claim to the inner store. Fail LOUD (never a silent no-op) if the inner store
		// cannot claim atomically — a silent fallback would re-create the check-then-act race.
		if (_inner is not IClaimableInboxStore claimable)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IClaimableInboxStore; " +
				"lease-based claiming cannot be forwarded through the telemetry decorator.");
		}

		var start = Stopwatch.GetTimestamp();

		try
		{
			return await claimable.TryClaimAsync(messageId, handlerType, leaseDuration, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_claim_lease", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		// Forward the atomic-claim capability to the inner store. Fail LOUD (never a silent no-op) if the inner
		// store cannot claim atomically — a silent fallback would re-create the check-then-act race.
		if (_inner is not IClaimableInboxStore claimable)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IClaimableInboxStore; " +
				"atomic claiming cannot be forwarded through the telemetry decorator.");
		}

		var start = Stopwatch.GetTimestamp();

		try
		{
			return await claimable.TryClaimAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_claim", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		if (_inner is not IClaimableInboxStore claimable)
		{
			throw new NotSupportedException(
				$"The decorated inbox store '{_inner.GetType().FullName}' does not implement IClaimableInboxStore; " +
				"claim release cannot be forwarded through the telemetry decorator.");
		}

		var start = Stopwatch.GetTimestamp();

		try
		{
			await claimable.ReleaseAsync(messageId, handlerType, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("release_claim", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.IsProcessedAsync(messageId, handlerType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("is_processed", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await _inner.GetEntryAsync(messageId, handlerType, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("get_entry", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			await _inner.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("mark_failed", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			// Backoff is an optional optimization (fail-open): forward to the inner store if it supports the
			// schedule, otherwise fall back to the plain failed status so the decorator never regresses behavior.
			if (_inner is IBackoffSchedulableInboxStore schedulable)
			{
				await schedulable.MarkFailedWithBackoffAsync(messageId, handlerType, errorMessage, retryCount, nextAttemptAt, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				await _inner.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken)
					.ConfigureAwait(false);
			}
		}
		finally
		{
			RecordOperation("mark_failed_with_backoff", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Admin (bulk query / retry-processor) surface. Forwarded to the inner store cast to
	/// <see cref="IInboxStoreAdmin"/> — a telemetry-decorated store must expose the admin capability so the
	/// retry processor (which casts the store to <see cref="IInboxStoreAdmin"/>) works through the decorator;
	/// an inner store that is not admin-capable is a genuine misconfiguration and surfaces as an
	/// <see cref="InvalidCastException"/> rather than being silently dropped.
	/// </remarks>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await ((IInboxStoreAdmin)_inner)
				.GetFailedEntriesAsync(maxRetries, olderThan, batchSize, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("get_failed_entries", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			await ((IInboxStoreAdmin)_inner)
				.MarkFailedAsync(messageId, handlerType, errorMessage, retryCount, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("mark_failed_admin", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await ((IInboxStoreAdmin)_inner).GetAllEntriesAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("get_all_entries", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await ((IInboxStoreAdmin)_inner).GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("get_statistics", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		var start = Stopwatch.GetTimestamp();

		try
		{
			return await ((IInboxStoreAdmin)_inner).CleanupAsync(olderThan, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("cleanup", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_meter.Dispose();
	}

	private void RecordOperation(string operation, double durationMs)
	{
		var tags = new TagList { { "operation", operation } };
		_operationsCounter.Add(1, tags);
		_operationDuration.Record(durationMs, tags);
	}
}
