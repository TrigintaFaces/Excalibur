// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// The one place that decides whether a create-index request left the index in place.
/// </summary>
internal static class ElasticIndexProvisioning
{
	private const string AlreadyExists = "resource_already_exists_exception";

	/// <summary>
	/// Determines whether the create request failed and the index is therefore absent, so the caller must
	/// not carry on as though its declared mapping and settings had been applied.
	/// </summary>
	/// <param name="response"> The response to the create-index request. </param>
	/// <returns>
	/// <see langword="true" /> when the index was neither created nor already present; otherwise
	/// <see langword="false" />.
	/// </returns>
	/// <remarks>
	/// A create refused because the index already exists is not a failure. Two instances starting together
	/// both see the index absent and both attempt it; the loser is told it already exists, which is the
	/// state it wanted. Every creator shares this predicate so that exemption cannot be present in one and
	/// missing from another.
	/// </remarks>
	internal static bool Failed(CreateIndexResponse response)
	{
		ArgumentNullException.ThrowIfNull(response);

		return !response.IsValidResponse
			&& !response.Acknowledged
			&& !string.Equals(response.ElasticsearchServerError?.Error?.Type, AlreadyExists, StringComparison.Ordinal);
	}
}
