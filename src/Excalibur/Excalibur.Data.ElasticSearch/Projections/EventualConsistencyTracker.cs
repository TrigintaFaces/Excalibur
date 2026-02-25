// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Tracks eventual consistency between write and read models using Elasticsearch indices.
/// </summary>
public sealed class EventualConsistencyTracker : IEventualConsistencyTracker, IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly ProjectionOptions _settings;
	private readonly ILogger<EventualConsistencyTracker> _logger;
	private readonly string _writeIndexName;
	private readonly string _readIndexName;
	private readonly string _checkpointIndexName;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;
	private ConsistencyAlertConfiguration? _alertConfiguration;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventualConsistencyTracker" /> class.
	/// </summary>
	/// <param name="client"> The Elasticsearch client. </param>
	/// <param name="options"> Projection settings. </param>
	/// <param name="logger"> Logger instance. </param>
	public EventualConsistencyTracker(
		ElasticsearchClient client,
		IOptions<ProjectionOptions> options,
		ILogger<EventualConsistencyTracker> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
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

		var response = await _client.IndexAsync(
				document,
				idx => idx.Index(_writeIndexName).Id(eventId),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to track write model event {EventId}: {Error}",
				eventId,
				response.DebugInformation);
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

		var document = new ReadEventDocument { EventId = eventId, ProjectionType = projectionType, ReadTimestamp = timestamp, };

		var readId = $"{eventId}:{projectionType}";
		var response = await _client.IndexAsync(
				document,
				idx => idx.Index(_readIndexName).Id(readId),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to track read model projection {ProjectionType}/{EventId}: {Error}",
				projectionType,
				eventId,
				response.DebugInformation);
		}

		var checkpoint = new ProjectionCheckpointDocument
		{
			ProjectionType = projectionType,
			LastEventId = eventId,
			LastProcessedAt = timestamp,
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		var checkpointResponse = await _client.IndexAsync(
				checkpoint,
				idx => idx.Index(_checkpointIndexName).Id(projectionType),
				cancellationToken)
			.ConfigureAwait(false);

		if (!checkpointResponse.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to update projection checkpoint {ProjectionType}: {Error}",
				projectionType,
				checkpointResponse.DebugInformation);
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

		var latestWrite = await GetLatestWriteTimestampAsync(cancellationToken).ConfigureAwait(false);
		var latestRead = await GetLatestReadTimestampAsync(projectionType, cancellationToken)
			.ConfigureAwait(false);

		var currentLagMs = latestWrite.HasValue && latestRead.HasValue
			? Math.Max(0, (latestWrite.Value - latestRead.Value).TotalMilliseconds)
			: 0;

		var samples = await GetLagSamplesAsync(projectionType, cancellationToken).ConfigureAwait(false);

		var averageLag = samples.Count > 0 ? samples.Average() : 0;
		var maxLag = samples.Count > 0 ? samples.Max() : 0;
		var minLag = samples.Count > 0 ? samples.Min() : 0;
		var p95Lag = samples.Count > 0 ? CalculatePercentile(samples, 95) : 0;
		var p99Lag = samples.Count > 0 ? CalculatePercentile(samples, 99) : 0;

		var writesCount = await GetCountAsync(_writeIndexName, null, cancellationToken)
			.ConfigureAwait(false);
		var readsCount = await GetCountAsync(
				_readIndexName,
				q => q.Term(t => t.Field("projectionType").Value(projectionType)),
				cancellationToken)
			.ConfigureAwait(false);

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

		var response = await _client.SearchAsync<ReadEventDocument>(s => s
					.Index(_readIndexName)
					.Size(10000)
					.Query(q => q.Bool(b => b.Filter(f => f.Range(r => r.DateRange(dr => dr
						.Field("readTimestamp")
						.Gte(fromTime)
						.Lte(toTime)))))),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			_logger.LogWarning(
				"Failed to retrieve consistency metrics: {Error}",
				response.DebugInformation);
			return [];
		}

		var metrics = new List<ConsistencyMetrics>();
		var maxLagThreshold = _settings.ConsistencyTracking.ExpectedMaxLag.TotalMilliseconds;
		var grouped = response.Documents.GroupBy(d => d.ProjectionType, StringComparer.Ordinal);

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

		var writeResponse = await _client.GetAsync<WriteEventDocument>(
				_writeIndexName,
				eventId,
				cancellationToken)
			.ConfigureAwait(false);

		if (writeResponse is not { IsValidResponse: true, Found: true })
		{
			return false;
		}

		var projectionTypes = await GetProjectionTypesAsync(cancellationToken).ConfigureAwait(false);
		if (projectionTypes.Count == 0)
		{
			return false;
		}

		var readResponse = await _client.SearchAsync<ReadEventDocument>(s => s
					.Index(_readIndexName)
					.Size(1000)
					.Query(q => q.Term(t => t.Field("eventId").Value(eventId))),
				cancellationToken)
			.ConfigureAwait(false);

		var processed = readResponse.Documents is null
			? new HashSet<string>(StringComparer.Ordinal)
			: readResponse.Documents
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
		var writeResponse = await _client.SearchAsync<WriteEventDocument>(s => s
					.Index(_writeIndexName)
					.Size(maxResults)
					.Query(q => q.Range(r => r.DateRange(dr => dr
						.Field("writeTimestamp")
						.Lte(cutoff))))
					.Sort(s => s.Field("writeTimestamp", new FieldSort { Order = SortOrder.Asc })),
				cancellationToken)
			.ConfigureAwait(false);

		if (!writeResponse.IsValidResponse || writeResponse.Documents is null)
		{
			return [];
		}

		var projectionTypes = await GetProjectionTypesAsync(cancellationToken).ConfigureAwait(false);
		if (projectionTypes.Count == 0)
		{
			return [];
		}

		var eventIds = writeResponse.Documents
			.Select(d => d.EventId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (eventIds.Count == 0)
		{
			return [];
		}

		var terms = new TermsQueryField(eventIds.Select(id => (FieldValue)id).ToList());
		var readResponse = await _client.SearchAsync<ReadEventDocument>(s => s
					.Index(_readIndexName)
					.Size(maxResults * projectionTypes.Count)
					.Query(q => q.Terms(t => t.Field("eventId").Term(terms))),
				cancellationToken)
			.ConfigureAwait(false);

		var readLookup = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.Ordinal);
		if (readResponse.Documents is not null)
		{
			foreach (var doc in readResponse.Documents)
			{
				var projectionSet = readLookup.GetOrAdd(
					doc.EventId,
					_ => new HashSet<string>(StringComparer.Ordinal));
				_ = projectionSet.Add(doc.ProjectionType);
			}
		}

		var now = DateTimeOffset.UtcNow;
		var lagging = new List<LaggingEvent>();
		foreach (var write in writeResponse.Documents)
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
		ConsistencyAlertConfiguration config,
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

	private static TypeMapping BuildWriteMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["eventId"] = new KeywordProperty(),
				["aggregateId"] = new KeywordProperty(),
				["eventType"] = new KeywordProperty(),
				["writeTimestamp"] = new DateProperty(),
			},
		};
	}

	private static TypeMapping BuildReadMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["eventId"] = new KeywordProperty(),
				["projectionType"] = new KeywordProperty(),
				["readTimestamp"] = new DateProperty(),
			},
		};
	}

	private static TypeMapping BuildCheckpointMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["lastEventId"] = new KeywordProperty(),
				["lastProcessedAt"] = new DateProperty(),
				["updatedAt"] = new DateProperty(),
			},
		};
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

			await EnsureIndexAsync(_writeIndexName, BuildWriteMapping(), cancellationToken)
				.ConfigureAwait(false);
			await EnsureIndexAsync(_readIndexName, BuildReadMapping(), cancellationToken)
				.ConfigureAwait(false);
			await EnsureIndexAsync(_checkpointIndexName, BuildCheckpointMapping(), cancellationToken)
				.ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initializationLock.Release();
		}
	}

	private async Task EnsureIndexAsync(
		string indexName,
		TypeMapping mapping,
		CancellationToken cancellationToken)
	{
		var exists = await _client.Indices.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);
		if (exists.Exists)
		{
			return;
		}

		var createRequest = new CreateIndexRequest(indexName)
		{
			Mappings = mapping,
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0, },
		};

		var response = await _client.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			_logger.LogWarning(
				"Failed to create consistency tracking index {IndexName}: {Error}",
				indexName,
				response.DebugInformation);
		}
	}

	private async Task<DateTimeOffset?> GetLatestWriteTimestampAsync(CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<WriteEventDocument>(s => s
					.Index(_writeIndexName)
					.Size(1)
					.Sort(s => s.Field("writeTimestamp", new FieldSort { Order = SortOrder.Desc })),
				cancellationToken)
			.ConfigureAwait(false);

		return response.Documents?.FirstOrDefault()?.WriteTimestamp;
	}

	private async Task<DateTimeOffset?> GetLatestReadTimestampAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<ReadEventDocument>(s => s
					.Index(_readIndexName)
					.Size(1)
					.Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field("readTimestamp", new FieldSort { Order = SortOrder.Desc })),
				cancellationToken)
			.ConfigureAwait(false);

		return response.Documents?.FirstOrDefault()?.ReadTimestamp;
	}

	private async Task<List<double>> GetLagSamplesAsync(
		string projectionType,
		CancellationToken cancellationToken)
	{
		var readResponse = await _client.SearchAsync<ReadEventDocument>(s => s
					.Index(_readIndexName)
					.Size(100)
					.Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field("readTimestamp", new FieldSort { Order = SortOrder.Desc })),
				cancellationToken)
			.ConfigureAwait(false);

		if (!readResponse.IsValidResponse || readResponse.Documents is null)
		{
			return [];
		}

		var lags = new List<double>();
		foreach (var read in readResponse.Documents)
		{
			if (string.IsNullOrWhiteSpace(read.EventId))
			{
				continue;
			}

			var writeResponse = await _client.GetAsync<WriteEventDocument>(
					_writeIndexName,
					read.EventId,
					cancellationToken)
				.ConfigureAwait(false);

			if (writeResponse is { IsValidResponse: true, Found: true } &&
				writeResponse.Source is not null)
			{
				var lag = (read.ReadTimestamp - writeResponse.Source.WriteTimestamp)
					.TotalMilliseconds;
				lags.Add(Math.Max(0, lag));
			}
		}

		return lags;
	}

	private async Task<long> GetCountAsync(
		string indexName,
		Action<QueryDescriptor<WriteEventDocument>>? query,
		CancellationToken cancellationToken)
	{
		var response = await _client.CountAsync<WriteEventDocument>(c =>
		{
			_ = c.Indices(indexName);
			if (query is not null)
			{
				_ = c.Query(query);
			}
		}, cancellationToken).ConfigureAwait(false);

		return response.IsValidResponse ? response.Count : 0;
	}

	private async Task<List<string>> GetProjectionTypesAsync(CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<ProjectionCheckpointDocument>(s => s
					.Index(_checkpointIndexName)
					.Size(1000),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			return [];
		}

		return
		[
			.. response.Documents
				.Select(d => d.ProjectionType)
				.Where(p => !string.IsNullOrWhiteSpace(p))
				.Distinct(StringComparer.Ordinal)
		];
	}

	private sealed class WriteEventDocument
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public string EventType { get; init; } = string.Empty;
		public DateTimeOffset WriteTimestamp { get; init; }
	}

	private sealed class ReadEventDocument
	{
		public string EventId { get; init; } = string.Empty;
		public string ProjectionType { get; init; } = string.Empty;
		public DateTimeOffset ReadTimestamp { get; init; }
	}

	private sealed class ProjectionCheckpointDocument
	{
		public string ProjectionType { get; init; } = string.Empty;
		public string? LastEventId { get; init; }
		public DateTimeOffset? LastProcessedAt { get; init; }
		public DateTimeOffset UpdatedAt { get; init; }
	}
}
