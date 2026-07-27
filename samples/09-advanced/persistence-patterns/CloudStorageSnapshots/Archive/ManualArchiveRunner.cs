// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;
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
	/// The tenant partition every cold-storage key is composed with, resolved <strong>once at
	/// construction</strong> rather than read per call. Cold keys written under one partition are
	/// unreachable from another, so resolving the scope in the query path would let a mid-cycle context
	/// change split one archive run across two key spaces. A single-tenant host registers no
	/// <c>ITenantContext</c> and gets the explicit untenanted sentinel — never an empty term.
	/// </summary>
	private readonly KeyedTenantPartition _tenant;

	/// <summary>
	/// Initializes a new instance of the <see cref="ManualArchiveRunner"/> class.
	/// </summary>
	public ManualArchiveRunner(
		IEventStoreArchive archiveSource,
		IEventStore hotStore,
		IColdEventStore coldStore,
		IOptionsMonitor<ArchivePolicy> policyMonitor,
		ITenantContext? tenantContext,
		ILogger<ManualArchiveRunner> logger)
	{
		_archiveSource = archiveSource;
		_hotStore = hotStore;
		_coldStore = coldStore;
		_policyMonitor = policyMonitor;
		_tenant = KeyedTenantPartition.FromContext(tenantContext);
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

			// Write to cold storage (blob / S3 / GCS). The returned value is the durable
			// low-water mark: the highest version the cold tier has actually committed, or
			// -1 when this call durably added nothing. It is NOT necessarily the version we
			// asked it to archive -- a partial or deferred cold write returns less.
			var durableUpToVersion = await _coldStore
				.WriteAsync(_tenant, candidate.AggregateId, archivable, cancellationToken)
				.ConfigureAwait(false);

			// Delete from hot ONLY up to what cold has durably confirmed -- never up to the
			// version we requested. Bounding the delete by the watermark is what makes a
			// partial cold write safe: the unarchived tail stays hot and is retried next cycle.
			// This mirrors the framework's own EventArchiveService.
			var deleteUpToVersion = Math.Min(candidate.ArchivableUpToVersion, durableUpToVersion);

			// Nothing at or above the first archivable version was durably stored (including the
			// -1 "durably added nothing" case), so nothing may be deleted from hot -- the hot copy
			// is the only surviving one. Retry on the next cycle.
			if (deleteUpToVersion < archivable[0].Version)
			{
				_logger.LogWarning(
					"Cold write for aggregate {AggregateId} ({AggregateType}) durably stored nothing at or above "
						+ "v{FirstVersion} (watermark v{Watermark}); keeping all hot events and retrying next cycle",
					candidate.AggregateId,
					candidate.AggregateType,
					archivable[0].Version,
					durableUpToVersion);
				continue;
			}

			// Remove archived events from hot store. The tiered decorator will
			// transparently stitch hot + cold on the next read.
			var deleted = await _archiveSource
				.DeleteEventsUpToVersionAsync(
					_tenant,
					candidate.AggregateId,
					candidate.AggregateType,
					deleteUpToVersion,
					cancellationToken)
				.ConfigureAwait(false);

			aggregates++;
			events += deleted;

			_logger.LogInformation(
				"Archived aggregate {AggregateId} ({AggregateType}): moved {Count} events to cold store up to v{Version} "
					+ "(requested up to v{RequestedVersion})",
				candidate.AggregateId,
				candidate.AggregateType,
				deleted,
				deleteUpToVersion,
				candidate.ArchivableUpToVersion);
		}

		return new ArchiveCycleSummary(aggregates, events);
	}
}

/// <summary>Summary of a manual archive cycle.</summary>
public sealed record ArchiveCycleSummary(int AggregatesArchived, int EventsMoved);
