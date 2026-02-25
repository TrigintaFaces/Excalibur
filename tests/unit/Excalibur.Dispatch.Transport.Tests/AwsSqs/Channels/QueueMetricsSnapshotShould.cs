// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class QueueMetricsSnapshotShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var snapshot = new QueueMetricsSnapshot();

		// Assert
		snapshot.MessagesReceived.ShouldBe(0);
		snapshot.MessagesSent.ShouldBe(0);
		snapshot.Errors.ShouldBe(0);
		snapshot.AverageReceiveTime.ShouldBe(0.0);
		snapshot.AverageSendTime.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var snapshot = new QueueMetricsSnapshot
		{
			MessagesReceived = 1000,
			MessagesSent = 500,
			Errors = 3,
			AverageReceiveTime = 12.5,
			AverageSendTime = 8.3,
		};

		// Assert
		snapshot.MessagesReceived.ShouldBe(1000);
		snapshot.MessagesSent.ShouldBe(500);
		snapshot.Errors.ShouldBe(3);
		snapshot.AverageReceiveTime.ShouldBe(12.5);
		snapshot.AverageSendTime.ShouldBe(8.3);
	}
}
