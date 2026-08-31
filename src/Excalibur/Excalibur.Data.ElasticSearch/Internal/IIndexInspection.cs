// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Narrow internal seam over <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/>
/// document-count + sample inspection used by
/// <see cref="Excalibur.Data.ElasticSearch.Projections.SchemaEvolutionHandler"/>.
/// Hides <c>CountAsync</c> + <c>SearchAsync</c> behind domain-shaped
/// helpers. Not a consumer-facing abstraction; do not make this public.
/// </summary>
/// <remarks>
/// The <c>Inspection</c> suffix names the consumer's domain role and avoids
/// drifting into OpenTelemetry "Metrics" vocabulary. Size: 2 methods.
/// </remarks>
internal interface IIndexInspection
{
	/// <summary>
	/// Returns the document count for <paramref name="indexName"/>, or
	/// <see langword="null"/> when the count call did not succeed.
	/// </summary>
	Task<long?> CountDocumentsAsync(
		string indexName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Returns up to <paramref name="sampleSize"/> document IDs from
	/// <paramref name="indexName"/>. Returns an empty list when the index
	/// is missing or the search call did not succeed.
	/// </summary>
	Task<IReadOnlyList<string>> SampleDocumentIdsAsync(
		string indexName,
		int sampleSize,
		CancellationToken cancellationToken);
}
