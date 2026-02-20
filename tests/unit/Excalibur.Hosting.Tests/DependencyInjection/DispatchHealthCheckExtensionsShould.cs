// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Hosting.Options;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class DispatchHealthCheckExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IHealthChecksBuilder)null!).AddDispatchHealthChecks());
	}

	[Fact]
	public void RegisterAllHealthChecks_WhenAllServicesPresent()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddSingleton(A.Fake<IInboxStore>());
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		services.AddSingleton(A.Fake<ILeaderElection>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldContain(r => r.Name == "outbox");
		registrations.ShouldContain(r => r.Name == "inbox");
		registrations.ShouldContain(r => r.Name == "sagas");
		registrations.ShouldContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RegisterOnlyOutbox_WhenOnlyOutboxServiceRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldContain(r => r.Name == "outbox");
		registrations.ShouldNotContain(r => r.Name == "inbox");
		registrations.ShouldNotContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RegisterNoHealthChecks_WhenNoServicesRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldNotContain(r => r.Name == "outbox");
		registrations.ShouldNotContain(r => r.Name == "inbox");
		registrations.ShouldNotContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RespectOptionsFlags_WhenSpecificChecksDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddSingleton(A.Fake<IInboxStore>());
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		services.AddSingleton(A.Fake<ILeaderElection>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks(options =>
		{
			options.IncludeOutbox = false;
			options.IncludeLeaderElection = false;
		});

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldNotContain(r => r.Name == "outbox");
		registrations.ShouldContain(r => r.Name == "inbox");
		registrations.ShouldContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void ReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddDispatchHealthChecks();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DefaultOptionsIncludeAllChecks()
	{
		// Arrange & Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeOutbox.ShouldBeTrue();
		options.IncludeInbox.ShouldBeTrue();
		options.IncludeSaga.ShouldBeTrue();
		options.IncludeLeaderElection.ShouldBeTrue();
	}

	private static IReadOnlyList<HealthCheckRegistration> GetHealthCheckRegistrations(IServiceCollection services)
	{
		var registrations = new List<HealthCheckRegistration>();

		foreach (var descriptor in services)
		{
			if (descriptor.ServiceType == typeof(HealthCheckRegistration) &&
				descriptor.ImplementationInstance is HealthCheckRegistration registration)
			{
				registrations.Add(registration);
			}
		}

		// Also check via IConfigureOptions<HealthCheckServiceOptions> pattern
		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<HealthCheckServiceOptions>>();
		if (options?.Value.Registrations is { } regs)
		{
			foreach (var reg in regs)
			{
				if (!registrations.Any(r => r.Name == reg.Name))
				{
					registrations.Add(reg);
				}
			}
		}

		return registrations;
	}
}
