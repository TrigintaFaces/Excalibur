// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Background service that periodically archives old events from the hot store
/// to cold storage based on the configured <see cref="ArchivePolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// The service captures the <see cref="ArchivePolicy"/> for each scan round to identify
/// aggregates with archivable events. For each candidate:
/// </para>
/// <list type="number">
/// <item>Load archivable events from the hot store via <see cref="IEventStoreArchiveReader"/></item>
/// <item>Write them to cold storage via <see cref="IColdEventStore"/></item>
/// <item>Delete from hot store via <see cref="IEventStoreArchive"/></item>
/// </list>
/// <para>
/// Archival is best-effort per aggregate: a failure archiving one aggregate
/// does not block others. Failed aggregates are retried on the next scan round; individual candidate failures do not monopolize later cycles.
/// </para>
/// </remarks>
internal sealed class EventArchiveService : BackgroundService
{
	private const int DefaultBatchSize = 100;

	private readonly IEventStoreArchive _archiveSource;
	private readonly IEventStoreArchiveReader _archiveReader;
	private readonly IEventStoreArchiveScanner _scanner;
	private ArchiveScanCursor? _continuation;
	private readonly IColdEventStore _coldStore;
	private readonly IOptionsMonitor<ArchivePolicy> _policyMonitor;
	private readonly IOptionsMonitor<EventArchiveServiceOptions> _optionsMonitor;
	private readonly ILogger<EventArchiveService> _logger;

	/// <summary>
	/// DI key under which tiered storage registers the RAW hot event store, separate from the keyed
	/// "default" that is re-bound to the read-through <c>TieredEventStoreDecorator</c>. The archive
	/// service MUST enumerate and trim only the hot tier — resolving the decorated "default" would read
	/// through to cold during trim (re-reading already-archived events), so it binds the raw hot here.
	/// </summary>
	internal const string RawHotEventStoreKey = "tiered-hot";

	internal EventArchiveService(
		IEventStoreArchive archiveSource,
		IEventStoreArchiveReader archiveReader,
		IEventStoreArchiveScanner scanner,
		IColdEventStore coldStore,
		IOptionsMonitor<ArchivePolicy> policyMonitor,
		IOptionsMonitor<EventArchiveServiceOptions> optionsMonitor,
		ILogger<EventArchiveService> logger)
	{
		ArgumentNullException.ThrowIfNull(archiveSource);
		ArgumentNullException.ThrowIfNull(archiveReader);
		ArgumentNullException.ThrowIfNull(scanner);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(policyMonitor);
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_archiveSource = archiveSource;
		_archiveReader = archiveReader;
		_scanner = scanner;
		_coldStore = coldStore;
		_policyMonitor = policyMonitor;
		_optionsMonitor = optionsMonitor;
		_logger = logger;
	}

	internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.ArchiveServiceStarted();
		var continueRound = false;

		while (!stoppingToken.IsCancellationRequested)
		{
			var options = _optionsMonitor.CurrentValue;
			var interval = options.ArchiveInterval;

			try
			{
				if (!continueRound)
				{
					await Task.Delay(interval, TimeProvider, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				continueRound = false;
				await RunArchiveCycleAsync(stoppingToken).ConfigureAwait(false);
				continueRound = _continuation is not null;
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
#pragma warning disable CA1031 // Best-effort: cycle failures must not crash the service
			catch (Exception ex)
#pragma warning restore CA1031
			{
				_logger.ArchiveCycleFailed(ex);
			}
		}

		_logger.ArchiveServiceStopped();
	}

	private async Task RunArchiveCycleAsync(CancellationToken cancellationToken)
	{
		var policy = _policyMonitor.CurrentValue;

		if (_continuation is null && policy.MaxAge is null && policy.MaxPosition is null && policy.RetainRecentCount is null)
		{
			_logger.NoCriteriaConfigured();
			return;
		}

		var options = _optionsMonitor.CurrentValue;
		var batchSize = options.BatchSize > 0 ? options.BatchSize : DefaultBatchSize;

		var page = await _scanner.ScanArchiveCandidatesAsync(
			policy, batchSize, _continuation, cancellationToken).ConfigureAwait(false);
		var candidates = page.Candidates;

		if (candidates.Count == 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_continuation = page.Continuation;
			_logger.NoCandidatesFound();
			return;
		}

		_logger.CandidatesFound(candidates.Count);

		var archivedCount = 0;
		var archivedEvents = 0;

		foreach (var candidate in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				var moved = await ArchiveAggregateAsync(candidate, cancellationToken).ConfigureAwait(false);
				if (moved > 0)
				{
					archivedCount++;
					archivedEvents += moved;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // Best-effort per aggregate
			catch (Exception ex)
#pragma warning restore CA1031
			{
				_logger.ArchiveAggregateFailed(candidate.AggregateId, ex.Message);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		// Accept the continuation only after every candidate was attempted. Cancellation replays the page.
		_continuation = page.Continuation;
		_logger.ArchiveCycleComplete(archivedCount, candidates.Count, archivedEvents);
	}

	private async Task<int> ArchiveAggregateAsync(
		ArchiveCandidate candidate,
		CancellationToken cancellationToken)
	{
		// 1. Load all events from hot store and filter to archivable range
		var allEvents = await _archiveReader.LoadArchiveEventsAsync(
			candidate.Tenant,
			candidate.AggregateId,
			candidate.AggregateType,
			candidate.ArchivableUpToVersion,
			cancellationToken).ConfigureAwait(false);

		// Filter to events up to the archivable version
		if (allEvents.Any(e => e.Version <= candidate.ArchivableUpToVersion && ErasedEventMarker.IsErased(e.EventType)))
		{
			throw new InvalidOperationException("Archival cannot process erased events without cold-tier erasure support.");
		}

		var events = allEvents
			.Where(e => e.Version <= candidate.ArchivableUpToVersion)
			.Where(e => e.EventData is not null || e.ArchivedAt is null)
			.ToList();

		// Archived markers remain in hot history. Their bytes are already owned by cold;
		// submitting those nulls again would either overwrite payloads or stall every later cycle.
		// Other missing payloads are unresolved and cannot earn a durable archive receipt.
		foreach (var storedEvent in events)
		{
			_ = StoredEventPayload.Require(storedEvent);
		}

		if (events.Count == 0)
		{
			return 0;
		}

		// 2. Write to cold storage. The returned watermark is the highest version durably committed in cold
		//    (a contiguous prefix), which bounds how far we may delete from hot — never trust the submitted
		//    max, only the durable ack the cold store confirms.
		// The tenant term is read ONCE from the candidate and flows into both the cold write and the hot
		// delete below. There is no second source for it, so the two operations cannot address different
		// tenants: whatever was archived under this tenant is the only thing this run can delete.
		var tenant = candidate.Tenant;

		var durableWatermark = await _coldStore.WriteAsync(tenant, candidate.AggregateId, candidate.AggregateType, events, cancellationToken)
			.ConfigureAwait(false);

		// 3. Delete from hot store only up to the CONFIRMED durable watermark (and never past the archivable
		//    ceiling). If cold durably took only a prefix (partial/deferred write), hot deletion is bounded to
		//    that prefix, so deleted-from-hot is always a subset of durable-in-cold (no data loss).
		var deleteUpToVersion = Math.Min(candidate.ArchivableUpToVersion, durableWatermark);
		if (deleteUpToVersion < events[0].Version)
		{
			// Nothing was durably archived at or above the first candidate version — do not delete anything.
			return 0;
		}

		var deleted = await _archiveSource.TombstoneArchivedEventsUpToVersionAsync(
			tenant,
			candidate.AggregateId,
			candidate.AggregateType,
			deleteUpToVersion,
			cancellationToken).ConfigureAwait(false);

		// Report what the hot delete actually removed, not what was submitted. A zero-row delete leaves the
		// events in both tiers: correct on read, but the archive moved nothing and must not read as success.
		if (deleted == 0)
		{
			_logger.ArchiveHotDeleteRemovedNothing(candidate.AggregateId, events.Count, deleteUpToVersion);
			return 0;
		}

		_logger.ArchivingAggregate(candidate.AggregateId, deleted, events[0].Version, deleteUpToVersion);
		return deleted;
	}
}
