// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class BatchProcessorMetricsShould
{
	[Fact]
	public void HaveZeroDefaults()
	{
		// Arrange & Act
		var metrics = new BatchProcessorMetrics();

		// Assert
		metrics.MessagesReceived.ShouldBe(0);
		metrics.MessagesSent.ShouldBe(0);
		metrics.MessagesDeleted.ShouldBe(0);
		metrics.SendErrors.ShouldBe(0);
		metrics.DeleteErrors.ShouldBe(0);
		metrics.AverageReceiveTime.ShouldBe(0);
		metrics.AverageSendTime.ShouldBe(0);
		metrics.AverageDeleteTime.ShouldBe(0);
	}

	[Fact]
	public void RecordReceiveBatch()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordReceiveBatch(10, TimeSpan.FromMilliseconds(100));

		// Assert
		metrics.MessagesReceived.ShouldBe(10);
		metrics.AverageReceiveTime.ShouldBe(10.0, 0.1);
	}

	[Fact]
	public void RecordSendBatch()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordSendBatch(5, TimeSpan.FromMilliseconds(50));

		// Assert
		metrics.MessagesSent.ShouldBe(5);
		metrics.AverageSendTime.ShouldBe(10.0, 0.1);
	}

	[Fact]
	public void RecordDeleteBatch()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordDeleteBatch(3, TimeSpan.FromMilliseconds(30));

		// Assert
		metrics.MessagesDeleted.ShouldBe(3);
		metrics.AverageDeleteTime.ShouldBe(10.0, 0.1);
	}

	[Fact]
	public void RecordSendErrors()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordSendErrors(2);
		metrics.RecordSendErrors(3);

		// Assert
		metrics.SendErrors.ShouldBe(5);
	}

	[Fact]
	public void RecordDeleteErrors()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordDeleteErrors(1);
		metrics.RecordDeleteErrors(4);

		// Assert
		metrics.DeleteErrors.ShouldBe(5);
	}

	[Fact]
	public void AccumulateMultipleReceiveBatches()
	{
		// Arrange
		var metrics = new BatchProcessorMetrics();

		// Act
		metrics.RecordReceiveBatch(10, TimeSpan.FromMilliseconds(100));
		metrics.RecordReceiveBatch(20, TimeSpan.FromMilliseconds(200));

		// Assert
		metrics.MessagesReceived.ShouldBe(30);
	}
}
