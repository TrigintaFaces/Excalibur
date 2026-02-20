// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.StorageQueues;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DeadLetterQueueOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new DeadLetterQueueOptions();

		// Assert
		options.MaxDequeueCount.ShouldBe(5);
		options.MaxRetryAttempts.ShouldBe(3);
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(1));
		options.MaxDeadLetterQueueSize.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new DeadLetterQueueOptions
		{
			MaxDequeueCount = 10,
			MaxRetryAttempts = 5,
			MaxMessageAge = TimeSpan.FromDays(7),
			MaxDeadLetterQueueSize = 5000,
		};

		// Assert
		options.MaxDequeueCount.ShouldBe(10);
		options.MaxRetryAttempts.ShouldBe(5);
		options.MaxMessageAge.ShouldBe(TimeSpan.FromDays(7));
		options.MaxDeadLetterQueueSize.ShouldBe(5000);
	}
}
