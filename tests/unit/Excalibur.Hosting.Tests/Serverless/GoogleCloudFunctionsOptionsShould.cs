// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Hosting.Tests.Serverless;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleCloudFunctionsOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultRuntime()
	{
		var options = new GoogleCloudFunctionsOptions();
		options.Runtime.ShouldBe("dotnet6");
	}

	[Fact]
	public void HaveDefaultIngressSettings()
	{
		var options = new GoogleCloudFunctionsOptions();
		options.IngressSettings.ShouldBe("ALLOW_ALL");
	}

	[Fact]
	public void HaveNullMinInstancesByDefault()
	{
		var options = new GoogleCloudFunctionsOptions();
		options.MinInstances.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMaxInstancesByDefault()
	{
		var options = new GoogleCloudFunctionsOptions();
		options.MaxInstances.ShouldBeNull();
	}

	[Fact]
	public void HaveNullVpcConnectorByDefault()
	{
		var options = new GoogleCloudFunctionsOptions();
		options.VpcConnector.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomRuntime()
	{
		var options = new GoogleCloudFunctionsOptions { Runtime = "dotnet10" };
		options.Runtime.ShouldBe("dotnet10");
	}

	[Fact]
	public void AllowCustomIngressSettings()
	{
		var options = new GoogleCloudFunctionsOptions { IngressSettings = "ALLOW_INTERNAL_ONLY" };
		options.IngressSettings.ShouldBe("ALLOW_INTERNAL_ONLY");
	}

	[Fact]
	public void AllowCustomMinInstances()
	{
		var options = new GoogleCloudFunctionsOptions { MinInstances = 1 };
		options.MinInstances.ShouldBe(1);
	}

	[Fact]
	public void AllowCustomMaxInstances()
	{
		var options = new GoogleCloudFunctionsOptions { MaxInstances = 100 };
		options.MaxInstances.ShouldBe(100);
	}

	[Fact]
	public void AllowCustomVpcConnector()
	{
		var options = new GoogleCloudFunctionsOptions { VpcConnector = "projects/my-project/locations/us-central1/connectors/my-connector" };
		options.VpcConnector.ShouldBe("projects/my-project/locations/us-central1/connectors/my-connector");
	}
}
