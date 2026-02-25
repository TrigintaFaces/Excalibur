// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexOptimizationOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify default values and property initialization.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexOptimizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void OptimizeRefreshInterval_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions();

		// Assert
		options.OptimizeRefreshInterval.ShouldBeTrue();
	}

	[Fact]
	public void OptimizeReplicaCount_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions();

		// Assert
		options.OptimizeReplicaCount.ShouldBeTrue();
	}

	[Fact]
	public void ForceMerge_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions();

		// Assert
		options.ForceMerge.ShouldBeFalse();
	}

	[Fact]
	public void TargetSegmentCount_DefaultsToNull()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions();

		// Assert
		options.TargetSegmentCount.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions
		{
			OptimizeRefreshInterval = false,
			OptimizeReplicaCount = false,
			ForceMerge = true,
			TargetSegmentCount = 1
		};

		// Assert
		options.OptimizeRefreshInterval.ShouldBeFalse();
		options.OptimizeReplicaCount.ShouldBeFalse();
		options.ForceMerge.ShouldBeTrue();
		options.TargetSegmentCount.ShouldBe(1);
	}

	#endregion

	#region Target Segment Count Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void TargetSegmentCount_AcceptsVariousValues(int count)
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions { TargetSegmentCount = count };

		// Assert
		options.TargetSegmentCount.ShouldBe(count);
	}

	#endregion

	#region Force Merge Configuration Tests

	[Fact]
	public void ForceMerge_CanBeEnabledWithoutTargetCount()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions
		{
			ForceMerge = true
		};

		// Assert
		options.ForceMerge.ShouldBeTrue();
		options.TargetSegmentCount.ShouldBeNull();
	}

	[Fact]
	public void ForceMerge_CanBeEnabledWithTargetCount()
	{
		// Arrange & Act
		var options = new IndexOptimizationOptions
		{
			ForceMerge = true,
			TargetSegmentCount = 1
		};

		// Assert
		options.ForceMerge.ShouldBeTrue();
		options.TargetSegmentCount.ShouldBe(1);
	}

	#endregion
}
