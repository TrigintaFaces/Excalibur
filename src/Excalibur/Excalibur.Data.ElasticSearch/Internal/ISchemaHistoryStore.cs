// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// schema-version persistence used by
/// <see cref="Excalibur.Data.ElasticSearch.Projections.SchemaEvolutionHandler"/>.
/// Exposes write/query/ensure for the schema-history index — not SDK
/// factory shape — so tests can substitute the SDK without depending on
/// which <c>IndexAsync</c> / <c>SearchAsync</c> overloads remain virtual in
/// a given SDK minor version. Not a consumer-facing abstraction; do not
/// make this public.
/// </summary>
/// <remarks>
/// Fifth of the 6 γ seams per COMPASS S798 msg 1746 ruling. Seam 2 of 4 in
/// the S802 Path 4 multi-seam split per COMPASS msg 1940. <c>Store</c>
/// suffix per S799 msg 1799 domain-role naming precedent.
/// Size: 3 methods.
/// </remarks>
internal interface ISchemaHistoryStore
{
	/// <summary>
	/// Writes a schema-version record to the history index. Returns
	/// <see langword="true"/> on success.
	/// </summary>
	Task<bool> WriteSchemaVersionAsync(
		string indexName,
		string documentId,
		SchemaHistoryRecord record,
		CancellationToken cancellationToken);

	/// <summary>
	/// Queries history records for a projection type, ordered by registration
	/// time ascending. Returns an empty list when the index does not exist or
	/// the query returns no documents.
	/// </summary>
	Task<IReadOnlyList<SchemaHistoryRecord>> QueryHistoryAsync(
		string indexName,
		string projectionType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Idempotently ensures the schema-history tracking index exists with
	/// the framework-owned mapping. No-op when the index already exists.
	/// </summary>
	Task<bool> EnsureHistoryIndexAsync(
		string indexName,
		CancellationToken cancellationToken);
}

/// <summary>
/// Framework-owned schema-history record. Domain shape — no SDK types
/// cross the seam.
/// </summary>
internal sealed record SchemaHistoryRecord
{
	public required string ProjectionType { get; init; }

	public required string Version { get; init; }

	public required string SchemaJson { get; init; }

	public required DateTimeOffset RegisteredAt { get; init; }

	public string? Description { get; init; }

	public string? MigrationNotes { get; init; }
}
