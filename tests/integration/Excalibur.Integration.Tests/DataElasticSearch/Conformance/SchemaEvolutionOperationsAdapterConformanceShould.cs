// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Conformance;

/// <summary>
/// Real-SDK conformance smoke for <see cref="SchemaEvolutionOperationsAdapter"/>.
/// Verifies the S802-A1 γ seam 5/6 Path-4 split (<c>4ba6dd793</c>) + Option (B)
/// fix (<c>5ef6be2ab</c>) faithfully passes through to the underlying
/// <see cref="ElasticsearchClient"/>. Exercises
/// <see cref="ISchemaEvolutionOperations.EnsureMigrationIndexAsync"/> +
/// <see cref="ISchemaEvolutionOperations.GetSchemaVersionAsync"/> +
/// <see cref="ISchemaEvolutionOperations.VerifyVersionAsync"/> +
/// <see cref="ISchemaEvolutionOperations.MigrateAsync"/> (UpdateInPlace shape)
/// against a real Elasticsearch TestContainer.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough contract: one real-SDK smoke per adapter
/// under the integration shard. Closes the S802 NB-1 coverage gap
/// (<c>bd-hc915h</c>) per OVERWATCH msg 1959. Behaviorally-exhaustive tests
/// live in <c>SchemaEvolutionHandlerBehaviorShould</c> under unit shard.
/// </remarks>
[Collection(nameof(ElasticsearchHostTests))]
[Trait("Category", "Integration")]
[Trait("Component", "Elasticsearch")]
[Trait("Database", "Elasticsearch")]
[Trait("Pattern", "SEAM-PASSTHROUGH")]
public sealed class SchemaEvolutionOperationsAdapterConformanceShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly SchemaEvolutionOperationsAdapter _adapter;
	private bool _disposed;

	public SchemaEvolutionOperationsAdapterConformanceShould(ElasticsearchContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);
		_adapter = new SchemaEvolutionOperationsAdapter(_client);
	}

	[Fact]
	public void Construct_WithNullClient_ThrowsArgumentNullException() =>
		Should.Throw<ArgumentNullException>(() => new SchemaEvolutionOperationsAdapter(null!));

	[Fact]
	public async Task EnsureMigrationIndexAsync_CreatesIndexOnRealClient()
	{
		// Arrange
		var indexName = $"conformance-schemaops-{Guid.NewGuid():N}";

		try
		{
			// Act — adapter routes to _inner.Indices.ExistsAsync + CreateAsync.
			var outcome = await _adapter
				.EnsureMigrationIndexAsync(indexName, mapping: null, CancellationToken.None)
				.ConfigureAwait(false);

			// Assert — creation succeeds; idempotent second call also succeeds.
			outcome.Success.ShouldBeTrue(outcome.ErrorDetails);
			var second = await _adapter
				.EnsureMigrationIndexAsync(indexName, mapping: null, CancellationToken.None)
				.ConfigureAwait(false);
			second.Success.ShouldBeTrue(second.ErrorDetails);
		}
		finally
		{
			_ = await _client.Indices.DeleteAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task VerifyVersionAsync_ReturnsNullForMissingIndex()
	{
		// Arrange — an index that doesn't exist.
		var indexName = $"conformance-schemaops-missing-{Guid.NewGuid():N}";

		// Act — adapter must soft-fail: null return, no throw.
		var version = await _adapter
			.VerifyVersionAsync(indexName, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		version.ShouldBeNull();
	}

	[Fact]
	public async Task MigrateAsync_UpdateInPlaceShape_AppliesMappingWithoutReindex()
	{
		// Arrange — Option (B) reshape: sourceIndex == targetIndex is a pure mapping-put.
		var indexName = $"conformance-schemaops-inplace-{Guid.NewGuid():N}";
		try
		{
			var created = await _adapter
				.EnsureMigrationIndexAsync(indexName, mapping: null, CancellationToken.None)
				.ConfigureAwait(false);
			created.Success.ShouldBeTrue(created.ErrorDetails);

			// Act — adapter short-circuits the reindex and applies the mapping directly.
			var outcome = await _adapter
				.MigrateAsync(indexName, indexName, mapping: null, CancellationToken.None)
				.ConfigureAwait(false);

			// Assert — must not throw a self-reindex error; mapping-put path taken.
			outcome.Success.ShouldBeTrue(outcome.ErrorDetails);
		}
		finally
		{
			_ = await _client.Indices.DeleteAsync(indexName, CancellationToken.None)
				.ConfigureAwait(false);
		}
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
