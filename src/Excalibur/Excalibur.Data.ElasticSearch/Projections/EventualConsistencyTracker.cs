// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Tracks eventual consistency between write and read models using Elasticsearch indices.
/// S801 bd-r3xkes: migrated to 4 operation-axis seams per COMPASS msg 1867.
/// </summary>
public sealed class EventualConsistencyTracker : IEventualConsistencyTracker, IEventualConsistencyTrackerAdmin, IDisposable
{
	private readonly IProjectionEventIngest _ingest;
	private readonly IProjectionEventLookup _lookup;
	private readonly IProjectionEventScan _scan;
	private readonly IProjectionIndexProvisioning _provisioning;
	private readonly ProjectionOptions _settings;
	private readonly ILogger<EventualConsistencyTracker> _logger;
	private readonly string _writeIndexName;
	private readonly string _readIndexName;
	private readonly string _checkpointIndexName;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;
	private ConsistencyAlertOptions? _alertConfiguration;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventualConsistencyTracker" /> class.
	/// </summary>
	public EventualConsistencyTracker(
		ElasticsearchClient client,
		IOptions<ProjectionOptions> options,
		ILogger<EventualConsistencyTracker> logger)
		: this(
			CreateIngest(client, options),
			CreateLookup(client, options),
			CreateScan(client, options),
			CreateProvisioning(client),
			options,
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance using the 4 seam adapters directly.
	/// Test entry point (ADR-142 §D7 S801 bd-r3xkes).
	/// </summary>
	internal EventualConsistencyTracker(
		IProjectionEventIngest ingest,
		IProjectionEventLookup lookup,
		IProjectionEventScan scan,
		IProjectionIndexProvisioning provisioning,
		IOptions<ProjectionOptions> options,
		ILogger<EventualConsistencyTracker> logger)
	{
		_ingest = ingest ?? throw new ArgumentNullException(nameof(ingest));
		_lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
		_scan = scan ?? throw new ArgumentNullException(nameof(scan));
		_provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning));
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_writeIndexName = $"{_settings.IndexPrefix}-consistency-writes";
		_readIndexName = $"{_settings.IndexPrefix}-consistency-reads";
		_checkpointIndexName = $"{_settings.IndexPrefix}-consistency-checkpoints";
	}

	/// <inheritdoc />
	public async Task TrackWriteModelEventAsync(
		string eventId,
		string aggregateId,
		string eventType,
		DateTime timestamp,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

		if (!_settings.ConsistencyTracking.Enabled)
		{
			return;
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var document = new WriteEventDocument
		{
			EventId = eventId,
			AggregateId = aggregateId,
			EventType = eventType,
			WriteTimestamp = timestamp,
		};

		var success = await _ingest.IndexWriteEventAsync(document, eventId, cancellationToken).ConfigureAwait(false);
		if (!success)
		{
			_logger.LogWarning("Failed to track write model event {EventId}", eventId);
		}
	}

	/// <inheritdoc />
	public async Task TrackReadModelProjectionAsync(
		string eventId,
		string projectionType,
		DateTime timestamp,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

		if (!_settings.ConsistencyTracking.Enabled)
		{
			return;
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var readDoc = new ReadEventDocument { EventId = eventId, ProjectionType = projectionType, ReadTimestamp = timestamp };
		var readId = $"{eventId}:{projectionType}";
		var readOk = await _ingest.IndexReadEventAsync(readDoc, readId, cancellationToken).ConfigureAwait(false);
		if (!readOk)
		{
			_logger.LogWarning("Failed to track read model projection {ProjectionType}/{EventId}", projectionType, eventId);
		}

		var checkpoint = new ProjectionCheckpointDocument
		{
			ProjectionType = projectionType,
			LastEventId = eventId,
			LastProcessedAt = timestamp,
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		var checkpointOk = await _ingest.IndexCheckpointAsync(checkpoint, projectionType, cancellationToken).ConfigureAwait(false);
		if (!checkpointOk)
		{
			_logger.LogWarning("Failed to update projection checkpoint {ProjectionType}", projectionType);
		}
	}

	/// <inheritdoc />
	public async Task<ConsistencyLag> GetConsistencyLagAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

		if (!_settings.ConsistencyTracking.Enabled)
		{
			return new ConsistencyLag
			{
				ProjectionType = projectionType,
				CurrentLagMs = 0,
				AverageLagMs = 0,
				MaxLagMs = 0,
				MinLagMs = 0,
				PendingEvents = 0,
				IsWithinSLA = true,
			};
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var latestWrite = await _scan.GetLatestWriteTimestampAsync(cancellationToken).ConfigureAwait(false);
		var latestReadDoc = await _lookup.GetLatestReadForProjectionAsync(projectionType, cancellationToken).ConfigureAwait(false);
		var latestRead = latestReadDoc?.ReadTimestamp;

		var currentLagMs = latestWrite.HasValue && latestRead.HasValue
			? Math.Max(0, (latestWrite.Value - latestRead.Value).TotalMilliseconds)
			: 0;

		var samples = await GetLagSamplesAsync(projectionType, cancellationToken).ConfigureAwait(false);

		var averageLag = samples.Count > 0 ? samples.Average() : 0;
		var maxLag = samples.Count > 0 ? samples.Max() : 0;
		var minLag = samples.Count > 0 ? samples.Min() : 0;
		var p95Lag = samples.Count > 0 ? CalculatePercentile(samples, 95) : 0;
		var p99Lag = samples.Count > 0 ? CalculatePercentile(samples, 99) : 0;

		var writesCount = await _scan.GetDocumentCountAsync(_writeIndexName, ProjectionCountFilter.All, null, cancellationToken).ConfigureAwait(false);
		var readsCount = await _scan.GetDocumentCountAsync(_readIndexName, ProjectionCountFilter.ReadsByProjectionType, projectionType, cancellationToken).ConfigureAwait(false);

		var pendingEvents = Math.Max(0, writesCount - readsCount);
		var maxLagThreshold = _settings.ConsistencyTracking.ExpectedMaxLag.TotalMilliseconds;

		return new ConsistencyLag
		{
			ProjectionType = projectionType,
			CurrentLagMs = currentLagMs,
			AverageLagMs = averageLag,
			MaxLagMs = maxLag,
			MinLagMs = minLag,
			P95LagMs = p95Lag,
			P99LagMs = p99Lag,
			PendingEvents = pendingEvents,
			LastProcessedEventTime = latestRead,
			IsWithinSLA = currentLagMs <= maxLagThreshold,
		};
	}

	/// <inheritdoc />
	public async Task<IEnumerable<ConsistencyMetrics>> GetConsistencyMetricsAsync(
		DateTime fromTime,
		DateTime toTime,
		CancellationToken cancellationToken)
	{
		if (!_settings.ConsistencyTracking.Enabled)
		{
			return [];
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var reads = await _scan.SearchReadsAsync(
			new ReadEventSearch(FromTimestamp: fromTime, ToTimestamp: toTime, MaxResults: 10000),
			cancellationToken).ConfigureAwait(false);

		var metrics = new List<ConsistencyMetrics>();
		var maxLagThreshold = _settings.ConsistencyTracking.ExpectedMaxLag.TotalMilliseconds;
		var grouped = reads.GroupBy(d => d.ProjectionType, StringComparer.Ordinal);

		foreach (var group in grouped)
		{
			var lags = await GetLagSamplesAsync(group.Key, cancellationToken).ConfigureAwait(false);
			var totalEvents = group.Count();
			var averageLag = lags.Count > 0 ? lags.Average() : 0;
			var violations = lags.Count(lag => lag > maxLagThreshold);
			var totalSeconds = Math.Max(1, (toTime - fromTime).TotalSeconds);

			metrics.Add(new ConsistencyMetrics
			{
				ProjectionType = group.Key,
				PeriodStart = fromTime,
				PeriodEnd = toTime,
				TotalEventsProcessed = totalEvents,
				AverageProcessingTimeMs = averageLag,
				EventsPerSecond = totalEvents / totalSeconds,
				SLACompliancePercentage = totalEvents == 0
					? 100
					: (totalEvents - violations) / (double)totalEvents * 100,
				ConsistencyViolations = violations,
				LagDistribution = BuildLagDistribution(lags),
			});
		}

		return metrics;
	}

	/// <inheritdoc />
	public async Task<bool> IsEventFullyProcessedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		if (!_settings.ConsistencyTracking.Enabled)
		{
			return true;
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var write = await _lookup.GetWriteEventByIdAsync(eventId, cancellationToken).ConfigureAwait(false);
		if (write is null)
		{
			return false;
		}

		var projectionTypes = await _lookup.GetProjectionTypesAsync(cancellationToken).ConfigureAwait(false);
		if (projectionTypes.Count == 0)
		{
			return false;
		}

		var reads = await _scan.SearchReadsAsync(
			new ReadEventSearch(EventId: eventId, MaxResults: 1000),
			cancellationToken).ConfigureAwait(false);

		var processed = reads
			.Select(d => d.ProjectionType)
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.ToHashSet(StringComparer.Ordinal);

		return projectionTypes.All(processed.Contains);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<LaggingEvent>> GetLaggingEventsAsync(
		TimeSpan expectedProcessingTime,
		int maxResults,
		CancellationToken cancellationToken)
	{
		if (expectedProcessingTime <= TimeSpan.Zero || !_settings.ConsistencyTracking.Enabled)
		{
			return [];
		}

		await EnsureIndicesAsync(cancellationToken).ConfigureAwait(false);

		var cutoff = (DateTimeOffset.UtcNow - expectedProcessingTime).UtcDateTime;
		var writes = await _scan.SearchWritesOlderThanAsync(cutoff, maxResults, cancellationToken).ConfigureAwait(false);

		if (writes.Count == 0)
		{
			return [];
		}

		var projectionTypes = await _lookup.GetProjectionTypesAsync(cancellationToken).ConfigureAwait(false);
		if (projectionTypes.Count == 0)
		{
			return [];
		}

		var eventIds = writes
			.Select(d => d.EventId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (eventIds.Count == 0)
		{
			return [];
		}

		var reads = await _scan.SearchReadsAsync(
			new ReadEventSearch(EventIds: eventIds, MaxResults: maxResults * projectionTypes.Count),
			cancellationToken).ConfigureAwait(false);

		var readLookup = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (var doc in reads)
		{
			var projectionSet = readLookup.GetOrAdd(doc.EventId, _ => new HashSet<string>(StringComparer.Ordinal));
			_ = projectionSet.Add(doc.ProjectionType);
		}

		var now = DateTimeOffset.UtcNow;
		var lagging = new List<LaggingEvent>();
		foreach (var write in writes)
		{
			if (string.IsNullOrWhiteSpace(write.EventId))
			{
				continue;
			}

			var processedProjections = readLookup.TryGetValue(write.EventId, out var processed)
				? processed
				: new HashSet<string>(StringComparer.Ordinal);
			var pending = projectionTypes
				.Where(p => !processedProjections.Contains(p))
				.ToList();

			if (pending.Count == 0)
			{
				continue;
			}

			lagging.Add(new LaggingEvent
			{
				EventId = write.EventId,
				AggregateId = write.AggregateId,
				EventType = write.EventType,
				WriteModelTimestamp = write.WriteTimestamp,
				Age = now - write.WriteTimestamp,
				PendingProjections = pending,
			});
		}

		return lagging;
	}

	/// <inheritdoc />
	public Task ConfigureConsistencyAlertsAsync(
		ConsistencyAlertOptions config,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(config);
		_alertConfiguration = config;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_initializationLock.Dispose();
	}

	private async Task<List<double>> GetLagSamplesAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		var reads = await _scan.SearchReadsAsync(
			new ReadEventSearch(ProjectionType: projectionType, MaxResults: 100, SortByReadTimestampDesc: true),
			cancellationToken).ConfigureAwait(false);

		var lags = new List<double>();
		foreach (var read in reads)
		{
			if (string.IsNullOrWhiteSpace(read.EventId))
			{
				continue;
			}

			var write = await _lookup.GetWriteEventByIdAsync(read.EventId, cancellationToken).ConfigureAwait(false);
			if (write is not null)
			{
				var lag = (read.ReadTimestamp - write.WriteTimestamp).TotalMilliseconds;
				lags.Add(Math.Max(0, lag));
			}
		}

		return lags;
	}

	private static double CalculatePercentile(IReadOnlyList<double> values, int percentile)
	{
		if (values.Count == 0)
		{
			return 0;
		}

		var sorted = values.OrderBy(v => v).ToList();
		var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
		index = Math.Clamp(index, 0, sorted.Count - 1);
		return sorted[index];
	}

	private static IDictionary<string, long>? BuildLagDistribution(IReadOnlyList<double> lags)
	{
		if (lags.Count == 0)
		{
			return null;
		}

		var buckets = new Dictionary<string, long>(StringComparer.Ordinal)
		{
			["0-50ms"] = 0,
			["50-200ms"] = 0,
			["200-1000ms"] = 0,
			[">1000ms"] = 0,
		};

		foreach (var lag in lags)
		{
			if (lag <= 50)
			{
				buckets["0-50ms"]++;
			}
			else if (lag <= 200)
			{
				buckets["50-200ms"]++;
			}
			else if (lag <= 1000)
			{
				buckets["200-1000ms"]++;
			}
			else
			{
				buckets[">1000ms"]++;
			}
		}

		return buckets;
	}

	private async Task EnsureIndicesAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			await EnsureIndexAsync(_writeIndexName, ConsistencyIndexKind.WriteEvents, cancellationToken).ConfigureAwait(false);
			await EnsureIndexAsync(_readIndexName, ConsistencyIndexKind.ReadEvents, cancellationToken).ConfigureAwait(false);
			await EnsureIndexAsync(_checkpointIndexName, ConsistencyIndexKind.Checkpoints, cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initializationLock.Release();
		}
	}

	private async Task EnsureIndexAsync(string indexName, ConsistencyIndexKind kind, CancellationToken cancellationToken)
	{
		var exists = await _provisioning.IndexExistsAsync(indexName, cancellationToken).ConfigureAwait(false);
		if (exists)
		{
			return;
		}

		var created = await _provisioning.CreateIndexAsync(indexName, kind, cancellationToken).ConfigureAwait(false);
		if (!created)
		{
			_logger.LogWarning("Failed to create consistency tracking index {IndexName}", indexName);
		}
	}

	private static IProjectionEventIngest CreateIngest(ElasticsearchClient client, IOptions<ProjectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		var settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		return new ProjectionEventIngestAdapter(
			client,
			$"{settings.IndexPrefix}-consistency-writes",
			$"{settings.IndexPrefix}-consistency-reads",
			$"{settings.IndexPrefix}-consistency-checkpoints");
	}

	private static IProjectionEventLookup CreateLookup(ElasticsearchClient client, IOptions<ProjectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		var settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		return new ProjectionEventLookupAdapter(
			client,
			$"{settings.IndexPrefix}-consistency-writes",
			$"{settings.IndexPrefix}-consistency-reads",
			$"{settings.IndexPrefix}-consistency-checkpoints");
	}

	private static IProjectionEventScan CreateScan(ElasticsearchClient client, IOptions<ProjectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		var settings = options.Value ?? throw new ArgumentNullException(nameof(options));
		return new ProjectionEventScanAdapter(
			client,
			$"{settings.IndexPrefix}-consistency-writes",
			$"{settings.IndexPrefix}-consistency-reads");
	}

	private static IProjectionIndexProvisioning CreateProvisioning(ElasticsearchClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		return new ProjectionIndexProvisioningAdapter(client);
	}
}
