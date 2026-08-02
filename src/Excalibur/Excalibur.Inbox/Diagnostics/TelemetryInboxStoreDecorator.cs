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
internal sealed class TelemetryInboxStoreDecorator : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IBackoffSchedulableInboxStore, IInboxStoreCapabilities, IInboxStoreAdmin, ITransactionalInboxStore, IScopedTransactionalInboxStore, IDisposable
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
	/// <para>
	/// Reports the EFFECTIVE transactional handler+mark capability across BOTH transactional seams — the
	/// relational <see cref="ITransactionalInboxStore"/> and the document-store
	/// <see cref="IScopedTransactionalInboxStore"/> — and composes through chains (see
	/// <see cref="SupportsClaim"/>). A store capable of either seam is transactional-capable, because this
	/// decorator can forward the scoped seam over either one.
	/// </para>
	/// <para>
	/// An inner store that reports its own effective capability is AUTHORITATIVE and takes precedence over
	/// the static interface test: a store may implement a transactional seam yet be configured such that it
	/// cannot honour the atomic contract (for example a document store lacking the shared partition key its
	/// batch requires), and it reports <see langword="false"/> for exactly that case. Trusting the interface
	/// test over that report would re-advertise an atomicity guarantee the store has disclaimed.
	/// </para>
	/// </remarks>
	public bool SupportsTransactional =>
		_inner is IInboxStoreCapabilities capabilities
			? capabilities.SupportsTransactional
			: _inner is ITransactionalInboxStore or IScopedTransactionalInboxStore;

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

	/// <inheritdoc cref="IScopedTransactionalInboxStore.TryProcessTransactionallyAsync" />
	/// <remarks>
	/// <para>
	/// Forwards the scoped exactly-once seam — the highest-precedence atomic path, selected by a type test on
	/// the OUTERMOST store instance. A decorator that omitted this member would make the seam invisible
	/// through decoration and silently downgrade a document store's atomicity to the at-least-once claim
	/// protocol, so it is forwarded here rather than left to the inner store's static type.
	/// </para>
	/// <para>
	/// Two forwarding routes, both preserving the atomic contract. An inner store implementing the scoped
	/// seam is forwarded directly. An inner store implementing only the relational
	/// <see cref="ITransactionalInboxStore"/> is bridged onto it by wrapping the active transaction in
	/// <see cref="SqlInboxTransactionScope"/> — the same adaptation the relational providers apply to expose
	/// this seam, so the handler still enlists its writes atomically with the processed-mark.
	/// </para>
	/// </remarks>
	public async ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		var start = Stopwatch.GetTimestamp();

		try
		{
			if (_inner is IScopedTransactionalInboxStore scoped)
			{
				return await scoped.TryProcessTransactionallyAsync(messageId, handlerType, handler, cancellationToken).ConfigureAwait(false);
			}

			// Fail LOUD (never a silent fallback) if the inner store can enlist neither seam — a silent
			// downgrade of exactly-once to at-least-once is the defect this forward exists to prevent. The
			// SupportsTransactional presence-guard makes this unreachable for a validated configuration.
			if (_inner is not ITransactionalInboxStore relational)
			{
				throw new NotSupportedException(
					$"The decorated inbox store '{_inner.GetType().FullName}' implements neither IScopedTransactionalInboxStore " +
					"nor ITransactionalInboxStore; scoped transactional handler+mark cannot be forwarded through the telemetry decorator.");
			}

			return await relational.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				(transaction, ct) => handler(new SqlInboxTransactionScope(transaction), ct),
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			RecordOperation("try_process_transactionally_scoped", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
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
