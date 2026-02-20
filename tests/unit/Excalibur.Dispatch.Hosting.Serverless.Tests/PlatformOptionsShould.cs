// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for platform-specific option classes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PlatformOptionsShould : UnitTestBase
{
	#region AwsLambdaOptions

	[Fact]
	public void AwsLambdaOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AwsLambdaOptions();

		// Assert
		options.EnableProvisionedConcurrency.ShouldBeFalse();
		options.ReservedConcurrency.ShouldBeNull();
		options.Runtime.ShouldBe("dotnet8");
		options.Handler.ShouldBeNull();
		options.PackageType.ShouldBe("Zip");
	}

	[Fact]
	public void AwsLambdaOptions_AllProperties_CanBeSet()
	{
		// Act
		var options = new AwsLambdaOptions
		{
			EnableProvisionedConcurrency = true,
			ReservedConcurrency = 100,
			Runtime = "dotnet10",
			Handler = "MyAssembly::MyHandler::Process",
			PackageType = "Image",
		};

		// Assert
		options.EnableProvisionedConcurrency.ShouldBeTrue();
		options.ReservedConcurrency.ShouldBe(100);
		options.Runtime.ShouldBe("dotnet10");
		options.Handler.ShouldBe("MyAssembly::MyHandler::Process");
		options.PackageType.ShouldBe("Image");
	}

	#endregion

	#region AzureFunctionsOptions

	[Fact]
	public void AzureFunctionsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new AzureFunctionsOptions();

		// Assert
		options.HostingPlan.ShouldBe("Consumption");
		options.RuntimeVersion.ShouldBe("~4");
		options.EnableDurableFunctions.ShouldBeFalse();
		options.StorageConnectionString.ShouldBeNull();
	}

	[Fact]
	public void AzureFunctionsOptions_AllProperties_CanBeSet()
	{
		// Act
		var options = new AzureFunctionsOptions
		{
			HostingPlan = "Premium",
			RuntimeVersion = "~5",
			EnableDurableFunctions = true,
			StorageConnectionString = "DefaultEndpointsProtocol=https;...",
		};

		// Assert
		options.HostingPlan.ShouldBe("Premium");
		options.RuntimeVersion.ShouldBe("~5");
		options.EnableDurableFunctions.ShouldBeTrue();
		options.StorageConnectionString.ShouldBe("DefaultEndpointsProtocol=https;...");
	}

	#endregion

	#region GoogleCloudFunctionsOptions

	[Fact]
	public void GoogleCloudFunctionsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new GoogleCloudFunctionsOptions();

		// Assert
		options.Runtime.ShouldBe("dotnet6");
		options.MinInstances.ShouldBeNull();
		options.MaxInstances.ShouldBeNull();
		options.IngressSettings.ShouldBe("ALLOW_ALL");
		options.VpcConnector.ShouldBeNull();
	}

	[Fact]
	public void GoogleCloudFunctionsOptions_AllProperties_CanBeSet()
	{
		// Act
		var options = new GoogleCloudFunctionsOptions
		{
			Runtime = "dotnet8",
			MinInstances = 1,
			MaxInstances = 100,
			IngressSettings = "ALLOW_INTERNAL_ONLY",
			VpcConnector = "projects/my-project/locations/us-central1/connectors/my-connector",
		};

		// Assert
		options.Runtime.ShouldBe("dotnet8");
		options.MinInstances.ShouldBe(1);
		options.MaxInstances.ShouldBe(100);
		options.IngressSettings.ShouldBe("ALLOW_INTERNAL_ONLY");
		options.VpcConnector.ShouldBe("projects/my-project/locations/us-central1/connectors/my-connector");
	}

	#endregion
}
