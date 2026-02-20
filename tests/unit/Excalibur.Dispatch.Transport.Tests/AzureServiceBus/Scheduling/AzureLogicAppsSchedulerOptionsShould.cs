// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureLogicAppsSchedulerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureLogicAppsSchedulerOptions();

		// Assert
		options.SubscriptionId.ShouldBeNull();
		options.ResourceGroupName.ShouldBeNull();
		options.LogicAppName.ShouldBeNull();
		options.WorkflowName.ShouldBeNull();
		options.CallbackUrl.ShouldBeNull();
		options.TriggerName.ShouldBeNull();
		options.MaxRetries.ShouldBe(3);
		options.RetryDelaySeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureLogicAppsSchedulerOptions
		{
			SubscriptionId = "sub-123",
			ResourceGroupName = "rg-dispatch",
			LogicAppName = "my-logic-app",
			WorkflowName = "schedule-workflow",
			CallbackUrl = new Uri("https://example.com/callback"),
			TriggerName = "manual-trigger",
			MaxRetries = 5,
			RetryDelaySeconds = 120,
		};

		// Assert
		options.SubscriptionId.ShouldBe("sub-123");
		options.ResourceGroupName.ShouldBe("rg-dispatch");
		options.LogicAppName.ShouldBe("my-logic-app");
		options.WorkflowName.ShouldBe("schedule-workflow");
		options.CallbackUrl.ShouldNotBeNull();
		options.TriggerName.ShouldBe("manual-trigger");
		options.MaxRetries.ShouldBe(5);
		options.RetryDelaySeconds.ShouldBe(120);
	}
}
