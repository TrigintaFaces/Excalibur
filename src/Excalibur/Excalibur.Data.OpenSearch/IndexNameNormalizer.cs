// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Normalizes OpenSearch index names. OpenSearch rejects any index name containing an uppercase
/// character with a 400 <c>invalid_index_name_exception</c> ("must be lowercase"), so every name
/// composed from consumer-supplied prefixes/names or environment-derived segments must be
/// lowercased before it reaches the client.
/// </summary>
internal static class IndexNameNormalizer
{
	/// <summary>
	/// Lowercases the supplied index name so it is a valid OpenSearch index name.
	/// </summary>
	/// <param name="indexName">The composed index name.</param>
	/// <returns>The lowercased index name, or the input unchanged when null/empty.</returns>
#pragma warning disable CA1308 // OpenSearch index names must be lowercase
	internal static string Normalize(string indexName) =>
		string.IsNullOrEmpty(indexName) ? indexName : indexName.ToLowerInvariant();
#pragma warning restore CA1308
}
