// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="ServerlessHostOptions" />.
/// </summary>
[Trait("Category", "Unit")]
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
		_ = options.EnvironmentVariables.ShouldNotBeNull();
		options.EnvironmentVariables.ShouldBeEmpty();
	}

	[Fact]
	public void PreferredPlatform_CanBeSet()
	{
		// Arrange
		var options = new ServerlessHostOptions();

		// Act
		options.PreferredPlatform = ServerlessPlatform.AwsLambda;

		// Assert
		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void EnvironmentVariables_CanBeAdded()
	{
		// Arrange
		var options = new ServerlessHostOptions();

		// Act
		options.EnvironmentVariables["TEST_VAR"] = "test-value";

		// Assert
		options.EnvironmentVariables.ShouldContainKeyAndValue("TEST_VAR", "test-value");
	}

	[Fact]
	public void AwsLambdaOptions_IsNotNull()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		_ = options.AwsLambda.ShouldNotBeNull();
	}

	[Fact]
	public void AzureFunctionsOptions_IsNotNull()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		_ = options.AzureFunctions.ShouldNotBeNull();
	}

	[Fact]
	public void GoogleCloudFunctionsOptions_IsNotNull()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		_ = options.GoogleCloudFunctions.ShouldNotBeNull();
	}

	[Fact]
	public void ExecutionTimeout_CanBeSet()
	{
		// Arrange
		var options = new ServerlessHostOptions();
		var timeout = TimeSpan.FromMinutes(5);

		// Act
		options.ExecutionTimeout = timeout;

		// Assert
		options.ExecutionTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void MemoryLimitMB_CanBeSet()
	{
		// Arrange
		var options = new ServerlessHostOptions();

		// Act
		options.MemoryLimitMB = 512;

		// Assert
		options.MemoryLimitMB.ShouldBe(512);
	}
}
