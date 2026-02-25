// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AcknowledgmentMetricsShould
{
	[Fact]
	public void HaveZeroDefaults()
	{
		// Arrange & Act
		var metrics = new AcknowledgmentMetrics();

		// Assert
		metrics.TotalQueued.ShouldBe(0);
		metrics.TotalAcknowledged.ShouldBe(0);
		metrics.TotalErrors.ShouldBe(0);
		metrics.TotalBatches.ShouldBe(0);
		metrics.TotalDeadlineWarnings.ShouldBe(0);
		metrics.AverageBatchTime.ShouldBe(0);
	}

	[Fact]
	public void IncrementQueuedCount()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();

		// Act
		metrics.IncrementQueued();
		metrics.IncrementQueued();
		metrics.IncrementQueued();

		// Assert
		metrics.TotalQueued.ShouldBe(3);
	}

	[Fact]
	public void IncrementErrorCount()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();

		// Act
		metrics.IncrementErrors();
		metrics.IncrementErrors();

		// Assert
		metrics.TotalErrors.ShouldBe(2);
	}

	[Fact]
	public void IncrementDeadlineWarnings()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();

		// Act
		metrics.IncrementDeadlineWarnings();
		metrics.IncrementDeadlineWarnings(3);

		// Assert
		metrics.TotalDeadlineWarnings.ShouldBe(4);
	}

	[Fact]
	public void RecordBatchSent()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();

		// Act
		metrics.RecordBatchSent(10, TimeSpan.FromMilliseconds(100));
		metrics.RecordBatchSent(5, TimeSpan.FromMilliseconds(200));

		// Assert
		metrics.TotalAcknowledged.ShouldBe(15);
		metrics.TotalBatches.ShouldBe(2);
		metrics.AverageBatchTime.ShouldBe(150.0);
	}

	[Fact]
	public void CalculateCorrectAverageBatchTime()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();

		// Act
		metrics.RecordBatchSent(1, TimeSpan.FromMilliseconds(50));
		metrics.RecordBatchSent(1, TimeSpan.FromMilliseconds(150));
		metrics.RecordBatchSent(1, TimeSpan.FromMilliseconds(100));

		// Assert - average of 50, 150, 100 = 100
		metrics.AverageBatchTime.ShouldBe(100.0);
	}

	[Fact]
	public void ReturnZeroAverageBatchTimeWhenNoBatches()
	{
		// Arrange & Act
		var metrics = new AcknowledgmentMetrics();

		// Assert
		metrics.AverageBatchTime.ShouldBe(0);
	}

	[Fact]
	public void ProduceCorrectToString()
	{
		// Arrange
		var metrics = new AcknowledgmentMetrics();
		metrics.IncrementQueued();
		metrics.IncrementQueued();
		metrics.RecordBatchSent(1, TimeSpan.FromMilliseconds(50));
		metrics.IncrementDeadlineWarnings();
		metrics.IncrementErrors();

		// Act
		var result = metrics.ToString();

		// Assert
		result.ShouldContain("Queued=2");
		result.ShouldContain("Acknowledged=1");
		result.ShouldContain("Batches=1");
		result.ShouldContain("DeadlineWarnings=1");
		result.ShouldContain("Errors=1");
	}

	[Fact]
	public void CloneCorrectly()
	{
		// Arrange
		var original = new AcknowledgmentMetrics();
		original.IncrementQueued();
		original.IncrementQueued();
		original.IncrementErrors();
		original.RecordBatchSent(5, TimeSpan.FromMilliseconds(100));
		original.IncrementDeadlineWarnings(2);

		// Act
		var clone = original.Clone();

		// Assert
		clone.TotalQueued.ShouldBe(2);
		clone.TotalAcknowledged.ShouldBe(5);
		clone.TotalErrors.ShouldBe(1);
		clone.TotalBatches.ShouldBe(1);
		clone.TotalDeadlineWarnings.ShouldBe(2);
		clone.AverageBatchTime.ShouldBe(100.0);
	}

	[Fact]
	public void CloneIndependentOfOriginal()
	{
		// Arrange
		var original = new AcknowledgmentMetrics();
		original.IncrementQueued();

		// Act
		var clone = original.Clone();
		original.IncrementQueued();

		// Assert - clone should not reflect changes to original
		clone.TotalQueued.ShouldBe(1);
		original.TotalQueued.ShouldBe(2);
	}
}
