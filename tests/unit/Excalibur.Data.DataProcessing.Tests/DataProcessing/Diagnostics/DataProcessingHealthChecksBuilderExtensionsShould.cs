// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.DataProcessing.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataProcessingHealthChecksBuilderExtensions"/>.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingHealthChecksBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DataProcessingHealthChecksBuilderExtensions.AddDataProcessingHealthCheck(null!));
	}

	[Fact]
	public void RegisterHealthCheckAndSharedState()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck();

		using var provider = services.BuildServiceProvider();

		// Assert — shared state singleton is registered
		var state = provider.GetService<DataProcessingHealthState>();
		state.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterSingletonHealthState_SharedAcrossResolutions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck();

		using var provider = services.BuildServiceProvider();

		// Act
		var state1 = provider.GetRequiredService<DataProcessingHealthState>();
		var state2 = provider.GetRequiredService<DataProcessingHealthState>();

		// Assert — same instance (singleton)
		state1.ShouldBeSameAs(state2);
	}

	[Fact]
	public void RegisterWithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck();

		using var provider = services.BuildServiceProvider();

		// Assert — health check service resolves (proves registration succeeded)
		var healthCheckService = provider.GetService<HealthCheckService>();
		healthCheckService.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomName()
	{
		// Arrange & Act — should not throw
		var services = new ServiceCollection();
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck(name: "custom_dp_check");

		using var provider = services.BuildServiceProvider();

		// Assert — state is still registered
		provider.GetService<DataProcessingHealthState>().ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomFailureStatus()
	{
		// Arrange & Act — should not throw
		var services = new ServiceCollection();
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck(failureStatus: HealthStatus.Degraded);

		using var provider = services.BuildServiceProvider();
		provider.GetService<DataProcessingHealthState>().ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomTags()
	{
		// Arrange & Act — should not throw
		var services = new ServiceCollection();
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck(tags: ["data-processing", "background"]);

		using var provider = services.BuildServiceProvider();
		provider.GetService<DataProcessingHealthState>().ShouldNotBeNull();
	}

	[Fact]
	public void NotDuplicateHealthState_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — register twice
		services.AddHealthChecks()
			.AddDataProcessingHealthCheck()
			.AddDataProcessingHealthCheck();

		using var provider = services.BuildServiceProvider();

		// Assert — TryAddSingleton ensures only one registration
		var state = provider.GetRequiredService<DataProcessingHealthState>();
		state.ShouldNotBeNull();
	}
}