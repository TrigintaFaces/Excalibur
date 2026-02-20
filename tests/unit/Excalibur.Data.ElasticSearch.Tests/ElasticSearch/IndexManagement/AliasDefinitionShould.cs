// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="AliasDefinition"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify required and optional properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class AliasDefinitionShould
{
	#region Required Property Tests

	[Fact]
	public void AliasName_IsRequired()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "events-read",
			Indices = ["events-000001", "events-000002"]
		};

		// Assert
		alias.AliasName.ShouldBe("events-read");
	}

	[Fact]
	public void Indices_IsRequired()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "events-read",
			Indices = ["events-000001"]
		};

		// Assert
		alias.Indices.ShouldContain("events-000001");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Filter_DefaultsToNull()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "test",
			Indices = []
		};

		// Assert
		alias.Filter.ShouldBeNull();
	}

	[Fact]
	public void IndexRouting_DefaultsToNull()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "test",
			Indices = []
		};

		// Assert
		alias.IndexRouting.ShouldBeNull();
	}

	[Fact]
	public void SearchRouting_DefaultsToNull()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "test",
			Indices = []
		};

		// Assert
		alias.SearchRouting.ShouldBeNull();
	}

	[Fact]
	public void IsWriteIndex_DefaultsToNull()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "test",
			Indices = []
		};

		// Assert
		alias.IsWriteIndex.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "events-write",
			Indices = ["events-000001", "events-000002", "events-000003"],
			IndexRouting = "tenant-1",
			SearchRouting = "tenant-1",
			IsWriteIndex = true
		};

		// Assert
		alias.AliasName.ShouldBe("events-write");
		alias.Indices.Count().ShouldBe(3);
		alias.IndexRouting.ShouldBe("tenant-1");
		alias.SearchRouting.ShouldBe("tenant-1");
		alias.IsWriteIndex.ShouldBe(true);
	}

	#endregion

	#region Multiple Indices Tests

	[Fact]
	public void Indices_CanContainMultipleEntries()
	{
		// Arrange
		var indices = new[] { "events-000001", "events-000002", "events-000003" };

		// Act
		var alias = new AliasDefinition
		{
			AliasName = "events",
			Indices = indices
		};

		// Assert
		alias.Indices.Count().ShouldBe(3);
		alias.Indices.ShouldContain("events-000001");
		alias.Indices.ShouldContain("events-000002");
		alias.Indices.ShouldContain("events-000003");
	}

	[Fact]
	public void Indices_CanBeEmpty()
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "empty-alias",
			Indices = []
		};

		// Assert
		alias.Indices.ShouldBeEmpty();
	}

	#endregion

	#region Write Index Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void IsWriteIndex_CanBeSetExplicitly(bool isWriteIndex)
	{
		// Arrange & Act
		var alias = new AliasDefinition
		{
			AliasName = "test",
			Indices = ["index-1"],
			IsWriteIndex = isWriteIndex
		};

		// Assert
		alias.IsWriteIndex.ShouldBe(isWriteIndex);
	}

	#endregion
}
