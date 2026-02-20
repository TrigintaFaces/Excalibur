// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineValidationResult"/>.
/// </summary>
/// <remarks>
/// Tests the pipeline validation result class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class PipelineValidationResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var result = new PipelineValidationResult();

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsOptimized.ShouldBeFalse();
		result.Complexity.ShouldBe(PipelineComplexity.Standard);
		_ = result.Notes.ShouldNotBeNull();
		result.Notes.ShouldBeEmpty();
	}

	#endregion

	#region IsOptimized Property Tests

	[Fact]
	public void IsOptimized_CanBeSetToTrue()
	{
		// Arrange
		var result = new PipelineValidationResult();

		// Act
		result.IsOptimized = true;

		// Assert
		result.IsOptimized.ShouldBeTrue();
	}

	[Fact]
	public void IsOptimized_CanBeSetToFalse()
	{
		// Arrange
		var result = new PipelineValidationResult
		{
			IsOptimized = true,
		};

		// Act
		result.IsOptimized = false;

		// Assert
		result.IsOptimized.ShouldBeFalse();
	}

	#endregion

	#region Complexity Property Tests

	[Theory]
	[InlineData(PipelineComplexity.Standard)]
	[InlineData(PipelineComplexity.Reduced)]
	[InlineData(PipelineComplexity.Minimal)]
	[InlineData(PipelineComplexity.Direct)]
	public void Complexity_CanBeSetToVariousLevels(PipelineComplexity level)
	{
		// Arrange
		var result = new PipelineValidationResult();

		// Act
		result.Complexity = level;

		// Assert
		result.Complexity.ShouldBe(level);
	}

	[Fact]
	public void Complexity_DefaultsToStandard()
	{
		// Arrange & Act
		var result = new PipelineValidationResult();

		// Assert
		result.Complexity.ShouldBe(PipelineComplexity.Standard);
	}

	#endregion

	#region Notes Property Tests

	[Fact]
	public void Notes_IsInitializedAsEmptyCollection()
	{
		// Arrange & Act
		var result = new PipelineValidationResult();

		// Assert
		_ = result.Notes.ShouldNotBeNull();
		result.Notes.Count.ShouldBe(0);
	}

	[Fact]
	public void Notes_CanAddItems()
	{
		// Arrange
		var result = new PipelineValidationResult();

		// Act
		result.Notes.Add("Consider reducing middleware count");
		result.Notes.Add("High allocation detected in handler");

		// Assert
		result.Notes.Count.ShouldBe(2);
		result.Notes[0].ShouldBe("Consider reducing middleware count");
		result.Notes[1].ShouldBe("High allocation detected in handler");
	}

	[Fact]
	public void Notes_CanRemoveItems()
	{
		// Arrange
		var result = new PipelineValidationResult();
		result.Notes.Add("Note 1");
		result.Notes.Add("Note 2");

		// Act
		_ = result.Notes.Remove("Note 1");

		// Assert
		result.Notes.Count.ShouldBe(1);
		result.Notes[0].ShouldBe("Note 2");
	}

	[Fact]
	public void Notes_CanClearAllItems()
	{
		// Arrange
		var result = new PipelineValidationResult();
		result.Notes.Add("Note 1");
		result.Notes.Add("Note 2");

		// Act
		result.Notes.Clear();

		// Assert
		result.Notes.ShouldBeEmpty();
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange & Act
		var result = new PipelineValidationResult
		{
			IsOptimized = true,
			Complexity = PipelineComplexity.Direct,
		};
		result.Notes.Add("Pipeline is fully optimized");

		// Assert
		result.IsOptimized.ShouldBeTrue();
		result.Complexity.ShouldBe(PipelineComplexity.Direct);
		result.Notes.Count.ShouldBe(1);
	}

	[Fact]
	public void OptimizedPipeline_TypicalConfiguration()
	{
		// Arrange & Act
		var result = new PipelineValidationResult
		{
			IsOptimized = true,
			Complexity = PipelineComplexity.Minimal,
		};
		result.Notes.Add("Removed logging middleware");
		result.Notes.Add("Using inline handler invocation");
		result.Notes.Add("Zero-allocation path enabled");

		// Assert
		result.IsOptimized.ShouldBeTrue();
		result.Complexity.ShouldBe(PipelineComplexity.Minimal);
		result.Notes.Count.ShouldBe(3);
	}

	[Fact]
	public void NonOptimizedPipeline_WithWarnings()
	{
		// Arrange & Act
		var result = new PipelineValidationResult
		{
			IsOptimized = false,
			Complexity = PipelineComplexity.Standard,
		};
		result.Notes.Add("Too many middleware registered");
		result.Notes.Add("Consider using async void patterns");

		// Assert
		result.IsOptimized.ShouldBeFalse();
		result.Complexity.ShouldBe(PipelineComplexity.Standard);
		result.Notes.Count.ShouldBe(2);
	}

	#endregion
}
