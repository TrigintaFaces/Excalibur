// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="IndexInspectionAdapter"/>.
/// Verifies the S802-A1 γ seam 5/6 Path-4 split (<c>aea82f45f</c>) faithfully
/// passes through to the underlying <see cref="ElasticsearchClient"/>.
/// Exercises <see cref="IIndexInspection.CountDocumentsAsync"/> +
/// <see cref="IIndexInspection.SampleDocumentIdsAsync"/> against a real
/// Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract. Closes the S802 NB-1 coverage gap
/// (<c>bd-hc915h</c>) per OVERWATCH msg 1959.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class IndexInspectionAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly IndexInspectionAdapter _adapter;
	private bool _disposed;

	public IndexInspectionAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new IndexInspectionAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new IndexInspectionAdapter(null!));

	[Fact]
	public async Task CountDocumentsAsync_ReturnsZeroForFreshIndex()
	{
		// Arrange
		var indexName = $"conformance-inspect-{Guid.NewGuid():N}";
		_ = await _client.Indices.CreateAsync(indexName, CancellationToken.None)
			.ConfigureAwait(false);

		try
		{
			// Act — adapter routes to _inner.CountAsync<object>.
			var count = await _adapter
				.CountDocumentsAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);

			// Assert — fresh index → zero docs (not null).
			count.ShouldBe(0L);
		}
		finally
		{
			_ = await _client.Indices.DeleteAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CountDocumentsAsync_ReturnsNullForMissingIndex()
	{
		// Arrange — index that does not exist.
		var indexName = $"conformance-inspect-missing-{Guid.NewGuid():N}";

		// Act — adapter soft-fails to null on failure, not throw.
		var count = await _adapter
			.CountDocumentsAsync(indexName, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		count.ShouldBeNull();
	}

	[Fact]
	public async Task SampleDocumentIdsAsync_ReturnsEmptyForMissingIndex()
	{
		// Arrange
		var indexName = $"conformance-inspect-sample-missing-{Guid.NewGuid():N}";

		// Act — adapter soft-fails on missing index.
		var ids = await _adapter
			.SampleDocumentIdsAsync(indexName, sampleSize: 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — empty list is the contract.
		ids.ShouldBeEmpty();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}
}
