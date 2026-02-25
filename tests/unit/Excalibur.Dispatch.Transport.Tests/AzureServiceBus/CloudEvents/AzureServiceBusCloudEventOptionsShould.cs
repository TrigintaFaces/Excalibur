// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureServiceBusCloudEventOptions();

		// Assert
		options.UseSessionsForOrdering.ShouldBeFalse();
		options.DefaultSessionId.ShouldBeNull();
		options.EnableDuplicateDetection.ShouldBeTrue();
		options.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(10));
		options.UsePartitionKeys.ShouldBeTrue();
		options.MaxMessageSizeBytes.ShouldBe(256 * 1024);
		options.EnableScheduledDelivery.ShouldBeTrue();
		options.EnableDeadLetterQueue.ShouldBeTrue();
		options.MaxDeliveryCount.ShouldBe(10);
		options.TimeToLive.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureServiceBusCloudEventOptions
		{
			UseSessionsForOrdering = true,
			DefaultSessionId = "session-abc",
			EnableDuplicateDetection = false,
			DuplicateDetectionWindow = TimeSpan.FromMinutes(30),
			UsePartitionKeys = false,
			MaxMessageSizeBytes = 1024 * 1024,
			EnableScheduledDelivery = false,
			EnableDeadLetterQueue = false,
			MaxDeliveryCount = 5,
			TimeToLive = TimeSpan.FromDays(7),
		};

		// Assert
		options.UseSessionsForOrdering.ShouldBeTrue();
		options.DefaultSessionId.ShouldBe("session-abc");
		options.EnableDuplicateDetection.ShouldBeFalse();
		options.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(30));
		options.UsePartitionKeys.ShouldBeFalse();
		options.MaxMessageSizeBytes.ShouldBe(1024 * 1024);
		options.EnableScheduledDelivery.ShouldBeFalse();
		options.EnableDeadLetterQueue.ShouldBeFalse();
		options.MaxDeliveryCount.ShouldBe(5);
		options.TimeToLive.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void AllowNullTimeToLive()
	{
		// Arrange & Act
		var options = new AzureServiceBusCloudEventOptions { TimeToLive = null };

		// Assert
		options.TimeToLive.ShouldBeNull();
	}
}
