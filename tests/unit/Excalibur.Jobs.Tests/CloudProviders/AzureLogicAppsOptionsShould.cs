// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Core;

using Excalibur.Jobs.CloudProviders.Azure;

namespace Excalibur.Jobs.Tests.CloudProviders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureLogicAppsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "test-rg",
			SubscriptionId = "sub-id",
			JobExecutionEndpoint = "https://example.com/jobs",
		};

		// Assert
		options.Location.ShouldBe(AzureLocation.EastUS);
		options.Tags.ShouldNotBeNull();
		options.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingResourceGroupName()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "my-rg",
			SubscriptionId = "sub-1",
			JobExecutionEndpoint = "https://test.com",
		};

		// Assert
		options.ResourceGroupName.ShouldBe("my-rg");
	}

	[Fact]
	public void AllowSettingSubscriptionId()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "rg",
			SubscriptionId = "my-sub-id",
			JobExecutionEndpoint = "https://test.com",
		};

		// Assert
		options.SubscriptionId.ShouldBe("my-sub-id");
	}

	[Fact]
	public void AllowSettingLocation()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "rg",
			SubscriptionId = "sub",
			JobExecutionEndpoint = "https://test.com",
			Location = AzureLocation.WestEurope,
		};

		// Assert
		options.Location.ShouldBe(AzureLocation.WestEurope);
	}

	[Fact]
	public void AllowSettingTags()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "rg",
			SubscriptionId = "sub",
			JobExecutionEndpoint = "https://test.com",
			Tags = new Dictionary<string, string>
			{
				["env"] = "prod",
				["team"] = "platform",
			},
		};

		// Assert
		options.Tags.Count.ShouldBe(2);
		options.Tags["env"].ShouldBe("prod");
	}

	[Fact]
	public void AllowSettingJobExecutionEndpoint()
	{
		// Act
		var options = new AzureLogicAppsOptions
		{
			ResourceGroupName = "rg",
			SubscriptionId = "sub",
			JobExecutionEndpoint = "https://api.example.com/execute",
		};

		// Assert
		options.JobExecutionEndpoint.ShouldBe("https://api.example.com/execute");
	}
}
