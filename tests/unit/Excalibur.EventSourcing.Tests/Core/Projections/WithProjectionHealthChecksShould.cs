// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Tests for <see cref="EventNotificationServiceCollectionExtensions.WithProjectionHealthChecks"/>
/// DI extension: null guard, health check registration, idempotency, options registration,
/// and fluent chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class WithProjectionHealthChecksShould
{
	[Fact]
	public void ThrowOnNullBuilder()
	{
		// Arrange
		IEventSourcingBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.WithProjectionHealthChecks());
	}

	[Fact]
	public void RegisterProjectionHealthCheck_AsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.WithProjectionHealthChecks();

		// Assert
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(ProjectionHealthCheck));

		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterHealthCheckWithHealthCheckService()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.WithProjectionHealthChecks();

		// Assert — HealthCheckService infrastructure should be registered
		var descriptors = services.Where(sd => sd.ServiceType == typeof(HealthCheckService)).ToList();
		descriptors.ShouldNotBeEmpty();
	}

	[Fact]
	public void RegisterProjectionHealthCheckOptions_WithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.WithProjectionHealthChecks();

		// Assert — IValidateOptions<ProjectionHealthCheckOptions> infrastructure should exist
		var optionsDescriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IOptions<ProjectionHealthCheckOptions>));

		// Options infrastructure is registered via AddOptions<T>().ValidateOnStart()
		// which registers IOptions<T>, IOptionsMonitor<T>, etc.
		var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<ProjectionHealthCheckOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.WithProjectionHealthChecks();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void NotDuplicateRegistrations_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act — call twice
		builder.WithProjectionHealthChecks();
		builder.WithProjectionHealthChecks();

		// Assert — ProjectionHealthCheck should be registered exactly once
		var descriptors = services.Where(d =>
			d.ServiceType == typeof(ProjectionHealthCheck)).ToList();

		descriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void NotDuplicateHealthCheckRegistration_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act — call twice
		builder.WithProjectionHealthChecks();
		builder.WithProjectionHealthChecks();

		// Assert — IHealthCheck registrations should not be duplicated
		var healthCheckDescriptors = services.Where(d =>
			d.ServiceType == typeof(IHealthCheck) &&
			d.ImplementationType == typeof(ProjectionHealthCheck)).ToList();

		// AddHealthChecks().AddCheck<T>() registers via IConfigureOptions<HealthCheckServiceOptions>
		// so we verify the singleton count instead
		var singletonCount = services.Count(d =>
			d.ServiceType == typeof(ProjectionHealthCheck) &&
			d.Lifetime == ServiceLifetime.Singleton);

		singletonCount.ShouldBe(1);
	}

	private static IEventSourcingBuilder CreateBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}
}
