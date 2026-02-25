// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.IndexManagement;
using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify index configuration properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void Settings_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.Settings.ShouldBeNull();
	}

	[Fact]
	public void Mappings_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.Mappings.ShouldBeNull();
	}

	[Fact]
	public void Aliases_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.Aliases.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var aliases = new Dictionary<string, Alias>
		{
			["events-read"] = new Alias()
		};

		// Act
		var config = new IndexConfiguration
		{
			Settings = new IndexSettings(),
			Mappings = new Elastic.Clients.Elasticsearch.Mapping.TypeMapping(),
			Aliases = aliases
		};

		// Assert
		config.Settings.ShouldNotBeNull();
		config.Mappings.ShouldNotBeNull();
		config.Aliases.ShouldNotBeNull();
		config.Aliases.Count.ShouldBe(1);
	}

	#endregion

	#region Aliases Tests

	[Fact]
	public void Aliases_CanContainMultipleEntries()
	{
		// Arrange
		var aliases = new Dictionary<string, Alias>
		{
			["events-read"] = new Alias(),
			["events-write"] = new Alias(),
			["events-latest"] = new Alias()
		};

		// Act
		var config = new IndexConfiguration
		{
			Aliases = aliases
		};

		// Assert
		config.Aliases.Count.ShouldBe(3);
		config.Aliases.ContainsKey("events-read").ShouldBeTrue();
		config.Aliases.ContainsKey("events-write").ShouldBeTrue();
		config.Aliases.ContainsKey("events-latest").ShouldBeTrue();
	}

	[Fact]
	public void Aliases_CanBeEmpty()
	{
		// Arrange & Act
		var config = new IndexConfiguration
		{
			Aliases = new Dictionary<string, Alias>()
		};

		// Assert
		config.Aliases.ShouldNotBeNull();
		config.Aliases.Count.ShouldBe(0);
	}

	#endregion

	#region Minimal Configuration Tests

	[Fact]
	public void MinimalConfiguration_HasNoRequiredProperties()
	{
		// Arrange & Act - All properties are optional
		var config = new IndexConfiguration();

		// Assert
		config.Settings.ShouldBeNull();
		config.Mappings.ShouldBeNull();
		config.Aliases.ShouldBeNull();
	}

	#endregion
}
