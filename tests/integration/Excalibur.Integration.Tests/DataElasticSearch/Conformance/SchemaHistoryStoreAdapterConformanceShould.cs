// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="SchemaHistoryStoreAdapter"/>.
/// Verifies the S802-A1 γ seam 5/6 Path-4 split (<c>5cd1ad337</c>) faithfully
/// passes through to the underlying <see cref="ElasticsearchClient"/>.
/// Exercises <see cref="ISchemaHistoryStore.EnsureHistoryIndexAsync"/> +
/// <see cref="ISchemaHistoryStore.WriteSchemaVersionAsync"/> +
/// <see cref="ISchemaHistoryStore.QueryHistoryAsync"/> round-trip against a
/// real Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter.
/// Closes the S802 NB-1 coverage gap (<c>bd-hc915h</c>) per OVERWATCH msg 1959.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class SchemaHistoryStoreAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly SchemaHistoryStoreAdapter _adapter;
	private bool _disposed;

	public SchemaHistoryStoreAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new SchemaHistoryStoreAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new SchemaHistoryStoreAdapter(null!));

	[Fact]
	public async Task EnsureWriteQuery_RoundTripsAgainstRealClient()
	{
		// Arrange
		var indexName = $"conformance-schemahistory-{Guid.NewGuid():N}";
		const string projectionType = "ConformanceProjection";
		var record = new SchemaHistoryRecord
		{
			ProjectionType = projectionType,
			Version = "1.0.0",
			SchemaJson = """{"field":"keyword"}""",
			RegisteredAt = DateTimeOffset.UtcNow,
			Description = "Conformance smoke",
		};
		var documentId = $"{projectionType}:1.0.0";

		try
		{
			// Act 1 — EnsureHistoryIndexAsync creates the history index.
			var ensured = await _adapter
				.EnsureHistoryIndexAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
			ensured.ShouldBeTrue();

			// Act 2 — WriteSchemaVersionAsync persists a record.
			var written = await _adapter
				.WriteSchemaVersionAsync(indexName, documentId, record, CancellationToken.None)
				.ConfigureAwait(false);
			written.ShouldBeTrue();

			// Refresh so the query sees the write without racing.
			_ = await _client.Indices.RefreshAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);

			// Act 3 — QueryHistoryAsync returns the record for that projection type.
			var records = await _adapter
				.QueryHistoryAsync(indexName, projectionType, CancellationToken.None)
				.ConfigureAwait(false);

			// Assert
			records.ShouldNotBeEmpty();
			records[0].ProjectionType.ShouldBe(projectionType);
			records[0].Version.ShouldBe("1.0.0");
		}
		finally
		{
			_ = await _client.Indices.DeleteAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task QueryHistoryAsync_ReturnsEmptyForMissingIndex()
	{
		// Arrange — index that does not exist.
		var indexName = $"conformance-schemahistory-missing-{Guid.NewGuid():N}";

		// Act — adapter must soft-fail to an empty list.
		var records = await _adapter
			.QueryHistoryAsync(indexName, "anything", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		records.ShouldBeEmpty();
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
