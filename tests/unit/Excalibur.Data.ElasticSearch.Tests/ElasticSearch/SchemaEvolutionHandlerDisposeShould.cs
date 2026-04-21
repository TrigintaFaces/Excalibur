// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for <see cref="SchemaEvolutionHandler"/> Dispose functionality.
/// Verifies Sprint 389 fix: Dispose method properly releases resources.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SchemaEvolutionHandlerDisposeShould : UnitTestBase
{
	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Arrange
		var handler = CreateHandler();

		// Act & Assert - Should not throw
		var exception = Record.Exception(() => handler.Dispose());
		exception.ShouldBeNull();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var handler = CreateHandler();

		// Act - Call Dispose multiple times (should be idempotent)
		var exception = Record.Exception(() =>
		{
			handler.Dispose();
			handler.Dispose();
			handler.Dispose();
		});

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void Constructor_ThrowsOnNullClient()
	{
		// Arrange
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(null!, aliasManager, options, logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullAliasManager()
	{
		// Arrange
		// S799: real unconnected ElasticsearchClient per ADR-142 §D7.
		var client = new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(client, null!, options, logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Arrange
		// S799: real unconnected ElasticsearchClient per ADR-142 §D7.
		var client = new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
		var aliasManager = A.Fake<IIndexAliasManager>();
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(client, aliasManager, null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Arrange
		// S799: real unconnected ElasticsearchClient per ADR-142 §D7.
		var client = new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(client, aliasManager, options, null!));
	}

	[Fact]
	public void InternalConstructor_ThrowsOnNullOps()
	{
		// Arrange — S802 seam 5/6 Path 4 split: 4 internal seams.
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(
				null!,
				A.Fake<ISchemaHistoryStore>(),
				A.Fake<IMigrationHistoryStore>(),
				A.Fake<IIndexInspection>(),
				aliasManager,
				options,
				logger));
	}

	[Fact]
	public void InternalConstructor_ThrowsOnNullHistory()
	{
		// Arrange
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(
				A.Fake<ISchemaEvolutionOperations>(),
				null!,
				A.Fake<IMigrationHistoryStore>(),
				A.Fake<IIndexInspection>(),
				aliasManager,
				options,
				logger));
	}

	[Fact]
	public void InternalConstructor_ThrowsOnNullMigrationHistory()
	{
		// Arrange
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(
				A.Fake<ISchemaEvolutionOperations>(),
				A.Fake<ISchemaHistoryStore>(),
				null!,
				A.Fake<IIndexInspection>(),
				aliasManager,
				options,
				logger));
	}

	[Fact]
	public void InternalConstructor_ThrowsOnNullInspection()
	{
		// Arrange
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(
				A.Fake<ISchemaEvolutionOperations>(),
				A.Fake<ISchemaHistoryStore>(),
				A.Fake<IMigrationHistoryStore>(),
				null!,
				aliasManager,
				options,
				logger));
	}

	private static SchemaEvolutionHandler CreateHandler()
	{
		// S799: real unconnected ElasticsearchClient per ADR-142 §D7.
		var client = new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions
		{
			IndexPrefix = "test",
			SchemaEvolution = new SchemaEvolutionOptions
			{
				AllowBreakingChanges = false
			}
		});
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();

		return new SchemaEvolutionHandler(client, aliasManager, options, logger);
	}
}
