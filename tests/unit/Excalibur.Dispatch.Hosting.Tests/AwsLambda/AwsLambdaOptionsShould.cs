// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for AwsLambdaOptions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsLambdaOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions();

		// Assert
		options.EnableProvisionedConcurrency.ShouldBeFalse();
		options.ReservedConcurrency.ShouldBeNull();
		options.Runtime.ShouldBe("dotnet8");
		options.Handler.ShouldBeNull();
		options.PackageType.ShouldBe("Zip");
	}

	[Fact]
	public void EnableProvisionedConcurrency_CanBeEnabled()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions
		{
			EnableProvisionedConcurrency = true
		};

		// Assert
		options.EnableProvisionedConcurrency.ShouldBeTrue();
	}

	[Fact]
	public void ReservedConcurrency_CanBeSet()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions
		{
			ReservedConcurrency = 100
		};

		// Assert
		options.ReservedConcurrency.ShouldBe(100);
	}

	[Fact]
	public void Runtime_CanBeCustomized()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions
		{
			Runtime = "dotnet6"
		};

		// Assert
		options.Runtime.ShouldBe("dotnet6");
	}

	[Fact]
	public void Handler_CanBeSet()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions
		{
			Handler = "MyAssembly::MyNamespace.MyClass::MyMethod"
		};

		// Assert
		options.Handler.ShouldBe("MyAssembly::MyNamespace.MyClass::MyMethod");
	}

	[Fact]
	public void PackageType_CanBeSetToImage()
	{
		// Arrange & Act
		var options = new AwsLambdaOptions
		{
			PackageType = "Image"
		};

		// Assert
		options.PackageType.ShouldBe("Image");
	}
}
