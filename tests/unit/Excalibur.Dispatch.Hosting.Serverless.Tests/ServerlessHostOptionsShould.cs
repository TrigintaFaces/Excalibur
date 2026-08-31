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
		options.Telemetry.EnableDistributedTracing.ShouldBeTrue();
		options.Telemetry.EnableMetrics.ShouldBeTrue();
		options.Telemetry.EnableStructuredLogging.ShouldBeTrue();
		options.ExecutionTimeout.ShouldBeNull();
		options.MemoryLimitMB.ShouldBeNull();
		options.EnvironmentVariables.ShouldNotBeNull();
		options.EnvironmentVariables.ShouldBeEmpty();
	}


	[Fact]
	public void Telemetry_SubOptionsAreInitialized()
	{
		// Act
		var options = new ServerlessHostOptions();

		// Assert
		options.Telemetry.ShouldNotBeNull();
	}

	[Fact]
	public void AllProperties_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new ServerlessHostOptions
		{
			PreferredPlatform = ServerlessPlatform.AwsLambda,
			EnableColdStartOptimization = false,
			ExecutionTimeout = TimeSpan.FromMinutes(5),
			MemoryLimitMB = 1024,
		};
		options.Telemetry.EnableDistributedTracing = false;
		options.Telemetry.EnableMetrics = false;
		options.Telemetry.EnableStructuredLogging = false;

		// Assert
		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AwsLambda);
		options.EnableColdStartOptimization.ShouldBeFalse();
		options.Telemetry.EnableDistributedTracing.ShouldBeFalse();
		options.Telemetry.EnableMetrics.ShouldBeFalse();
		options.Telemetry.EnableStructuredLogging.ShouldBeFalse();
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

	// ExecutionTimeout is a promise the framework has to keep: a consumer who sets it to bound a
	// runaway handler must actually get that bound. These arms pin BOTH directions -- it shortens
	// the budget when it is tighter than the platform's, and it cannot lengthen it when it is not.
	[Fact]
	public void CapExecutionBudgetWhenTheConfiguredTimeoutIsShorterThanThePlatformBudget()
	{
		var remaining = TimeSpan.FromSeconds(30);

		var budget = ServerlessHostOptions.ComputeExecutionTimeout(remaining, TimeSpan.FromSeconds(5));

		budget.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void IgnoreAConfiguredTimeoutLongerThanThePlatformBudget()
	{
		var remaining = TimeSpan.FromSeconds(30);

		var budget = ServerlessHostOptions.ComputeExecutionTimeout(remaining, TimeSpan.FromMinutes(10));

		budget.ShouldBe(remaining - ServerlessHostOptions.DefaultCleanupReserve);
	}

	[Fact]
	public void FallBackToThePlatformBudgetWhenNoTimeoutIsConfigured()
	{
		var remaining = TimeSpan.FromSeconds(30);

		var budget = ServerlessHostOptions.ComputeExecutionTimeout(remaining, configuredTimeout: null);

		budget.ShouldBe(remaining - ServerlessHostOptions.DefaultCleanupReserve);
	}

	[Fact]
	public void FloorTheBudgetAtZeroWhenTheConfiguredTimeoutIsNegative()
	{
		var budget = ServerlessHostOptions.ComputeExecutionTimeout(
			TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-1));

		budget.ShouldBe(TimeSpan.Zero);
	}
}
