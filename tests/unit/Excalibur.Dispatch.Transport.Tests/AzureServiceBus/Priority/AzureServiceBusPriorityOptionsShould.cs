// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Priority;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusPriorityOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureServiceBusPriorityOptions();

		// Assert
		options.PriorityLevels.ShouldBe(3);
		options.QueueNameTemplate.ShouldBe("dispatch-priority-{0}");
		options.DefaultPriority.ShouldBe(1);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureServiceBusPriorityOptions
		{
			PriorityLevels = 5,
			QueueNameTemplate = "orders-priority-{0}",
			DefaultPriority = 2,
		};

		// Assert
		options.PriorityLevels.ShouldBe(5);
		options.QueueNameTemplate.ShouldBe("orders-priority-{0}");
		options.DefaultPriority.ShouldBe(2);
	}
}
