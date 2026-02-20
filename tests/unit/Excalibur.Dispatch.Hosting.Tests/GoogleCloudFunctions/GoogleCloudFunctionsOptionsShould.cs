// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Hosting.Tests.GoogleCloudFunctions;

/// <summary>
/// Unit tests for GoogleCloudFunctionsOptions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GoogleCloudFunctionsOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions();

		// Assert
		options.Runtime.ShouldBe("dotnet6");
		options.MinInstances.ShouldBeNull();
		options.MaxInstances.ShouldBeNull();
		options.IngressSettings.ShouldBe("ALLOW_ALL");
		options.VpcConnector.ShouldBeNull();
	}

	[Fact]
	public void Runtime_CanBeCustomized()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions
		{
			Runtime = "dotnet8"
		};

		// Assert
		options.Runtime.ShouldBe("dotnet8");
	}

	[Fact]
	public void MinInstances_CanBeSet()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions
		{
			MinInstances = 1
		};

		// Assert
		options.MinInstances.ShouldBe(1);
	}

	[Fact]
	public void MaxInstances_CanBeSet()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions
		{
			MaxInstances = 100
		};

		// Assert
		options.MaxInstances.ShouldBe(100);
	}

	[Fact]
	public void IngressSettings_CanBeCustomized()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions
		{
			IngressSettings = "ALLOW_INTERNAL_ONLY"
		};

		// Assert
		options.IngressSettings.ShouldBe("ALLOW_INTERNAL_ONLY");
	}

	[Fact]
	public void VpcConnector_CanBeSet()
	{
		// Arrange & Act
		var options = new GoogleCloudFunctionsOptions
		{
			VpcConnector = "projects/my-project/locations/us-central1/connectors/my-connector"
		};

		// Assert
		options.VpcConnector.ShouldBe("projects/my-project/locations/us-central1/connectors/my-connector");
	}
}
