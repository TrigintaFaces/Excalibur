// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.IndexManagement;
using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="ComponentTemplateConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify component template configuration properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class ComponentTemplateConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void Version_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration();

		// Assert
		config.Version.ShouldBeNull();
	}

	[Fact]
	public void Template_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration();

		// Assert
		config.Template.ShouldBeNull();
	}

	[Fact]
	public void Mappings_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration();

		// Assert
		config.Mappings.ShouldBeNull();
	}

	[Fact]
	public void Metadata_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration();

		// Assert
		config.Metadata.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var metadata = new Dictionary<string, object?>
		{
			["author"] = "admin",
			["description"] = "Common settings"
		};

		// Act
		var config = new ComponentTemplateConfiguration
		{
			Version = 1,
			Template = new IndexSettings(),
			Mappings = new Elastic.Clients.Elasticsearch.Mapping.TypeMapping(),
			Metadata = metadata
		};

		// Assert
		config.Version.ShouldBe(1);
		config.Template.ShouldNotBeNull();
		config.Mappings.ShouldNotBeNull();
		config.Metadata.ShouldNotBeNull();
		config.Metadata.Count.ShouldBe(2);
	}

	#endregion

	#region Version Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void Version_AcceptsVariousValues(long version)
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration
		{
			Version = version
		};

		// Assert
		config.Version.ShouldBe(version);
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void Metadata_CanContainMultipleEntries()
	{
		// Arrange
		var metadata = new Dictionary<string, object?>
		{
			["author"] = "admin",
			["created"] = "2026-01-01",
			["department"] = "engineering",
			["environment"] = "production"
		};

		// Act
		var config = new ComponentTemplateConfiguration
		{
			Metadata = metadata
		};

		// Assert
		config.Metadata.Count.ShouldBe(4);
		config.Metadata["author"].ShouldBe("admin");
	}

	[Fact]
	public void Metadata_CanBeEmpty()
	{
		// Arrange & Act
		var config = new ComponentTemplateConfiguration
		{
			Metadata = new Dictionary<string, object?>()
		};

		// Assert
		config.Metadata.ShouldNotBeNull();
		config.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void Metadata_CanContainNullValues()
	{
		// Arrange
		var metadata = new Dictionary<string, object?>
		{
			["key"] = null
		};

		// Act
		var config = new ComponentTemplateConfiguration
		{
			Metadata = metadata
		};

		// Assert
		config.Metadata.ContainsKey("key").ShouldBeTrue();
		config.Metadata["key"].ShouldBeNull();
	}

	#endregion

	#region Minimal Configuration Tests

	[Fact]
	public void MinimalConfiguration_HasNoRequiredProperties()
	{
		// Arrange & Act - All properties are optional
		var config = new ComponentTemplateConfiguration();

		// Assert
		config.Version.ShouldBeNull();
		config.Template.ShouldBeNull();
		config.Mappings.ShouldBeNull();
		config.Metadata.ShouldBeNull();
	}

	#endregion
}
