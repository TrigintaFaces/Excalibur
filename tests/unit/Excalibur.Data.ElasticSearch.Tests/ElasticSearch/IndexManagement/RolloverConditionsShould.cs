// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="RolloverConditions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify rollover condition properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class RolloverConditionsShould
{
	#region Default Value Tests

	[Fact]
	public void MaxAge_DefaultsToNull()
	{
		// Arrange & Act
		var conditions = new RolloverConditions();

		// Assert
		conditions.MaxAge.ShouldBeNull();
	}

	[Fact]
	public void MaxSize_DefaultsToNull()
	{
		// Arrange & Act
		var conditions = new RolloverConditions();

		// Assert
		conditions.MaxSize.ShouldBeNull();
	}

	[Fact]
	public void MaxDocs_DefaultsToNull()
	{
		// Arrange & Act
		var conditions = new RolloverConditions();

		// Assert
		conditions.MaxDocs.ShouldBeNull();
	}

	[Fact]
	public void MaxPrimaryShardSize_DefaultsToNull()
	{
		// Arrange & Act
		var conditions = new RolloverConditions();

		// Assert
		conditions.MaxPrimaryShardSize.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var conditions = new RolloverConditions
		{
			MaxAge = TimeSpan.FromDays(7),
			MaxSize = "50GB",
			MaxDocs = 1_000_000,
			MaxPrimaryShardSize = "25GB"
		};

		// Assert
		conditions.MaxAge.ShouldBe(TimeSpan.FromDays(7));
		conditions.MaxSize.ShouldBe("50GB");
		conditions.MaxDocs.ShouldBe(1_000_000);
		conditions.MaxPrimaryShardSize.ShouldBe("25GB");
	}

	#endregion

	#region MaxAge Tests

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	public void MaxAge_AcceptsVariousDurations(int days)
	{
		// Arrange
		var expected = TimeSpan.FromDays(days);

		// Act
		var conditions = new RolloverConditions { MaxAge = expected };

		// Assert
		conditions.MaxAge.ShouldBe(expected);
	}

	#endregion

	#region MaxSize Tests

	[Theory]
	[InlineData("1GB")]
	[InlineData("50GB")]
	[InlineData("100GB")]
	[InlineData("500MB")]
	public void MaxSize_AcceptsVariousSizeStrings(string size)
	{
		// Act
		var conditions = new RolloverConditions { MaxSize = size };

		// Assert
		conditions.MaxSize.ShouldBe(size);
	}

	#endregion

	#region MaxDocs Tests

	[Theory]
	[InlineData(1000)]
	[InlineData(100_000)]
	[InlineData(1_000_000)]
	[InlineData(long.MaxValue)]
	public void MaxDocs_AcceptsVariousCounts(long count)
	{
		// Act
		var conditions = new RolloverConditions { MaxDocs = count };

		// Assert
		conditions.MaxDocs.ShouldBe(count);
	}

	#endregion

	#region Partial Condition Tests

	[Fact]
	public void CanSetOnlyMaxAge()
	{
		// Arrange & Act
		var conditions = new RolloverConditions
		{
			MaxAge = TimeSpan.FromDays(7)
		};

		// Assert
		conditions.MaxAge.ShouldNotBeNull();
		conditions.MaxSize.ShouldBeNull();
		conditions.MaxDocs.ShouldBeNull();
		conditions.MaxPrimaryShardSize.ShouldBeNull();
	}

	[Fact]
	public void CanSetOnlyMaxSize()
	{
		// Arrange & Act
		var conditions = new RolloverConditions
		{
			MaxSize = "50GB"
		};

		// Assert
		conditions.MaxAge.ShouldBeNull();
		conditions.MaxSize.ShouldNotBeNull();
		conditions.MaxDocs.ShouldBeNull();
		conditions.MaxPrimaryShardSize.ShouldBeNull();
	}

	[Fact]
	public void CanSetMultipleConditions()
	{
		// Arrange & Act
		var conditions = new RolloverConditions
		{
			MaxAge = TimeSpan.FromDays(7),
			MaxSize = "50GB"
		};

		// Assert
		conditions.MaxAge.ShouldNotBeNull();
		conditions.MaxSize.ShouldNotBeNull();
		conditions.MaxDocs.ShouldBeNull();
	}

	#endregion
}
