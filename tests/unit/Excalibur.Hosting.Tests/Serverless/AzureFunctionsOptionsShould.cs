// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Hosting.Tests.Serverless;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureFunctionsOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultHostingPlan()
	{
		var options = new AzureFunctionsOptions();
		options.HostingPlan.ShouldBe("Consumption");
	}

	[Fact]
	public void HaveDefaultRuntimeVersion()
	{
		var options = new AzureFunctionsOptions();
		options.RuntimeVersion.ShouldBe("~4");
	}

	[Fact]
	public void HaveDurableFunctionsDisabledByDefault()
	{
		var options = new AzureFunctionsOptions();
		options.EnableDurableFunctions.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullStorageConnectionStringByDefault()
	{
		var options = new AzureFunctionsOptions();
		options.StorageConnectionString.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomHostingPlan()
	{
		var options = new AzureFunctionsOptions { HostingPlan = "Premium" };
		options.HostingPlan.ShouldBe("Premium");
	}

	[Fact]
	public void AllowCustomRuntimeVersion()
	{
		var options = new AzureFunctionsOptions { RuntimeVersion = "~5" };
		options.RuntimeVersion.ShouldBe("~5");
	}

	[Fact]
	public void AllowEnablingDurableFunctions()
	{
		var options = new AzureFunctionsOptions { EnableDurableFunctions = true };
		options.EnableDurableFunctions.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomStorageConnectionString()
	{
		var options = new AzureFunctionsOptions { StorageConnectionString = "UseDevelopmentStorage=true" };
		options.StorageConnectionString.ShouldBe("UseDevelopmentStorage=true");
	}
}
