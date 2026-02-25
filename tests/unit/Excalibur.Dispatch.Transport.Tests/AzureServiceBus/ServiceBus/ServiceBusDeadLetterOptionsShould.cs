// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AzureServiceBus;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServiceBusDeadLetterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ServiceBusDeadLetterOptions();

		// Assert
		options.EntityPath.ShouldBe(string.Empty);
		options.MaxBatchSize.ShouldBe(100);
		options.ReceiveWaitTime.ShouldBe(TimeSpan.FromSeconds(5));
		options.StatisticsPeekCount.ShouldBe(1000);
		options.IncludeStackTrace.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ServiceBusDeadLetterOptions
		{
			EntityPath = "orders-queue",
			MaxBatchSize = 50,
			ReceiveWaitTime = TimeSpan.FromSeconds(10),
			StatisticsPeekCount = 500,
			IncludeStackTrace = false,
		};

		// Assert
		options.EntityPath.ShouldBe("orders-queue");
		options.MaxBatchSize.ShouldBe(50);
		options.ReceiveWaitTime.ShouldBe(TimeSpan.FromSeconds(10));
		options.StatisticsPeekCount.ShouldBe(500);
		options.IncludeStackTrace.ShouldBeFalse();
	}
}
