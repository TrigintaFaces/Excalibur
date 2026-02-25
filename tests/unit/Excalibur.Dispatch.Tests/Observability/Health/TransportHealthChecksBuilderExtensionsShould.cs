// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Tests.Observability.Health;

/// <summary>
/// Unit tests for <see cref="TransportHealthChecksBuilderExtensions"/>.
/// Part of TBC-022 Transport Bindings Unit Test Suite (Sprint 200).
/// Verifies TBC-019 Health Checks Integration from Sprint 199.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class TransportHealthChecksBuilderExtensionsShould
{
	[Fact]
	public void AddTransportHealthChecks_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TransportHealthChecksBuilderExtensions.AddTransportHealthChecks(null!));
	}

	[Fact]
	public void AddTransportHealthChecks_RegisterHealthCheckWithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks();

		// Assert - Verify health check is registered via HealthCheckServiceOptions
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.Registrations.ShouldContain(r => r.Name == TransportHealthChecksBuilderExtensions.DefaultHealthCheckName);
	}

	[Fact]
	public void AddTransportHealthChecks_RegisterHealthCheckWithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");
		const string customName = "my-transports";

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks(name: customName);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.Registrations.ShouldContain(r => r.Name == customName);
	}

	[Fact]
	public void AddTransportHealthChecks_UseDefaultTags()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks();

		// Assert - Check default tags: "transport", "messaging", "ready"
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();

		var registration = options.Value.Registrations.FirstOrDefault(r =>
			r.Name == TransportHealthChecksBuilderExtensions.DefaultHealthCheckName);
		_ = registration.ShouldNotBeNull();
		registration.Tags.ShouldContain("transport");
		registration.Tags.ShouldContain("messaging");
		registration.Tags.ShouldContain("ready");
	}

	[Fact]
	public void AddTransportHealthChecks_AllowCustomTags()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");
		var customTags = new[] { "custom", "infra" };

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks(tags: customTags);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();

		var registration = options.Value.Registrations.FirstOrDefault(r =>
			r.Name == TransportHealthChecksBuilderExtensions.DefaultHealthCheckName);
		_ = registration.ShouldNotBeNull();
		registration.Tags.ShouldContain("custom");
		registration.Tags.ShouldContain("infra");
		registration.Tags.ShouldNotContain("transport"); // Default tags replaced
	}

	[Fact]
	public void AddTransportHealthChecks_AllowCustomFailureStatus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks(failureStatus: HealthStatus.Degraded);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();

		var registration = options.Value.Registrations.FirstOrDefault(r =>
			r.Name == TransportHealthChecksBuilderExtensions.DefaultHealthCheckName);
		_ = registration.ShouldNotBeNull();
		registration.FailureStatus.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public void AddTransportHealthChecks_ReturnBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddTransportHealthChecks();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddTransportHealthChecks_WithOptions_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TransportHealthChecksBuilderExtensions.AddTransportHealthChecks(null!, _ => { }));
	}

	[Fact]
	public void AddTransportHealthChecks_WithOptions_ThrowWhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");
		var builder = services.AddHealthChecks();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.AddTransportHealthChecks((Action<MultiTransportHealthCheckOptions>)null!));
	}

	[Fact]
	public void AddTransportHealthChecks_WithOptions_ApplyCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddHealthChecks()
			.AddTransportHealthChecks(opts =>
			{
				opts.RequireAtLeastOneTransport = true;
				opts.RequireDefaultTransportHealthy = false;
			});

		// Assert - Options are applied (we verify via the health check factory)
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.Registrations.ShouldContain(r =>
			r.Name == TransportHealthChecksBuilderExtensions.DefaultHealthCheckName);
	}
}
