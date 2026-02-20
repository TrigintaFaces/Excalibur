// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Hosting.Tests.Serverless;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultRuntime()
	{
		var options = new AwsLambdaOptions();
		options.Runtime.ShouldBe("dotnet8");
	}

	[Fact]
	public void HaveDefaultPackageType()
	{
		var options = new AwsLambdaOptions();
		options.PackageType.ShouldBe("Zip");
	}

	[Fact]
	public void HaveProvisionedConcurrencyDisabledByDefault()
	{
		var options = new AwsLambdaOptions();
		options.EnableProvisionedConcurrency.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullReservedConcurrencyByDefault()
	{
		var options = new AwsLambdaOptions();
		options.ReservedConcurrency.ShouldBeNull();
	}

	[Fact]
	public void HaveNullHandlerByDefault()
	{
		var options = new AwsLambdaOptions();
		options.Handler.ShouldBeNull();
	}

	[Fact]
	public void AllowCustomRuntime()
	{
		var options = new AwsLambdaOptions { Runtime = "dotnet10" };
		options.Runtime.ShouldBe("dotnet10");
	}

	[Fact]
	public void AllowCustomPackageType()
	{
		var options = new AwsLambdaOptions { PackageType = "Image" };
		options.PackageType.ShouldBe("Image");
	}

	[Fact]
	public void AllowEnablingProvisionedConcurrency()
	{
		var options = new AwsLambdaOptions { EnableProvisionedConcurrency = true };
		options.EnableProvisionedConcurrency.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomReservedConcurrency()
	{
		var options = new AwsLambdaOptions { ReservedConcurrency = 100 };
		options.ReservedConcurrency.ShouldBe(100);
	}

	[Fact]
	public void AllowCustomHandler()
	{
		var options = new AwsLambdaOptions { Handler = "MyAssembly::MyNamespace.MyHandler::HandleAsync" };
		options.Handler.ShouldBe("MyAssembly::MyNamespace.MyHandler::HandleAsync");
	}
}
