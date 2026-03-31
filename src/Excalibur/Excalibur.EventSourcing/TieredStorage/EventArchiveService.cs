// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

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
/// The service evaluates the <see cref="ArchivePolicy"/> on each cycle to identify
/// aggregates with archivable events. For each candidate:
/// </para>
/// <list type="number">
/// <item>Load archivable events from the hot store via <see cref="IEventStore"/></item>
/// <item>Write them to cold storage via <see cref="IColdEventStore"/></item>
/// <item>Delete from hot store via <see cref="IEventStoreArchive"/></item>
/// </list>
/// <para>
/// Archival is best-effort per aggregate: a failure archiving one aggregate
/// does not block others. Failed aggregates will be retried on the next cycle.
/// </para>
/// </remarks>
internal sealed class EventArchiveService : BackgroundService
{
	private const int DefaultBatchSize = 100;

	private readonly IEventStoreArchive _archiveSource;
	private readonly IEventStore _hotStore;
	private readonly IColdEventStore _coldStore;
	private readonly IOptionsMonitor<ArchivePolicy> _policyMonitor;
	private readonly IOptionsMonitor<EventArchiveServiceOptions> _optionsMonitor;
	private readonly ILogger<EventArchiveService> _logger;

	internal EventArchiveService(
		IEventStoreArchive archiveSource,
		IEventStore hotStore,
		IColdEventStore coldStore,
		IOptionsMonitor<ArchivePolicy> policyMonitor,
		IOptionsMonitor<EventArchiveServiceOptions> optionsMonitor,
		ILogger<EventArchiveService> logger)
	{
		ArgumentNullException.ThrowIfNull(archiveSource);
		ArgumentNullException.ThrowIfNull(hotStore);
		ArgumentNullException.ThrowIfNull(coldStore);
		ArgumentNullException.ThrowIfNull(policyMonitor);
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_archiveSource = archiveSource;
		_hotStore = hotStore;
		_coldStore = coldStore;
		_policyMonitor = policyMonitor;
		_optionsMonitor = optionsMonitor;
		_logger = logger;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.ArchiveServiceStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			var options = _optionsMonitor.CurrentValue;
			var interval = options.ArchiveInterval;

			try
			{
				await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				await RunArchiveCycleAsync(stoppingToken).ConfigureAwait(false);
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

		if (policy.MaxAge is null && policy.MaxPosition is null && policy.RetainRecentCount is null)
		{
			_logger.NoCriteriaConfigured();
			return;
		}

		var options = _optionsMonitor.CurrentValue;
		var batchSize = options.BatchSize > 0 ? options.BatchSize : DefaultBatchSize;

		var candidates = await _archiveSource.GetArchiveCandidatesAsync(
			policy, batchSize, cancellationToken).ConfigureAwait(false);

		if (candidates.Count == 0)
		{
			_logger.NoCandidatesFound();
			return;
		}

		_logger.CandidatesFound(candidates.Count);

		var archivedCount = 0;
		var failedCount = 0;

		foreach (var candidate in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await ArchiveAggregateAsync(candidate, cancellationToken).ConfigureAwait(false);
				archivedCount++;
			}
#pragma warning disable CA1031 // Best-effort per aggregate
			catch (Exception ex)
#pragma warning restore CA1031
			{
				failedCount++;
				_logger.ArchiveAggregateFailed(candidate.AggregateId, ex.Message);
			}
		}

		_logger.ArchiveCycleComplete(archivedCount, candidates.Count, 0);
	}

	private async Task ArchiveAggregateAsync(
		ArchiveCandidate candidate,
		CancellationToken cancellationToken)
	{
		// 1. Load all events from hot store and filter to archivable range
		var allEvents = await _hotStore.LoadAsync(
			candidate.AggregateId,
			candidate.AggregateType,
			cancellationToken).ConfigureAwait(false);

		// Filter to events up to the archivable version
		var events = allEvents
			.Where(e => e.Version <= candidate.ArchivableUpToVersion)
			.ToList();

		if (events.Count == 0)
		{
			return;
		}

		// 2. Write to cold storage
		await _coldStore.WriteAsync(candidate.AggregateId, events, cancellationToken)
			.ConfigureAwait(false);

		// 3. Delete from hot store (only after cold write succeeds)
		var deleted = await _archiveSource.DeleteEventsUpToVersionAsync(
			candidate.AggregateId,
			candidate.AggregateType,
			candidate.ArchivableUpToVersion,
			cancellationToken).ConfigureAwait(false);

		_logger.ArchivingAggregate(candidate.AggregateId, events.Count, events[0].Version, candidate.ArchivableUpToVersion);
	}
}
