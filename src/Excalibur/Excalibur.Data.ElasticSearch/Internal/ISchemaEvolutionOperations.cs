// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// reindex + mapping endpoints used by
/// <see cref="Excalibur.Data.ElasticSearch.Projections.SchemaEvolutionHandler"/>.
/// Exposes the <b>migration workflow</b> (migrate forward + rollback +
/// version inspection + index ensure) — not SDK factory shape — so tests can
/// substitute the SDK without depending on which <c>Indices</c> /
/// <c>ReindexAsync</c> / <c>PutMappingAsync</c> overloads remain virtual in a
/// given SDK minor version. Not a consumer-facing abstraction; do not make
/// this public.
/// </summary>
/// <remarks>
/// Fifth of the 6 γ seams per COMPASS S798 msg 1746 ruling + S799 msg 1799
/// naming precedent. Seam 1 of 4 in the S802 Path 4 multi-seam split per
/// COMPASS msg 1940. <c>Operations</c> suffix describes the consumer's domain
/// role — the migration workflow — rather than an SDK sub-client mirror.
/// Size: 5 methods, at the ADR-142 §D7 hard cap.
///
/// <para>
/// The <c>mapping</c> parameters are typed as <see cref="object"/> so SDK
/// types (<c>TypeMapping</c>, <c>Properties</c>) do not cross the seam.
/// Adapters cast internally and treat unrecognized shapes as no-mapping.
/// </para>
/// </remarks>
internal interface ISchemaEvolutionOperations
{
	/// <summary>
	/// Reindexes documents from <paramref name="sourceIndex"/> to
	/// <paramref name="targetIndex"/>, optionally applying a new mapping
	/// after the reindex completes.
	/// </summary>
	Task<MigrationStepOutcome> MigrateAsync(
		string sourceIndex,
		string targetIndex,
		object? mapping,
		CancellationToken cancellationToken);

	/// <summary>
	/// Inspects an index's current schema-version stamp, if any. Returns
	/// <see langword="null"/> when the index is missing or has no recorded
	/// version.
	/// </summary>
	Task<SchemaVersion?> VerifyVersionAsync(
		string indexName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reverts a forward migration by reindexing
	/// <paramref name="targetIndex"/> back into <paramref name="sourceIndex"/>,
	/// optionally re-applying a prior mapping.
	/// </summary>
	Task<MigrationStepOutcome> RollbackAsync(
		string sourceIndex,
		string targetIndex,
		object? mapping,
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns the current schema version for <paramref name="indexName"/>,
	/// defaulting to <c>"1.0.0"</c> when unstamped (preserves legacy behavior).
	/// The returned <see cref="SchemaVersion.MappingJson"/> carries the
	/// flattened field-type map so the consumer can perform schema diffs
	/// without seeing SDK mapping types.
	/// </summary>
	Task<SchemaVersion> GetSchemaVersionAsync(
		string indexName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Idempotently ensures <paramref name="indexName"/> exists with the
	/// supplied mapping (single shard, no replicas — internal migration index
	/// defaults). No-op when the index already exists.
	/// </summary>
	Task<MigrationStepOutcome> EnsureMigrationIndexAsync(
		string indexName,
		object? mapping,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of an <see cref="ISchemaEvolutionOperations"/> write operation.
/// Domain shape — no SDK types cross the seam.
/// </summary>
internal readonly record struct MigrationStepOutcome(
	bool Success,
	string? ErrorDetails);

/// <summary>
/// Schema-version snapshot for a single index. <see cref="MappingJson"/>
/// is a JSON-serialized field-type dictionary (<c>{ "fieldName": "TypeName" }</c>)
/// extracted by the adapter so the consumer can compare schemas without
/// referencing SDK mapping types.
/// </summary>
internal readonly record struct SchemaVersion(
	string Version,
	string? MappingJson);
