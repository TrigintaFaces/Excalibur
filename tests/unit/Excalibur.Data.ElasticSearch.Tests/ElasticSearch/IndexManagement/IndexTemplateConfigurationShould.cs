// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.IndexManagement;
using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexTemplateConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify index template configuration properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexTemplateConfigurationShould
{
	#region Required Property Tests

	[Fact]
	public void IndexPatterns_IsRequired()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*", "metrics-*"]
		};

		// Assert
		config.IndexPatterns.Count().ShouldBe(2);
		config.IndexPatterns.ShouldContain("logs-*");
		config.IndexPatterns.ShouldContain("metrics-*");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Priority_DefaultsTo100()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.Priority.ShouldBe(100);
	}

	[Fact]
	public void Version_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.Version.ShouldBeNull();
	}

	[Fact]
	public void Template_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.Template.ShouldBeNull();
	}

	[Fact]
	public void Mappings_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.Mappings.ShouldBeNull();
	}

	[Fact]
	public void ComposedOf_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.ComposedOf.ShouldBeNull();
	}

	[Fact]
	public void DataStream_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.DataStream.ShouldBeNull();
	}

	[Fact]
	public void Metadata_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowAutoCreate_DefaultsToTrue()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"]
		};

		// Assert
		config.AllowAutoCreate.ShouldBeTrue();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var metadata = new Dictionary<string, object?>
		{
			["author"] = "admin"
		};

		// Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*"],
			Priority = 200,
			Version = 1,
			Template = new IndexSettings(),
			Mappings = new Elastic.Clients.Elasticsearch.Mapping.TypeMapping(),
			ComposedOf = ["common-settings", "common-mappings"],
			DataStream = new DataStreamConfiguration(),
			Metadata = metadata,
			AllowAutoCreate = false
		};

		// Assert
		config.IndexPatterns.ShouldContain("logs-*");
		config.Priority.ShouldBe(200);
		config.Version.ShouldBe(1);
		config.Template.ShouldNotBeNull();
		config.Mappings.ShouldNotBeNull();
		config.ComposedOf.Count().ShouldBe(2);
		config.DataStream.ShouldNotBeNull();
		config.Metadata.Count.ShouldBe(1);
		config.AllowAutoCreate.ShouldBeFalse();
	}

	#endregion

	#region Index Patterns Tests

	[Fact]
	public void IndexPatterns_CanContainMultiplePatterns()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*", "metrics-*", "traces-*", "events-*"]
		};

		// Assert
		config.IndexPatterns.Count().ShouldBe(4);
	}

	[Fact]
	public void IndexPatterns_CanContainSinglePattern()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*"]
		};

		// Assert
		config.IndexPatterns.Count().ShouldBe(1);
	}

	#endregion

	#region Priority Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(500)]
	public void Priority_AcceptsVariousValues(int priority)
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"],
			Priority = priority
		};

		// Assert
		config.Priority.ShouldBe(priority);
	}

	#endregion

	#region ComposedOf Tests

	[Fact]
	public void ComposedOf_CanContainMultipleComponents()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*"],
			ComposedOf = ["settings-component", "mappings-component", "lifecycle-component"]
		};

		// Assert
		config.ComposedOf.Count().ShouldBe(3);
		config.ComposedOf.ShouldContain("settings-component");
	}

	#endregion

	#region Data Stream Template Tests

	[Fact]
	public void DataStreamTemplate_HasConfiguration()
	{
		// Arrange & Act
		var config = new IndexTemplateConfiguration
		{
			IndexPatterns = ["logs-*"],
			DataStream = new DataStreamConfiguration
			{
				Hidden = false,
				AllowCustomRouting = true
			}
		};

		// Assert
		config.DataStream.ShouldNotBeNull();
		config.DataStream.Hidden.ShouldBe(false);
		config.DataStream.AllowCustomRouting.ShouldBe(true);
	}

	#endregion
}
