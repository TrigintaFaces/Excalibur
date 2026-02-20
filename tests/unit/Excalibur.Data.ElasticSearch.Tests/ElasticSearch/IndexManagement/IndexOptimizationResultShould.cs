// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexOptimizationResult"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify optimization result properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexOptimizationResultShould
{
	#region Required Property Tests

	[Fact]
	public void IsSuccessful_IsRequired()
	{
		// Arrange & Act
		var result = new IndexOptimizationResult { IsSuccessful = true };

		// Assert
		result.IsSuccessful.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void PerformedActions_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new IndexOptimizationResult { IsSuccessful = true };

		// Assert
		result.PerformedActions.ShouldBeEmpty();
	}

	[Fact]
	public void Errors_DefaultsToEmptyCollection()
	{
		// Arrange & Act
		var result = new IndexOptimizationResult { IsSuccessful = true };

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void PerformanceImprovements_DefaultsToNull()
	{
		// Arrange & Act
		var result = new IndexOptimizationResult { IsSuccessful = true };

		// Assert
		result.PerformanceImprovements.ShouldBeNull();
	}

	#endregion

	#region Successful Optimization Tests

	[Fact]
	public void SuccessfulOptimization_HasCorrectProperties()
	{
		// Arrange
		var actions = new[]
		{
			"Optimized refresh interval to 30s",
			"Reduced replica count from 2 to 1"
		};
		var improvements = new Dictionary<string, string>
		{
			["refresh_interval"] = "1s -> 30s",
			["replicas"] = "2 -> 1"
		};

		// Act
		var result = new IndexOptimizationResult
		{
			IsSuccessful = true,
			PerformedActions = actions,
			PerformanceImprovements = improvements
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.PerformedActions.Count().ShouldBe(2);
		result.Errors.ShouldBeEmpty();
		result.PerformanceImprovements.ShouldNotBeNull();
		result.PerformanceImprovements.Count.ShouldBe(2);
	}

	#endregion

	#region Failed Optimization Tests

	[Fact]
	public void FailedOptimization_HasErrors()
	{
		// Arrange
		var errors = new[] { "Index not found", "Permission denied" };

		// Act
		var result = new IndexOptimizationResult
		{
			IsSuccessful = false,
			Errors = errors
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.Errors.Count().ShouldBe(2);
		result.Errors.ShouldContain("Index not found");
		result.Errors.ShouldContain("Permission denied");
	}

	[Fact]
	public void FailedOptimization_NoPerformedActions()
	{
		// Arrange & Act
		var result = new IndexOptimizationResult
		{
			IsSuccessful = false,
			Errors = ["Connection timeout"]
		};

		// Assert
		result.PerformedActions.ShouldBeEmpty();
	}

	#endregion

	#region Partial Success Tests

	[Fact]
	public void PartialSuccess_HasActionsAndErrors()
	{
		// Arrange
		var actions = new[] { "Optimized refresh interval" };
		var errors = new[] { "Failed to reduce replicas: insufficient nodes" };

		// Act
		var result = new IndexOptimizationResult
		{
			IsSuccessful = false,
			PerformedActions = actions,
			Errors = errors
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.PerformedActions.Count().ShouldBe(1);
		result.Errors.Count().ShouldBe(1);
	}

	#endregion

	#region Performance Improvements Tests

	[Fact]
	public void PerformanceImprovements_CanContainMultipleMetrics()
	{
		// Arrange
		var improvements = new Dictionary<string, string>
		{
			["search_latency"] = "100ms -> 50ms",
			["indexing_throughput"] = "1000 docs/s -> 2000 docs/s",
			["storage_size"] = "10GB -> 5GB"
		};

		// Act
		var result = new IndexOptimizationResult
		{
			IsSuccessful = true,
			PerformanceImprovements = improvements
		};

		// Assert
		result.PerformanceImprovements.Count.ShouldBe(3);
		result.PerformanceImprovements["search_latency"].ShouldBe("100ms -> 50ms");
	}

	#endregion
}
