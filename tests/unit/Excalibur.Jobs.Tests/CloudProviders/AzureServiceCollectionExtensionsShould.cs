// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.ResourceManager;

using Excalibur.Jobs.CloudProviders.Azure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Tests.CloudProviders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			AzureServiceCollectionExtensions.AddAzureLogicApps(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureLogicApps(null!));
	}

	[Fact]
	public void RegisterRequiredServicesAndOptions()
	{
		var services = new ServiceCollection();

		var result = services.AddAzureLogicApps(options =>
		{
			options.ResourceGroupName = "jobs-rg";
			options.SubscriptionId = "sub-id";
			options.JobExecutionEndpoint = "https://jobs.example.com/execute";
		});

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(ArmClient));
		services.ShouldContain(sd => sd.ServiceType == typeof(AzureLogicAppsJobProvider));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AzureLogicAppsOptions>>().Value;
		options.ResourceGroupName.ShouldBe("jobs-rg");
		options.SubscriptionId.ShouldBe("sub-id");
		options.JobExecutionEndpoint.ShouldBe("https://jobs.example.com/execute");
	}
}
