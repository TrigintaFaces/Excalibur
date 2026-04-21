// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="IIndexInspection"/> implementation that forwards
/// to <c>_inner.CountAsync</c> and <c>_inner.SearchAsync</c> on a real
/// <see cref="ElasticsearchClient"/>.
/// </summary>
internal sealed class IndexInspectionAdapter : IIndexInspection
{
	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="IndexInspectionAdapter"/> class.
	/// </summary>
	public IndexInspectionAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<long?> CountDocumentsAsync(
		string indexName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		// ES 9 returns a valid count=0 response even for non-existent indices.
		// Pre-check existence so callers can distinguish "empty index" from
		// "index does not exist" (null contract).
		var exists = await _inner.Indices
			.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);

		if (!exists.Exists)
		{
			return null;
		}

		var response = await _inner.CountAsync<object>(
				c => c.Indices(indexName),
				cancellationToken)
			.ConfigureAwait(false);

		return response.IsValidResponse ? response.Count : null;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> SampleDocumentIdsAsync(
		string indexName,
		int sampleSize,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		var response = await _inner.SearchAsync<object>(
				s => s.Index(indexName).Size(sampleSize),
				cancellationToken)
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Hits is null)
		{
			return [];
		}

		var ids = new List<string>(response.Hits.Count);
		foreach (var hit in response.Hits)
		{
			ids.Add(hit.Id ?? string.Empty);
		}

		return ids;
	}
}
