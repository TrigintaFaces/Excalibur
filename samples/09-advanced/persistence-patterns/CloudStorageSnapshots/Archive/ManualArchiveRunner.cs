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
/// <see cref="IEventStoreArchiveReader"/>, <see cref="IColdEventStore"/>) but lets a caller
/// force a single cycle so the archival behaviour can be observed immediately.
/// </para>
/// </remarks>
public sealed class ManualArchiveRunner
{
	private readonly IEventStoreArchive _archiveSource;
	private readonly IEventStoreArchiveReader _archiveReader;
	private readonly IColdEventStore _coldStore;
	private readonly IOptionsMonitor<ArchivePolicy> _policyMonitor;
	private readonly ILogger<ManualArchiveRunner> _logger;

	/// <summary>
	/// The tenant partition every cold-storage key is composed with, resolved <strong>once at
	/// construction</strong> rather than read per call. Cold keys written under one partition are
	/// unreachable from another, so resolving the scope in the query path would let a mid-cycle context
	/// change split one archive run across two key spaces. The registered sample uses the default
	/// single-tenant context. Explicit construction without a context uses the untenanted sentinel.
	/// </summary>
	private readonly KeyedTenantPartition _tenant;

	/// <summary>
	/// Initializes a new instance of the <see cref="ManualArchiveRunner"/> class.
	/// </summary>
	public ManualArchiveRunner(
		IEventStoreArchive archiveSource,
		IEventStoreArchiveReader archiveReader,
		IColdEventStore coldStore,
		IOptionsMonitor<ArchivePolicy> policyMonitor,
		ITenantContext? tenantContext,
		ILogger<ManualArchiveRunner> logger)
	{
		_archiveSource = archiveSource;
		_archiveReader = archiveReader;
		_coldStore = coldStore;
		_policyMonitor = policyMonitor;
		// No ambient context means this host is not multi-tenant, so every archived row belongs to the
		// reserved untenanted partition. The framework will not make that call for you: the conversion
		// requires a context, so the fallback is written here where it can be seen and reviewed.
		_tenant = tenantContext is null
			? KeyedTenantPartition.Untenanted
			: KeyedTenantPartition.FromContext(tenantContext);
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
			if (!string.Equals(candidate.Tenant.TenantId, _tenant.TenantId, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("This sample's archive runner can only process its configured tenant.");
			}

			// Load the archivable events from the hot store.
			var stored = await _archiveReader
				.LoadArchiveEventsAsync(candidate.Tenant, candidate.AggregateId, candidate.AggregateType, candidate.ArchivableUpToVersion, cancellationToken)
				.ConfigureAwait(false);
			var selected = stored.Where(e => e.Version <= candidate.ArchivableUpToVersion).ToList();
			if (selected.Any(e => ErasedEventMarker.IsErased(e.EventType)))
			{
				throw new InvalidOperationException("Archival cannot process erased events without cold-tier erasure support.");
			}

			var archivable = selected.Where(e => e.EventData is not null || e.ArchivedAt is null).ToList();
			if (archivable.Any(e => e.EventData is null))
			{
				throw new InvalidOperationException("An unresolved event payload cannot be acknowledged as archived.");
			}
			if (archivable.Count == 0)
			{
				continue;
			}

			// Write to cold storage (blob / S3 / GCS). The returned value is the durable
			// low-water mark: the highest proven contiguous durable prefix starting at version 0.
			// A -1 receipt means no such prefix is proven, even if a suffix was durably stored.
			var durableUpToVersion = await _coldStore
				.WriteAsync(candidate.Tenant, candidate.AggregateId, candidate.AggregateType, archivable, cancellationToken)
				.ConfigureAwait(false);

			// Delete from hot ONLY up to what cold has durably confirmed -- never up to the
			// version we requested. Bounding the delete by the watermark is what makes a
			// partial cold write safe: the unarchived tail stays hot and is retried next cycle.
			// This mirrors the framework's own EventArchiveService.
			var deleteUpToVersion = Math.Min(candidate.ArchivableUpToVersion, durableUpToVersion);

			// Nothing at or above the first archivable version was durably stored (including the
			// -1 "no prefix proven" case), so nothing may be deleted from hot -- the hot copy
			// is the only surviving one. Retry on the next cycle.
			if (deleteUpToVersion < archivable[0].Version)
			{
				_logger.LogWarning(
					"Cold write for aggregate {AggregateId} ({AggregateType}) proved no durable prefix reaching "
						+ "v{FirstVersion} (watermark v{Watermark}); keeping all hot events and retrying next cycle",
					candidate.AggregateId,
					candidate.AggregateType,
					archivable[0].Version,
					durableUpToVersion);
				continue;
			}

			// Tombstone the archived events in the hot store. They are not removed: the row stays and
			// its payload is cleared, so the stream's shape is preserved and the tiered decorator can
			// transparently stitch hot + cold on the next read.
			var tombstoned = await _archiveSource
				.TombstoneArchivedEventsUpToVersionAsync(
					candidate.Tenant,
					candidate.AggregateId,
					candidate.AggregateType,
					deleteUpToVersion,
					cancellationToken)
				.ConfigureAwait(false);

			aggregates++;
			events += tombstoned;

			_logger.LogInformation(
				"Archived aggregate {AggregateId} ({AggregateType}): moved {Count} events to cold store up to v{Version} "
					+ "(requested up to v{RequestedVersion})",
				candidate.AggregateId,
				candidate.AggregateType,
				tombstoned,
				deleteUpToVersion,
				candidate.ArchivableUpToVersion);
		}

		return new ArchiveCycleSummary(aggregates, events);
	}
}

/// <summary>Summary of a manual archive cycle.</summary>
public sealed record ArchiveCycleSummary(int AggregatesArchived, int EventsMoved);
