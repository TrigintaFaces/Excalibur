// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessHostOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessHostOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		options.PreferredPlatform.ShouldBeNull();
		options.EnableColdStartOptimization.ShouldBeTrue();
		options.EnableDistributedTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableStructuredLogging.ShouldBeTrue();
		options.ExecutionTimeout.ShouldBeNull();
		options.MemoryLimitMB.ShouldBeNull();
		options.EnvironmentVariables.ShouldNotBeNull();
		options.EnvironmentVariables.ShouldBeEmpty();
	}

	[Fact]
	public void PlatformOptions_AreInitialized()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		options.AwsLambda.ShouldNotBeNull();
		options.AzureFunctions.ShouldNotBeNull();
		options.GoogleCloudFunctions.ShouldNotBeNull();
	}

	[Fact]
	public void AllProperties_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new ServerlessHostOptions
		{
			PreferredPlatform = ServerlessPlatform.AwsLambda,
			EnableColdStartOptimization = false,
			EnableDistributedTracing = false,
			EnableMetrics = false,
			EnableStructuredLogging = false,
			ExecutionTimeout = TimeSpan.FromMinutes(5),
			MemoryLimitMB = 1024,
		};

		// Assert
		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AwsLambda);
		options.EnableColdStartOptimization.ShouldBeFalse();
		options.EnableDistributedTracing.ShouldBeFalse();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableStructuredLogging.ShouldBeFalse();
		options.ExecutionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.MemoryLimitMB.ShouldBe(1024);
	}

	[Fact]
	public void EnvironmentVariables_CanBePopulated()
	{
		// Arrange
		var options = new ServerlessHostOptions();

		// Act
		options.EnvironmentVariables["KEY1"] = "value1";

		// Assert
		options.EnvironmentVariables.ShouldContainKeyAndValue("KEY1", "value1");
	}
}
