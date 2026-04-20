// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

#pragma warning disable CS8604 // Possible null reference argument -- Elastic client API nullability annotations are overly strict for query builder lambdas

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IProjectionEventIngest"/> implementation forwarding to
/// <c>ElasticsearchClient.IndexAsync</c>.
/// </summary>
internal sealed class ProjectionEventIngestAdapter : IProjectionEventIngest
{
	private readonly ElasticsearchClient _inner;
	private readonly string _writeIndexName;
	private readonly string _readIndexName;
	private readonly string _checkpointIndexName;

	public ProjectionEventIngestAdapter(
		ElasticsearchClient inner,
		string writeIndexName,
		string readIndexName,
		string checkpointIndexName)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
		_writeIndexName = writeIndexName;
		_readIndexName = readIndexName;
		_checkpointIndexName = checkpointIndexName;
	}

	public async Task<bool> IndexWriteEventAsync(WriteEventDocument document, string id, CancellationToken cancellationToken)
	{
		var response = await _inner.IndexAsync(document, idx => idx.Index(_writeIndexName).Id(id), cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse;
	}

	public async Task<bool> IndexReadEventAsync(ReadEventDocument document, string id, CancellationToken cancellationToken)
	{
		var response = await _inner.IndexAsync(document, idx => idx.Index(_readIndexName).Id(id), cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse;
	}

	public async Task<bool> IndexCheckpointAsync(ProjectionCheckpointDocument document, string projectionType, CancellationToken cancellationToken)
	{
		var response = await _inner.IndexAsync(document, idx => idx.Index(_checkpointIndexName).Id(projectionType), cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse;
	}
}

/// <summary>
/// Default <see cref="IProjectionEventLookup"/> implementation.
/// </summary>
internal sealed class ProjectionEventLookupAdapter : IProjectionEventLookup
{
	private readonly ElasticsearchClient _inner;
	private readonly string _writeIndexName;
	private readonly string _readIndexName;
	private readonly string _checkpointIndexName;

	public ProjectionEventLookupAdapter(
		ElasticsearchClient inner,
		string writeIndexName,
		string readIndexName,
		string checkpointIndexName)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
		_writeIndexName = writeIndexName;
		_readIndexName = readIndexName;
		_checkpointIndexName = checkpointIndexName;
	}

	public async Task<WriteEventDocument?> GetWriteEventByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		var response = await _inner.GetAsync<WriteEventDocument>(_writeIndexName, eventId, cancellationToken).ConfigureAwait(false);
		return response is { IsValidResponse: true, Found: true } ? response.Source : null;
	}

	public async Task<ReadEventDocument?> GetLatestReadForProjectionAsync(string projectionType, CancellationToken cancellationToken)
	{
		var response = await _inner.SearchAsync<ReadEventDocument>(s => s
				.Index(_readIndexName)
				.Size(1)
				.Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
				.Sort(ss => ss.Field(f => f.Field("readTimestamp").Order(SortOrder.Desc))),
			cancellationToken).ConfigureAwait(false);

		return response.Documents?.FirstOrDefault();
	}

	public async Task<IReadOnlyList<string>> GetProjectionTypesAsync(CancellationToken cancellationToken)
	{
		var response = await _inner.SearchAsync<ProjectionCheckpointDocument>(s => s
				.Index(_checkpointIndexName)
				.Size(1000),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse || response.Documents is null)
		{
			return [];
		}

		return response.Documents
			.Select(d => d.ProjectionType)
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}
}

/// <summary>
/// Default <see cref="IProjectionEventScan"/> implementation.
/// </summary>
internal sealed class ProjectionEventScanAdapter : IProjectionEventScan
{
	private readonly ElasticsearchClient _inner;
	private readonly string _writeIndexName;
	private readonly string _readIndexName;

	public ProjectionEventScanAdapter(
		ElasticsearchClient inner,
		string writeIndexName,
		string readIndexName)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
		_writeIndexName = writeIndexName;
		_readIndexName = readIndexName;
	}

	public async Task<DateTimeOffset?> GetLatestWriteTimestampAsync(CancellationToken cancellationToken)
	{
		var response = await _inner.SearchAsync<WriteEventDocument>(s => s
				.Index(_writeIndexName)
				.Size(1)
				.Sort(ss => ss.Field(f => f.Field("writeTimestamp").Order(SortOrder.Desc))),
			cancellationToken).ConfigureAwait(false);

		return response.Documents?.FirstOrDefault()?.WriteTimestamp;
	}

	public async Task<IReadOnlyList<ReadEventDocument>> SearchReadsAsync(ReadEventSearch search, CancellationToken cancellationToken)
	{
		var maxResults = search.MaxResults ?? 1000;
		var response = await _inner.SearchAsync<ReadEventDocument>(s =>
		{
			_ = s.Index(_readIndexName).Size(maxResults);

			_ = s.Query(q => q.Bool(b =>
			{
				var filters = new List<Action<QueryDescriptor<ReadEventDocument>>>();

				if (!string.IsNullOrEmpty(search.EventId))
				{
					filters.Add(qd => qd.Term(t => t.Field("eventId").Value(search.EventId)));
				}

				if (search.EventIds is { Count: > 0 })
				{
					var terms = new TermsQueryField(search.EventIds.Select(id => (FieldValue)id).ToList());
					filters.Add(qd => qd.Terms(t => t.Field("eventId").Terms(terms)));
				}

				if (!string.IsNullOrEmpty(search.ProjectionType))
				{
					filters.Add(qd => qd.Term(t => t.Field("projectionType").Value(search.ProjectionType)));
				}

				if (search.FromTimestamp.HasValue || search.ToTimestamp.HasValue)
				{
					filters.Add(qd => qd.Range(r => r.DateRange(dr =>
					{
						_ = dr.Field("readTimestamp");
						if (search.FromTimestamp.HasValue)
						{
							_ = dr.Gte(search.FromTimestamp.Value);
						}

						if (search.ToTimestamp.HasValue)
						{
							_ = dr.Lte(search.ToTimestamp.Value);
						}
					})));
				}

				if (filters.Count > 0)
				{
					_ = b.Filter(filters.ToArray());
				}
			}));

			if (search.SortByReadTimestampDesc)
			{
				_ = s.Sort(ss => ss.Field(f => f.Field("readTimestamp").Order(SortOrder.Desc)));
			}
		}, cancellationToken).ConfigureAwait(false);

		return response.Documents?.ToArray() ?? [];
	}

	public async Task<IReadOnlyList<WriteEventDocument>> SearchWritesOlderThanAsync(DateTime cutoff, int maxResults, CancellationToken cancellationToken)
	{
		var response = await _inner.SearchAsync<WriteEventDocument>(s => s
				.Index(_writeIndexName)
				.Size(maxResults)
				.Query(q => q.Range(r => r.DateRange(dr => dr.Field("writeTimestamp").Lte(cutoff))))
				.Sort(ss => ss.Field(f => f.Field("writeTimestamp").Order(SortOrder.Asc))),
			cancellationToken).ConfigureAwait(false);

		return response.Documents?.ToArray() ?? [];
	}

	public async Task<long> GetDocumentCountAsync(string indexName, ProjectionCountFilter filter, string? filterValue, CancellationToken cancellationToken)
	{
		var response = await _inner.CountAsync<WriteEventDocument>(c =>
		{
			_ = c.Indices(indexName);
			if (filter == ProjectionCountFilter.ReadsByProjectionType && !string.IsNullOrEmpty(filterValue))
			{
				_ = c.Query(q => q.Term(t => t.Field("projectionType").Value(filterValue)));
			}
		}, cancellationToken).ConfigureAwait(false);

		return response.IsValidResponse ? response.Count : 0;
	}
}

/// <summary>
/// Default <see cref="IProjectionIndexProvisioning"/> implementation. Owns the
/// three consistency-tracking index mappings.
/// </summary>
internal sealed class ProjectionIndexProvisioningAdapter : IProjectionIndexProvisioning
{
	private readonly ElasticsearchClient _inner;

	public ProjectionIndexProvisioningAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
	{
		var response = await _inner.Indices.ExistsAsync(indexName, cancellationToken).ConfigureAwait(false);
		return response.Exists;
	}

	public async Task<bool> CreateIndexAsync(string indexName, ConsistencyIndexKind kind, CancellationToken cancellationToken)
	{
		var mapping = BuildMapping(kind);
		var createRequest = new CreateIndexRequest(indexName)
		{
			Mappings = mapping,
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
		};

		var response = await _inner.Indices.CreateAsync(createRequest, cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse;
	}

	private static TypeMapping BuildMapping(ConsistencyIndexKind kind) => kind switch
	{
		ConsistencyIndexKind.WriteEvents => new TypeMapping
		{
			Properties = new Properties
			{
				["eventId"] = new KeywordProperty(),
				["aggregateId"] = new KeywordProperty(),
				["eventType"] = new KeywordProperty(),
				["writeTimestamp"] = new DateProperty { Format = "strict_date_time" },
			},
		},
		ConsistencyIndexKind.ReadEvents => new TypeMapping
		{
			Properties = new Properties
			{
				["eventId"] = new KeywordProperty(),
				["projectionType"] = new KeywordProperty(),
				["readTimestamp"] = new DateProperty { Format = "strict_date_time" },
			},
		},
		ConsistencyIndexKind.Checkpoints => new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["lastEventId"] = new KeywordProperty(),
				["lastProcessedAt"] = new DateProperty { Format = "strict_date_time" },
				["updatedAt"] = new DateProperty { Format = "strict_date_time" },
			},
		},
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown consistency index kind."),
	};
}
