// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Abstractions.Tests.Delivery;

/// <summary>
/// Unit tests for the <see cref="DocumentProgress"/> record struct.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class DocumentProgressShould
{
	[Fact]
	public void Constructor_Should_SetAllProperties()
	{
		// Act
		var progress = new DocumentProgress(50.0, 500, 1000, "Processing");

		// Assert
		progress.PercentComplete.ShouldBe(50.0);
		progress.ItemsProcessed.ShouldBe(500);
		progress.TotalItems.ShouldBe(1000);
		progress.CurrentPhase.ShouldBe("Processing");
	}

	[Fact]
	public void Completed_Should_CreateCompletedProgress()
	{
		// Act
		var progress = DocumentProgress.Completed(1000);

		// Assert
		progress.PercentComplete.ShouldBe(100.0);
		progress.ItemsProcessed.ShouldBe(1000);
		progress.TotalItems.ShouldBe(1000);
		progress.CurrentPhase.ShouldBe("Completed");
	}

	[Fact]
	public void Completed_Should_UseCustomPhase()
	{
		// Act
		var progress = DocumentProgress.Completed(500, "Finalization done");

		// Assert
		progress.CurrentPhase.ShouldBe("Finalization done");
	}

	[Fact]
	public void Indeterminate_Should_CreateIndeterminateProgress()
	{
		// Act
		var progress = DocumentProgress.Indeterminate(250);

		// Assert
		progress.PercentComplete.ShouldBe(-1);
		progress.ItemsProcessed.ShouldBe(250);
		progress.TotalItems.ShouldBeNull();
		progress.CurrentPhase.ShouldBeNull();
	}

	[Fact]
	public void Indeterminate_Should_AcceptCustomPhase()
	{
		// Act
		var progress = DocumentProgress.Indeterminate(100, "Streaming data");

		// Assert
		progress.CurrentPhase.ShouldBe("Streaming data");
	}

	[Fact]
	public void FromItems_Should_CalculatePercentage()
	{
		// Act
		var progress = DocumentProgress.FromItems(250, 1000, "Batch 1");

		// Assert
		progress.PercentComplete.ShouldBe(25.0);
		progress.ItemsProcessed.ShouldBe(250);
		progress.TotalItems.ShouldBe(1000);
		progress.CurrentPhase.ShouldBe("Batch 1");
	}

	[Fact]
	public void FromItems_Should_ReturnZero_WhenTotalIsZero()
	{
		// Act
		var progress = DocumentProgress.FromItems(0, 0);

		// Assert
		progress.PercentComplete.ShouldBe(0.0);
	}

	[Fact]
	public void Equality_Should_WorkForRecordStruct()
	{
		// Arrange
		var a = new DocumentProgress(50.0, 500, 1000, "Phase");
		var b = new DocumentProgress(50.0, 500, 1000, "Phase");

		// Act & Assert
		a.ShouldBe(b);
	}

	[Fact]
	public void Inequality_Should_WorkForDifferentValues()
	{
		// Arrange
		var a = new DocumentProgress(50.0, 500, 1000, "Phase");
		var b = new DocumentProgress(75.0, 750, 1000, "Phase");

		// Act & Assert
		a.ShouldNotBe(b);
	}
}
