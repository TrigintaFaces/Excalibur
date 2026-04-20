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
/// Fifth of the 6 γ seams per COMPASS S798 msg 1746 ruling. Seam 4 of 4 in
/// the S802 Path 4 multi-seam split per COMPASS msg 1940. <c>Inspection</c>
/// suffix selected per COMPASS msg 1940 to avoid drifting into OpenTelemetry
/// "Metrics" vocabulary. Size: 2 methods.
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
