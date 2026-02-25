// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsProcessorMetricsShould
{
	[Fact]
	public void HaveZeroDefaults()
	{
		// Arrange & Act
		var metrics = new SqsProcessorMetrics();

		// Assert
		metrics.MessagesProcessed.ShouldBe(0);
		metrics.ProcessingErrors.ShouldBe(0);
		metrics.MessagesDeleted.ShouldBe(0);
		metrics.DeleteErrors.ShouldBe(0);
		metrics.AverageProcessingTime.ShouldBe(0);
	}

	[Fact]
	public void RecordSuccess()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordSuccess(TimeSpan.FromMilliseconds(50));

		// Assert
		metrics.MessagesProcessed.ShouldBe(1);
		metrics.AverageProcessingTime.ShouldBe(50.0, 0.1);
	}

	[Fact]
	public void RecordFailure()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordFailure(TimeSpan.FromMilliseconds(30));

		// Assert
		metrics.MessagesProcessed.ShouldBe(1);
	}

	[Fact]
	public void RecordError()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordError(TimeSpan.FromMilliseconds(20));

		// Assert
		metrics.ProcessingErrors.ShouldBe(1);
	}

	[Fact]
	public void RecordDeletes()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordDeletes(5);
		metrics.RecordDeletes(3);

		// Assert
		metrics.MessagesDeleted.ShouldBe(8);
	}

	[Fact]
	public void RecordDeleteErrors()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordDeleteErrors(2);

		// Assert
		metrics.DeleteErrors.ShouldBe(2);
	}

	[Fact]
	public void CalculateAverageAcrossMultipleRecords()
	{
		// Arrange
		var metrics = new SqsProcessorMetrics();

		// Act
		metrics.RecordSuccess(TimeSpan.FromMilliseconds(100));
		metrics.RecordSuccess(TimeSpan.FromMilliseconds(200));

		// Assert
		metrics.MessagesProcessed.ShouldBe(2);
		metrics.AverageProcessingTime.ShouldBe(150, 1);
	}
}
