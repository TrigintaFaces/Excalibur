// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Domain;
using Excalibur.Domain.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Domain.Extensions;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionContextExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ServiceCollectionContextExtensionsShould
{
	#region TryAddCorrelationId Tests

	[Fact]
	public void TryAddCorrelationId_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddCorrelationId());
	}

	[Fact]
	public void TryAddCorrelationId_RegistersICorrelationId_AsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddCorrelationId();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICorrelationId));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddCorrelationId_ServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddCorrelationId();

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var correlationId = scope.ServiceProvider.GetService<ICorrelationId>();

		// Assert
		correlationId.ShouldNotBeNull();
	}

	[Fact]
	public void TryAddCorrelationId_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddCorrelationId();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddCorrelationId Tests

	#region TryAddETag Tests

	[Fact]
	public void TryAddETag_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddETag());
	}

	[Fact]
	public void TryAddETag_RegistersIETag_AsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddETag();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IETag));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddETag_ServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddETag();

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var etag = scope.ServiceProvider.GetService<IETag>();

		// Assert
		etag.ShouldNotBeNull();
	}

	[Fact]
	public void TryAddETag_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddETag();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddETag Tests

	#region TryAddClientAddress Tests

	[Fact]
	public void TryAddClientAddress_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddClientAddress());
	}

	[Fact]
	public void TryAddClientAddress_RegistersIClientAddress_AsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddClientAddress();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClientAddress));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddClientAddress_ServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddClientAddress();

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var clientAddress = scope.ServiceProvider.GetService<IClientAddress>();

		// Assert
		clientAddress.ShouldNotBeNull();
	}

	[Fact]
	public void TryAddClientAddress_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddClientAddress();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddClientAddress Tests

	#region TryAddLocalClientAddress Tests

	[Fact]
	public void TryAddLocalClientAddress_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddLocalClientAddress());
	}

	[Fact]
	public void TryAddLocalClientAddress_RegistersIClientAddress_AsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddLocalClientAddress();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClientAddress));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void TryAddLocalClientAddress_ServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddLocalClientAddress();

		// Act
		using var provider = services.BuildServiceProvider();
		var clientAddress = provider.GetService<IClientAddress>();

		// Assert
		clientAddress.ShouldNotBeNull();
		clientAddress.ToString().ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void TryAddLocalClientAddress_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddLocalClientAddress();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddLocalClientAddress Tests

	#region AddExcaliburContextServices Tests

	[Fact]
	public void AddExcaliburContextServices_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburContextServices());
	}

	[Fact]
	public void AddExcaliburContextServices_RegistersAllContextServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburContextServices();

		// Assert
		services.ShouldContain(d => d.ServiceType == typeof(IConfigureOptions<TenantContextOptions>));
		services.ShouldContain(d => d.ServiceType == typeof(ICorrelationId));
		services.ShouldContain(d => d.ServiceType == typeof(IETag));
		services.ShouldContain(d => d.ServiceType == typeof(IClientAddress));
	}

	[Fact]
	public void AddExcaliburContextServices_WithTenant_SetsTenantValue()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburContextServices(tenant: "my-tenant");

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<TenantContextOptions>>();

		// Assert
		options.Value.DefaultTenantId.ShouldBe("my-tenant");
	}

	[Fact]
	public void AddExcaliburContextServices_WithLocalAddressFalse_RegistersScopedClientAddress()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburContextServices(localAddress: false);

		// Assert
		var descriptor = services.First(d => d.ServiceType == typeof(IClientAddress));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddExcaliburContextServices_WithLocalAddressTrue_RegistersSingletonClientAddress()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburContextServices(localAddress: true);

		// Assert
		var descriptor = services.First(d => d.ServiceType == typeof(IClientAddress));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddExcaliburContextServices_AllServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburContextServices();

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		var correlationId = scope.ServiceProvider.GetService<ICorrelationId>();
		var etag = scope.ServiceProvider.GetService<IETag>();
		var clientAddress = scope.ServiceProvider.GetService<IClientAddress>();

		// Assert
		correlationId.ShouldNotBeNull();
		etag.ShouldNotBeNull();
		clientAddress.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburContextServices_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburContextServices();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddExcaliburContextServices Tests
}
