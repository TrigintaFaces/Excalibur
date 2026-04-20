// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// migration-result persistence used by
/// <see cref="Excalibur.Data.ElasticSearch.Projections.SchemaEvolutionHandler"/>.
/// Mirrors <see cref="ISchemaHistoryStore"/> but for migration result
/// records (most-recent-first ordering). Not a consumer-facing abstraction;
/// do not make this public.
/// </summary>
/// <remarks>
/// Fifth of the 6 γ seams per COMPASS S798 msg 1746 ruling. Seam 3 of 4 in
/// the S802 Path 4 multi-seam split per COMPASS msg 1940. Size: 3 methods.
/// </remarks>
internal interface IMigrationHistoryStore
{
	/// <summary>
	/// Writes a migration-result record to the migration-history index.
	/// </summary>
	Task<bool> WriteMigrationResultAsync(
		string indexName,
		string documentId,
		MigrationHistoryRecord record,
		CancellationToken cancellationToken);

	/// <summary>
	/// Queries migration-history records for a projection type, ordered by
	/// recorded time descending (most-recent first).
	/// </summary>
	Task<IReadOnlyList<MigrationHistoryRecord>> QueryHistoryAsync(
		string indexName,
		string projectionType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Idempotently ensures the migration-history tracking index exists
	/// with the framework-owned mapping.
	/// </summary>
	Task<bool> EnsureHistoryIndexAsync(
		string indexName,
		CancellationToken cancellationToken);
}

/// <summary>
/// Framework-owned migration-history record. <see cref="ResultJson"/>
/// carries the serialized <c>SchemaMigrationResult</c> so the consumer
/// can deserialize without exposing SDK types.
/// </summary>
internal sealed record MigrationHistoryRecord
{
	public required string ProjectionType { get; init; }

	public required string PlanId { get; init; }

	public required DateTimeOffset RecordedAt { get; init; }

	public required string ResultJson { get; init; }
}
