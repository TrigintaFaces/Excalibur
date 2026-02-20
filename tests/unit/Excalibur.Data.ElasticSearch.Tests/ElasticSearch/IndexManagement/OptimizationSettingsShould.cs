// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="OptimizationOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify optimization settings defaults and configurations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class OptimizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void AutoOptimize_DefaultsToTrue()
	{
		// Arrange & Act
		var settings = new OptimizationOptions();

		// Assert
		settings.AutoOptimize.ShouldBeTrue();
	}

	[Fact]
	public void MergePolicy_DefaultsToTiered()
	{
		// Arrange & Act
		var settings = new OptimizationOptions();

		// Assert
		settings.MergePolicy.ShouldBe("tiered");
	}

	[Fact]
	public void MaxSegmentsPerTier_DefaultsToTen()
	{
		// Arrange & Act
		var settings = new OptimizationOptions();

		// Assert
		settings.MaxSegmentsPerTier.ShouldBe(10);
	}

	[Fact]
	public void ForceMergeOnRollover_DefaultsToTrue()
	{
		// Arrange & Act
		var settings = new OptimizationOptions();

		// Assert
		settings.ForceMergeOnRollover.ShouldBeTrue();
	}

	[Fact]
	public void CompressionLevel_DefaultsToBestCompression()
	{
		// Arrange & Act
		var settings = new OptimizationOptions();

		// Assert
		settings.CompressionLevel.ShouldBe("best_compression");
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var settings = new OptimizationOptions
		{
			AutoOptimize = false,
			MergePolicy = "log_doc",
			MaxSegmentsPerTier = 5,
			ForceMergeOnRollover = false,
			CompressionLevel = "best_speed"
		};

		// Assert
		settings.AutoOptimize.ShouldBeFalse();
		settings.MergePolicy.ShouldBe("log_doc");
		settings.MaxSegmentsPerTier.ShouldBe(5);
		settings.ForceMergeOnRollover.ShouldBeFalse();
		settings.CompressionLevel.ShouldBe("best_speed");
	}

	#endregion

	#region Merge Policy Tests

	[Theory]
	[InlineData("tiered")]
	[InlineData("log_doc")]
	[InlineData("log_byte_size")]
	public void MergePolicy_AcceptsValidPolicies(string policy)
	{
		// Arrange & Act
		var settings = new OptimizationOptions
		{
			MergePolicy = policy
		};

		// Assert
		settings.MergePolicy.ShouldBe(policy);
	}

	#endregion

	#region Max Segments Per Tier Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(20)]
	public void MaxSegmentsPerTier_AcceptsVariousValues(int segments)
	{
		// Arrange & Act
		var settings = new OptimizationOptions
		{
			MaxSegmentsPerTier = segments
		};

		// Assert
		settings.MaxSegmentsPerTier.ShouldBe(segments);
	}

	#endregion

	#region Compression Level Tests

	[Theory]
	[InlineData("best_compression")]
	[InlineData("best_speed")]
	public void CompressionLevel_AcceptsValidLevels(string level)
	{
		// Arrange & Act
		var settings = new OptimizationOptions
		{
			CompressionLevel = level
		};

		// Assert
		settings.CompressionLevel.ShouldBe(level);
	}

	#endregion

	#region Performance vs Storage Tradeoff Tests

	[Fact]
	public void PerformanceOptimized_Settings()
	{
		// Arrange & Act - Settings optimized for performance
		var settings = new OptimizationOptions
		{
			AutoOptimize = true,
			MergePolicy = "tiered",
			MaxSegmentsPerTier = 5,
			ForceMergeOnRollover = true,
			CompressionLevel = "best_speed"
		};

		// Assert
		settings.CompressionLevel.ShouldBe("best_speed");
		settings.MaxSegmentsPerTier.ShouldBe(5); // Fewer segments = faster queries
	}

	[Fact]
	public void StorageOptimized_Settings()
	{
		// Arrange & Act - Settings optimized for storage
		var settings = new OptimizationOptions
		{
			AutoOptimize = true,
			MergePolicy = "tiered",
			MaxSegmentsPerTier = 10,
			ForceMergeOnRollover = true,
			CompressionLevel = "best_compression"
		};

		// Assert
		settings.CompressionLevel.ShouldBe("best_compression");
	}

	#endregion
}
