// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.FlowControl;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class FlowControlMetricsSnapshotShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var snapshot = new FlowControlMetricsSnapshot();

		// Assert
		snapshot.MessagesReceived.ShouldBe(0);
		snapshot.MessagesProcessed.ShouldBe(0);
		snapshot.BytesReceived.ShouldBe(0);
		snapshot.BytesProcessed.ShouldBe(0);
		snapshot.ProcessingErrors.ShouldBe(0);
		snapshot.FlowControlPauses.ShouldBe(0);
		snapshot.CurrentOutstandingMessages.ShouldBe(0);
		snapshot.CurrentOutstandingBytes.ShouldBe(0);
		snapshot.MessageProcessingRate.ShouldBe(0.0);
		snapshot.ByteProcessingRate.ShouldBe(0.0);
		snapshot.ErrorRate.ShouldBe(0.0);
		snapshot.UtilizationPercentage.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new FlowControlMetricsSnapshot
		{
			MessagesReceived = 10000,
			MessagesProcessed = 9500,
			BytesReceived = 50_000_000,
			BytesProcessed = 47_500_000,
			ProcessingErrors = 500,
			FlowControlPauses = 25,
			CurrentOutstandingMessages = 100,
			CurrentOutstandingBytes = 500_000,
			MessageProcessingRate = 1500.5,
			ByteProcessingRate = 7_500_000.0,
			ErrorRate = 5.0,
			UtilizationPercentage = 85.5,
			SnapshotTime = now,
		};

		// Assert
		snapshot.MessagesReceived.ShouldBe(10000);
		snapshot.MessagesProcessed.ShouldBe(9500);
		snapshot.BytesReceived.ShouldBe(50_000_000);
		snapshot.BytesProcessed.ShouldBe(47_500_000);
		snapshot.ProcessingErrors.ShouldBe(500);
		snapshot.FlowControlPauses.ShouldBe(25);
		snapshot.CurrentOutstandingMessages.ShouldBe(100);
		snapshot.CurrentOutstandingBytes.ShouldBe(500_000);
		snapshot.MessageProcessingRate.ShouldBe(1500.5);
		snapshot.ByteProcessingRate.ShouldBe(7_500_000.0);
		snapshot.ErrorRate.ShouldBe(5.0);
		snapshot.UtilizationPercentage.ShouldBe(85.5);
		snapshot.SnapshotTime.ShouldBe(now);
	}
}
