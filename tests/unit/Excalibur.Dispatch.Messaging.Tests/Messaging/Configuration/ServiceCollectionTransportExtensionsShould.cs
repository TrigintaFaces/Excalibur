// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionTransportExtensions"/>.
/// Tests DI merging fix per Sprint 34 bd-790j, bd-u6k0.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ServiceCollectionTransportExtensionsShould
{
	#region AddEventBindings Tests

	[Fact]
	public void AddEventBindings_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			ServiceCollectionTransportExtensions.AddEventBindings(null!, _ => { }));
	}

	[Fact]
	public void AddEventBindings_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddEventBindings(null!));
	}

	[Fact]
	public void AddEventBindings_ShareRegistryWithTransports()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - First add transports via ADR-098 pattern, then bindings
		_ = services.AddInMemoryTransport("test");
		_ = services.AddEventBindings(_ => { });

		// Assert - Should share registry
		var registryDescriptors = services.Where(d => d.ServiceType == typeof(TransportRegistry)).ToList();
		registryDescriptors.Count.ShouldBe(1);
	}

	[Fact]
	public void AddEventBindings_CreateRegistryIfNotExists()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Call bindings first (no transports registered yet)
		_ = services.AddEventBindings(_ => { });

		// Assert
		var provider = services.BuildServiceProvider();
		var registry = provider.GetService<TransportRegistry>();
		_ = registry.ShouldNotBeNull();
	}

	#endregion AddEventBindings Tests

	#region AddTransportValidation Tests

	[Fact]
	public void AddTransportValidation_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			ServiceCollectionTransportExtensions.AddTransportValidation(null!));
	}

	[Fact]
	public void AddTransportValidation_RegisterValidationServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Also need to register TransportRegistry for the validator
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddTransportValidation();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<TransportValidationOptions>();
		_ = options.ShouldNotBeNull();

		// Check hosted service is registered
		var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
		hostedServices.ShouldContain(d => d.ImplementationType == typeof(TransportStartupValidator));
	}

	[Fact]
	public void AddTransportValidation_UseDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddTransportValidation();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<TransportValidationOptions>();
		options.ValidateOnStartup.ShouldBeTrue();
		options.RequireAtLeastOneTransport.ShouldBeFalse();
		options.RequireDefaultTransportWhenMultiple.ShouldBeTrue();
	}

	[Fact]
	public void AddTransportValidation_AllowCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddTransportValidation(opts =>
		{
			opts.ValidateOnStartup = false;
			opts.RequireAtLeastOneTransport = true;
			opts.RequireDefaultTransportWhenMultiple = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<TransportValidationOptions>();
		options.ValidateOnStartup.ShouldBeFalse();
		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportWhenMultiple.ShouldBeFalse();
	}

	[Fact]
	public void AddTransportValidation_ReturnServicesForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddTransportValidation();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddTransportValidation Tests

	#region AddMultiTransportHealthChecks Tests

	[Fact]
	public void AddMultiTransportHealthChecks_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			ServiceCollectionTransportExtensions.AddMultiTransportHealthChecks(null!));
	}

	[Fact]
	public void AddMultiTransportHealthChecks_RegisterHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddMultiTransportHealthChecks();

		// Assert - Verify options are registered (can't resolve HealthCheckService without full host)
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<MultiTransportHealthCheckOptions>();
		_ = options.ShouldNotBeNull();
	}

	[Fact]
	public void AddMultiTransportHealthChecks_UseDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddMultiTransportHealthChecks();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<MultiTransportHealthCheckOptions>();
		_ = options.ShouldNotBeNull();
		options.RequireAtLeastOneTransport.ShouldBeFalse();
		options.RequireDefaultTransportHealthy.ShouldBeTrue(); // Default is true per MultiTransportHealthCheckOptions
	}

	[Fact]
	public void AddMultiTransportHealthChecks_AllowCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryTransport("test");

		// Act
		_ = services.AddMultiTransportHealthChecks(opts =>
		{
			opts.RequireAtLeastOneTransport = true;
			opts.RequireDefaultTransportHealthy = true;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<MultiTransportHealthCheckOptions>();
		options.RequireAtLeastOneTransport.ShouldBeTrue();
		options.RequireDefaultTransportHealthy.ShouldBeTrue();
	}

	[Fact]
	public void AddMultiTransportHealthChecks_CreateRegistryIfNotExists()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Call without AddEventTransports first
		_ = services.AddMultiTransportHealthChecks();

		// Assert - Should create registry
		var provider = services.BuildServiceProvider();
		var registry = provider.GetService<TransportRegistry>();
		_ = registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMultiTransportHealthChecks_ReturnServicesForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMultiTransportHealthChecks();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddMultiTransportHealthChecks Tests

	#region Integration Tests - Full Multi-Transport Setup

	[Fact]
	public void FullSetup_RegisterMultipleTransportsAndValidation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - Full multi-transport setup using ADR-098 pattern
		_ = services.AddInMemoryTransport("transport1");
		_ = services.AddInMemoryTransport("transport2");

		// Set default transport
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.SetDefaultTransport("transport2");

		_ = services.AddEventBindings(_ => { });

		_ = services.AddTransportValidation(opts =>
		{
			opts.RequireAtLeastOneTransport = true;
			opts.RequireDefaultTransportWhenMultiple = true;
		});

		_ = services.AddMultiTransportHealthChecks(opts =>
		{
			opts.RequireAtLeastOneTransport = true;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var resolvedRegistry = provider.GetRequiredService<TransportRegistry>();

		resolvedRegistry.GetTransportNames().Count().ShouldBe(2);
		resolvedRegistry.HasDefaultTransport.ShouldBeTrue();
		resolvedRegistry.DefaultTransportName.ShouldBe("transport2");
	}

	#endregion Integration Tests - Full Multi-Transport Setup
}
