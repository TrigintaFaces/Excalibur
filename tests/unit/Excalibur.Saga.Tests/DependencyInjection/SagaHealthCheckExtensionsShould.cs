// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Health;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagaHealthCheckExtensions"/>.
/// Verifies health check registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaHealthCheckExtensionsShould
{
	#region AddSagaHealthCheck with Configure Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaHealthCheckExtensions.AddSagaHealthCheck(null!));
	}

	[Fact]
	public void RegisterHealthCheck_WithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();

		// Act
		builder.AddSagaHealthCheck();
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "sagas");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHealthCheck_WithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();

		// Act
		builder.AddSagaHealthCheck(name: "custom-saga-check");
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "custom-saga-check");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHealthCheck_WithCustomFailureStatus()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();

		// Act
		builder.AddSagaHealthCheck(failureStatus: HealthStatus.Degraded);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.Single(r => r.Name == "sagas");
		registration.FailureStatus.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public void RegisterHealthCheck_WithTags()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();
		var tags = new[] { "saga", "workflow" };

		// Act
		builder.AddSagaHealthCheck(tags: tags);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.Single(r => r.Name == "sagas");
		registration.Tags.ShouldContain("saga");
		registration.Tags.ShouldContain("workflow");
	}

	[Fact]
	public void ApplyConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();
		var configureInvoked = false;

		// Act
		builder.AddSagaHealthCheck(configure: opts =>
		{
			configureInvoked = true;
			opts.StuckThreshold = TimeSpan.FromHours(1);
		});

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void ReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddSagaHealthCheck();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region AddSagaHealthCheck with Options Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForOptionsOverload()
	{
		// Arrange
		var options = new SagaHealthCheckOptions();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaHealthCheckExtensions.AddSagaHealthCheck(null!, options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddSagaHealthCheck(options: null!));
	}

	[Fact]
	public void RegisterHealthCheck_WithProvidedOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();
		var options = new SagaHealthCheckOptions
		{
			StuckThreshold = TimeSpan.FromMinutes(45),
			UnhealthyStuckThreshold = 20
		};

		// Act
		builder.AddSagaHealthCheck(options);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "sagas");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHealthCheck_WithOptionsAndCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();
		var options = new SagaHealthCheckOptions();

		// Act
		builder.AddSagaHealthCheck(options, name: "saga-with-options");
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "saga-with-options");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnBuilderForChaining_WithOptionsOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		var builder = services.AddHealthChecks();
		var options = new SagaHealthCheckOptions();

		// Act
		var result = builder.AddSagaHealthCheck(options);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion
}
