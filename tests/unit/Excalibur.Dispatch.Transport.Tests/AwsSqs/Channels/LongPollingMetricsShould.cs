// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class LongPollingMetricsShould
{
	[Fact]
	public void HaveZeroDefaults()
	{
		// Arrange & Act
		var metrics = new LongPollingMetrics();

		// Assert
		metrics.MessagesReceived.ShouldBe(0);
		metrics.EmptyPolls.ShouldBe(0);
		metrics.Errors.ShouldBe(0);
		metrics.AveragePollTime.ShouldBe(0);
	}

	[Fact]
	public void RecordMessagesReceived()
	{
		// Arrange
		var metrics = new LongPollingMetrics();

		// Act
		metrics.RecordMessagesReceived(10);
		metrics.RecordMessagesReceived(5);

		// Assert
		metrics.MessagesReceived.ShouldBe(15);
	}

	[Fact]
	public void RecordEmptyPoll()
	{
		// Arrange
		var metrics = new LongPollingMetrics();

		// Act
		metrics.RecordEmptyPoll();
		metrics.RecordEmptyPoll();

		// Assert
		metrics.EmptyPolls.ShouldBe(2);
	}

	[Fact]
	public void RecordError()
	{
		// Arrange
		var metrics = new LongPollingMetrics();

		// Act
		metrics.RecordError();
		metrics.RecordError();
		metrics.RecordError();

		// Assert
		metrics.Errors.ShouldBe(3);
	}

	[Fact]
	public void RecordPollDuration()
	{
		// Arrange
		var metrics = new LongPollingMetrics();

		// Act
		metrics.RecordPollDuration(TimeSpan.FromMilliseconds(100));
		metrics.RecordPollDuration(TimeSpan.FromMilliseconds(200));

		// Assert
		metrics.AveragePollTime.ShouldBe(150, 1);
	}

	[Fact]
	public void GetSnapshot()
	{
		// Arrange
		var metrics = new LongPollingMetrics();
		metrics.RecordMessagesReceived(10);
		metrics.RecordEmptyPoll();
		metrics.RecordError();

		// Act
		var snapshot = metrics.GetSnapshot();

		// Assert
		snapshot.MessagesReceived.ShouldBe(10);
		snapshot.EmptyPolls.ShouldBe(1);
		snapshot.Errors.ShouldBe(1);
	}
}
