// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Normalizes Elasticsearch index names. Elasticsearch rejects any index name containing an
/// uppercase character with a 400 <c>invalid_index_name_exception</c> ("must be lowercase"), so
/// every name composed from consumer-supplied prefixes/names or environment-derived segments must
/// be lowercased before it reaches the client.
/// </summary>
internal static class IndexNameNormalizer
{
	/// <summary>
	/// Lowercases the supplied index name so it is a valid Elasticsearch index name.
	/// </summary>
	/// <param name="indexName">The composed index name.</param>
	/// <returns>The lowercased index name, or the input unchanged when null/empty.</returns>
	internal static string Normalize(string indexName) =>
		string.IsNullOrEmpty(indexName) ? indexName : indexName.ToLowerInvariant();
}
