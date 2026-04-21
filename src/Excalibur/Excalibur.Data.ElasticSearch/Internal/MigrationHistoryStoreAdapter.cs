// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IMigrationHistoryStore"/> implementation that
/// forwards to <c>_inner.IndexAsync</c>, <c>_inner.SearchAsync</c>, and
/// <c>_inner.Indices.ExistsAsync</c> / <c>CreateAsync</c> on a real
/// <see cref="ElasticsearchClient"/>.
/// </summary>
internal sealed class MigrationHistoryStoreAdapter : IMigrationHistoryStore
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationHistoryStoreAdapter"/> class.
	/// </summary>
	public MigrationHistoryStoreAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<bool> WriteMigrationResultAsync(
		string indexName,
		string documentId,
		MigrationHistoryRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(record);

		var document = new MigrationHistoryDocument
		{
			ProjectionType = record.ProjectionType,
			PlanId = record.PlanId,
			RecordedAt = record.RecordedAt,
			ResultJson = record.ResultJson,
		};

		var response = await _inner.IndexAsync(
				document,
				idx => idx.Index(indexName).Id(documentId),
				cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<MigrationHistoryRecord>> QueryHistoryAsync(
		string indexName,
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

#pragma warning disable CS8604 // Elastic API nullability annotations are overly strict
		var response = await _inner.SearchAsync<MigrationHistoryDocument>(
				s => s.Index(indexName)
					.Size(1000)
					.Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field(f => f.Field("recordedAt").Order(SortOrder.Desc))),
				cancellationToken)
			.ConfigureAwait(false);
#pragma warning restore CS8604

		if (!response.IsValidResponse || response.Documents is null)
		{
			return [];
		}

		var results = new List<MigrationHistoryRecord>(response.Documents.Count);
		foreach (var doc in response.Documents)
		{
			results.Add(new MigrationHistoryRecord
			{
				ProjectionType = doc.ProjectionType,
				PlanId = doc.PlanId,
				RecordedAt = doc.RecordedAt,
				ResultJson = doc.ResultJson,
			});
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<bool> EnsureHistoryIndexAsync(
		string indexName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		var exists = await _inner.Indices.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);

		if (exists.Exists)
		{
			return true;
		}

		var createRequest = new CreateIndexRequest(indexName)
		{
			Mappings = BuildMigrationMapping(),
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
		};

		var response = await _inner.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse;
	}

	private static TypeMapping BuildMigrationMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["planId"] = new KeywordProperty(),
				["recordedAt"] = new DateProperty(),
				["resultJson"] = new TextProperty(),
			},
		};
	}

	private sealed class MigrationHistoryDocument
	{
		public string ProjectionType { get; init; } = string.Empty;

		public string PlanId { get; init; } = string.Empty;

		public DateTimeOffset RecordedAt { get; init; }

		public string ResultJson { get; init; } = string.Empty;
	}
}
