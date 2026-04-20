// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="MigrationHistoryStoreAdapter"/>.
/// Verifies the S802-A1 γ seam 5/6 Path-4 split (<c>626e019a5</c>) faithfully
/// passes through to the underlying <see cref="ElasticsearchClient"/>.
/// Exercises <see cref="IMigrationHistoryStore.EnsureHistoryIndexAsync"/> +
/// <see cref="IMigrationHistoryStore.WriteMigrationResultAsync"/> +
/// <see cref="IMigrationHistoryStore.QueryHistoryAsync"/> round-trip against a
/// real Elasticsearch TestContainer.
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
public sealed class MigrationHistoryStoreAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly MigrationHistoryStoreAdapter _adapter;
	private bool _disposed;

	public MigrationHistoryStoreAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new MigrationHistoryStoreAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new MigrationHistoryStoreAdapter(null!));

	[Fact]
	public async Task EnsureWriteQuery_RoundTripsAgainstRealClient()
	{
		// Arrange
		var indexName = $"conformance-migrationhistory-{Guid.NewGuid():N}";
		const string projectionType = "ConformanceProjection";
		var record = new MigrationHistoryRecord
		{
			ProjectionType = projectionType,
			PlanId = Guid.NewGuid().ToString("N"),
			RecordedAt = DateTimeOffset.UtcNow,
			ResultJson = """{"Success":true}""",
		};
		var documentId = $"{projectionType}:{record.PlanId}";

		try
		{
			// Act 1 — EnsureHistoryIndexAsync creates the migration-history index.
			var ensured = await _adapter
				.EnsureHistoryIndexAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
			ensured.ShouldBeTrue();

			// Act 2 — WriteMigrationResultAsync persists the result.
			var written = await _adapter
				.WriteMigrationResultAsync(indexName, documentId, record, CancellationToken.None)
				.ConfigureAwait(false);
			written.ShouldBeTrue();

			_ = await _client.Indices.RefreshAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);

			// Act 3 — QueryHistoryAsync returns the record, ordered most-recent first.
			var records = await _adapter
				.QueryHistoryAsync(indexName, projectionType, CancellationToken.None)
				.ConfigureAwait(false);

			// Assert
			records.ShouldNotBeEmpty();
			records[0].ProjectionType.ShouldBe(projectionType);
			records[0].PlanId.ShouldBe(record.PlanId);
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
		// Arrange
		var indexName = $"conformance-migrationhistory-missing-{Guid.NewGuid():N}";

		// Act
		var records = await _adapter
			.QueryHistoryAsync(indexName, "anything", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — soft-fail contract: empty list, no throw.
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
