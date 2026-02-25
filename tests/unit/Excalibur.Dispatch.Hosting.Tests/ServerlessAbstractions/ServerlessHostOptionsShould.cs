// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Tests.ServerlessAbstractions;

/// <summary>
/// Unit tests for ServerlessHostOptions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerlessHostOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions();

		// Assert
		options.PreferredPlatform.ShouldBeNull();
		options.EnableColdStartOptimization.ShouldBeTrue();
		options.EnableDistributedTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableStructuredLogging.ShouldBeTrue();
		options.ExecutionTimeout.ShouldBeNull();
		options.MemoryLimitMB.ShouldBeNull();
	}

	[Fact]
	public void EnvironmentVariables_IsInitialized()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions();

		// Assert
		_ = options.EnvironmentVariables.ShouldNotBeNull();
		options.EnvironmentVariables.ShouldBeEmpty();
	}

	[Fact]
	public void PlatformOptions_AreInitialized()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions();

		// Assert
		_ = options.AwsLambda.ShouldNotBeNull();
		_ = options.AzureFunctions.ShouldNotBeNull();
		_ = options.GoogleCloudFunctions.ShouldNotBeNull();
	}

	[Fact]
	public void PreferredPlatform_CanBeSet()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions
		{
			PreferredPlatform = ServerlessPlatform.AwsLambda
		};

		// Assert
		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void EnableColdStartOptimization_CanBeDisabled()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions
		{
			EnableColdStartOptimization = false
		};

		// Assert
		options.EnableColdStartOptimization.ShouldBeFalse();
	}

	[Fact]
	public void ExecutionTimeout_CanBeSet()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions
		{
			ExecutionTimeout = TimeSpan.FromMinutes(5)
		};

		// Assert
		options.ExecutionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void MemoryLimitMB_CanBeSet()
	{
		// Arrange & Act
		var options = new ServerlessHostOptions
		{
			MemoryLimitMB = 512
		};

		// Assert
		options.MemoryLimitMB.ShouldBe(512);
	}
}
