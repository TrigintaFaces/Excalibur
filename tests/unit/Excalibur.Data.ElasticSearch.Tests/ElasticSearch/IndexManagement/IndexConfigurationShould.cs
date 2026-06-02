// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify index configuration properties.
/// Updated Sprint 837: SDK types replaced with JsonElement? (bd-b9dvfl).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void SettingsJson_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.SettingsJson.ShouldBeNull();
	}

	[Fact]
	public void MappingsJson_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.MappingsJson.ShouldBeNull();
	}

	[Fact]
	public void AliasesJson_DefaultsToNull()
	{
		// Arrange & Act
		var config = new IndexConfiguration();

		// Assert
		config.AliasesJson.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		using var settingsDoc = JsonDocument.Parse("""{"number_of_shards":1}""");
		using var mappingsDoc = JsonDocument.Parse("""{"properties":{}}""");
		using var aliasesDoc = JsonDocument.Parse("""{"events-read":{}}""");

		// Act
		var config = new IndexConfiguration
		{
			SettingsJson = settingsDoc.RootElement.Clone(),
			MappingsJson = mappingsDoc.RootElement.Clone(),
			AliasesJson = aliasesDoc.RootElement.Clone(),
		};

		// Assert
		config.SettingsJson.ShouldNotBeNull();
		config.MappingsJson.ShouldNotBeNull();
		config.AliasesJson.ShouldNotBeNull();
	}

	#endregion

	#region Aliases Tests

	[Fact]
	public void AliasesJson_CanContainMultipleEntries()
	{
		// Arrange
		using var aliasesDoc = JsonDocument.Parse("""{"events-read":{},"events-write":{},"events-latest":{}}""");

		// Act
		var config = new IndexConfiguration
		{
			AliasesJson = aliasesDoc.RootElement.Clone(),
		};

		// Assert
		config.AliasesJson.ShouldNotBeNull();
		var obj = config.AliasesJson.Value;
		obj.ValueKind.ShouldBe(JsonValueKind.Object);
		obj.TryGetProperty("events-read", out _).ShouldBeTrue();
		obj.TryGetProperty("events-write", out _).ShouldBeTrue();
		obj.TryGetProperty("events-latest", out _).ShouldBeTrue();
	}

	[Fact]
	public void AliasesJson_CanBeEmptyObject()
	{
		// Arrange
		using var aliasesDoc = JsonDocument.Parse("{}");

		// Act
		var config = new IndexConfiguration
		{
			AliasesJson = aliasesDoc.RootElement.Clone(),
		};

		// Assert
		config.AliasesJson.ShouldNotBeNull();
		config.AliasesJson.Value.ValueKind.ShouldBe(JsonValueKind.Object);
	}

	#endregion

	#region Minimal Configuration Tests

	[Fact]
	public void MinimalConfiguration_HasNoRequiredProperties()
	{
		// Arrange & Act - All properties are optional
		var config = new IndexConfiguration();

		// Assert
		config.SettingsJson.ShouldBeNull();
		config.MappingsJson.ShouldBeNull();
		config.AliasesJson.ShouldBeNull();
	}

	#endregion
}
