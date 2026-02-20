// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="DocumentProgress"/> record struct.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class DocumentProgressShould
{
	[Fact]
	public void StoreConstructorParameters()
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
	public void AllowNullTotalItems()
	{
		// Act
		var progress = new DocumentProgress(25.0, 100, null, "Streaming");

		// Assert
		progress.TotalItems.ShouldBeNull();
	}

	[Fact]
	public void AllowNullCurrentPhase()
	{
		// Act
		var progress = new DocumentProgress(75.0, 300, 400, null);

		// Assert
		progress.CurrentPhase.ShouldBeNull();
	}

	[Fact]
	public void Completed_ReturnHundredPercent()
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
	public void Completed_UseCustomFinalPhase()
	{
		// Act
		var progress = DocumentProgress.Completed(500, "Finished processing");

		// Assert
		progress.PercentComplete.ShouldBe(100.0);
		progress.ItemsProcessed.ShouldBe(500);
		progress.TotalItems.ShouldBe(500);
		progress.CurrentPhase.ShouldBe("Finished processing");
	}

	[Fact]
	public void Indeterminate_ReturnNegativeOnePercent()
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
	public void Indeterminate_UseCustomPhase()
	{
		// Act
		var progress = DocumentProgress.Indeterminate(100, "Streaming data");

		// Assert
		progress.PercentComplete.ShouldBe(-1);
		progress.ItemsProcessed.ShouldBe(100);
		progress.TotalItems.ShouldBeNull();
		progress.CurrentPhase.ShouldBe("Streaming data");
	}

	[Fact]
	public void FromItems_CalculatePercentageCorrectly()
	{
		// Act
		var progress = DocumentProgress.FromItems(500, 1000);

		// Assert
		progress.PercentComplete.ShouldBe(50.0);
		progress.ItemsProcessed.ShouldBe(500);
		progress.TotalItems.ShouldBe(1000);
	}

	[Fact]
	public void FromItems_HandleZeroTotalItems()
	{
		// Act
		var progress = DocumentProgress.FromItems(0, 0);

		// Assert
		progress.PercentComplete.ShouldBe(0.0);
		progress.ItemsProcessed.ShouldBe(0);
		progress.TotalItems.ShouldBe(0);
	}

	[Fact]
	public void FromItems_UseCustomPhase()
	{
		// Act
		var progress = DocumentProgress.FromItems(250, 500, "Indexing");

		// Assert
		progress.PercentComplete.ShouldBe(50.0);
		progress.CurrentPhase.ShouldBe("Indexing");
	}

	[Fact]
	public void SupportValueEquality()
	{
		// Arrange
		var progress1 = new DocumentProgress(50.0, 500, 1000, "Processing");
		var progress2 = new DocumentProgress(50.0, 500, 1000, "Processing");

		// Assert
		progress1.ShouldBe(progress2);
		(progress1 == progress2).ShouldBeTrue();
	}

	[Fact]
	public void DetectInequality()
	{
		// Arrange
		var progress1 = new DocumentProgress(50.0, 500, 1000, "Processing");
		var progress2 = new DocumentProgress(75.0, 750, 1000, "Processing");

		// Assert
		progress1.ShouldNotBe(progress2);
		(progress1 != progress2).ShouldBeTrue();
	}
}
