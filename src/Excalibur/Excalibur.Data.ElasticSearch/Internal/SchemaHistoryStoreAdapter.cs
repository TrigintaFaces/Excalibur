// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="ISchemaHistoryStore"/> implementation that forwards
/// to <c>_inner.IndexAsync</c>, <c>_inner.SearchAsync</c>, and
/// <c>_inner.Indices.ExistsAsync</c> / <c>CreateAsync</c> on a real
/// <see cref="ElasticsearchClient"/>. Owns the document type +
/// history-mapping shape so the seam consumer sees only domain types.
/// </summary>
internal sealed class SchemaHistoryStoreAdapter : ISchemaHistoryStore
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaHistoryStoreAdapter"/> class.
	/// </summary>
	public SchemaHistoryStoreAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<bool> WriteSchemaVersionAsync(
		string indexName,
		string documentId,
		SchemaHistoryRecord record,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(record);

		var document = new SchemaVersionDocument
		{
			ProjectionType = record.ProjectionType,
			Version = record.Version,
			SchemaJson = record.SchemaJson,
			RegisteredAt = record.RegisteredAt,
			Description = record.Description,
			MigrationNotes = record.MigrationNotes,
		};

		var response = await _inner.IndexAsync(
				document,
				idx => idx.Index(indexName).Id(documentId),
				cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<SchemaHistoryRecord>> QueryHistoryAsync(
		string indexName,
		string projectionType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);

#pragma warning disable CS8604 // Elastic API nullability annotations are overly strict
		var response = await _inner.SearchAsync<SchemaVersionDocument>(
				s => s.Index(indexName)
					.Size(1000)
					.Query(q => q.Term(t => t.Field("projectionType").Value(projectionType)))
					.Sort(s => s.Field(f => f.Field("registeredAt").Order(SortOrder.Asc))),
				cancellationToken)
			.ConfigureAwait(false);
#pragma warning restore CS8604

		if (!response.IsValidResponse || response.Documents is null)
		{
			return [];
		}

		var results = new List<SchemaHistoryRecord>(response.Documents.Count);
		foreach (var doc in response.Documents)
		{
			results.Add(new SchemaHistoryRecord
			{
				ProjectionType = doc.ProjectionType,
				Version = doc.Version,
				SchemaJson = doc.SchemaJson,
				RegisteredAt = doc.RegisteredAt,
				Description = doc.Description,
				MigrationNotes = doc.MigrationNotes,
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
			Mappings = BuildHistoryMapping(),
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
		};

		var response = await _inner.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse;
	}

	private static TypeMapping BuildHistoryMapping()
	{
		return new TypeMapping
		{
			Properties = new Properties
			{
				["projectionType"] = new KeywordProperty(),
				["version"] = new KeywordProperty(),
				["registeredAt"] = new DateProperty(),
				["schemaJson"] = new TextProperty(),
				["description"] = new TextProperty(),
				["migrationNotes"] = new TextProperty(),
			},
		};
	}

	private sealed class SchemaVersionDocument
	{
		public string ProjectionType { get; init; } = string.Empty;

		public string Version { get; init; } = string.Empty;

		public string SchemaJson { get; init; } = string.Empty;

		public DateTimeOffset RegisteredAt { get; init; }

		public string? Description { get; init; }

		public string? MigrationNotes { get; init; }
	}
}
