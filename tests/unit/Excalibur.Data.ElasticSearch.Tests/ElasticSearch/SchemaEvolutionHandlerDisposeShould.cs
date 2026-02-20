// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for <see cref="SchemaEvolutionHandler"/> Dispose functionality.
/// Verifies Sprint 389 fix: Dispose method properly releases resources.
/// </summary>
[Trait("Category", "Unit")]
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
		var client = A.Fake<ElasticsearchClient>();
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
		var client = A.Fake<ElasticsearchClient>();
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
		var client = A.Fake<ElasticsearchClient>();
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SchemaEvolutionHandler(client, aliasManager, options, null!));
	}

	private static SchemaEvolutionHandler CreateHandler()
	{
		var client = A.Fake<ElasticsearchClient>();
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
