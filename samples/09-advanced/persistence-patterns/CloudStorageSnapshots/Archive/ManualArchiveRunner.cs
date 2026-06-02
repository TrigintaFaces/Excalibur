// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudStorageSnapshots.Archive;

/// <summary>
/// On-demand archival runner used by the sample so the hot→cold boundary is
/// exercisable without waiting for the <c>EventArchiveService</c> background
/// cycle.
/// </summary>
/// <remarks>
/// <para>
/// Production systems typically rely on <c>EventArchiveService</c> running in
/// the background with its configured <see cref="ArchivePolicy"/>. This runner
/// uses the same primitives (<see cref="IEventStoreArchive"/>,
/// <see cref="IEventStore"/>, <see cref="IColdEventStore"/>) but lets a caller
/// force a single cycle so the archival behaviour can be observed immediately.
/// </para>
/// </remarks>
public sealed class ManualArchiveRunner
{
	private readonly IEventStoreArchive _archiveSource;
	private readonly IEventStore _hotStore;
	private readonly IColdEventStore _coldStore;
	private readonly IOptionsMonitor<ArchivePolicy> _policyMonitor;
	private readonly ILogger<ManualArchiveRunner> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ManualArchiveRunner"/> class.
	/// </summary>
	public ManualArchiveRunner(
		IEventStoreArchive archiveSource,
		IEventStore hotStore,
		IColdEventStore coldStore,
		IOptionsMonitor<ArchivePolicy> policyMonitor,
		ILogger<ManualArchiveRunner> logger)
	{
		_archiveSource = archiveSource;
		_hotStore = hotStore;
		_coldStore = coldStore;
		_policyMonitor = policyMonitor;
		_logger = logger;
	}

	/// <summary>
	/// Runs one archive cycle using the currently-configured <see cref="ArchivePolicy"/>.
	/// </summary>
	/// <param name="batchSize">Maximum number of candidate aggregates to process.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A summary of the work performed.</returns>
	public async Task<ArchiveCycleSummary> RunAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var policy = _policyMonitor.CurrentValue;
		var candidates = await _archiveSource
			.GetArchiveCandidatesAsync(policy, batchSize, cancellationToken)
			.ConfigureAwait(false);

		var aggregates = 0;
		var events = 0;

		foreach (var candidate in candidates)
		{
			// Load the archivable events from the hot store.
			var stored = await _hotStore
				.LoadAsync(candidate.AggregateId, candidate.AggregateType, cancellationToken)
				.ConfigureAwait(false);
			var archivable = stored.Where(e => e.Version <= candidate.ArchivableUpToVersion).ToList();
			if (archivable.Count == 0)
			{
				continue;
			}

			// Write to cold storage (blob / S3 / GCS).
			await _coldStore
				.WriteAsync(candidate.AggregateId, archivable, cancellationToken)
				.ConfigureAwait(false);

			// Remove archived events from hot store. The tiered decorator will
			// transparently stitch hot + cold on the next read.
			var deleted = await _archiveSource
				.DeleteEventsUpToVersionAsync(
					candidate.AggregateId,
					candidate.AggregateType,
					candidate.ArchivableUpToVersion,
					cancellationToken)
				.ConfigureAwait(false);

			aggregates++;
			events += deleted;

			_logger.LogInformation(
				"Archived aggregate {AggregateId} ({AggregateType}): moved {Count} events to cold store up to v{Version}",
				candidate.AggregateId,
				candidate.AggregateType,
				deleted,
				candidate.ArchivableUpToVersion);
		}

		return new ArchiveCycleSummary(aggregates, events);
	}
}

/// <summary>Summary of a manual archive cycle.</summary>
public sealed record ArchiveCycleSummary(int AggregatesArchived, int EventsMoved);
