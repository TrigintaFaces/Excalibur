// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;
using Excalibur.Domain.Concurrency;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Domain.Extensions;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionContextExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ServiceCollectionContextExtensionsShould
{
	#region TryAddTenantId Tests

	[Fact]
	public void TryAddTenantId_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddTenantId());
	}

	[Fact]
	public void TryAddTenantId_RegistersITenantId_AsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddTenantId();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITenantId));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddTenantId_RegistersWithDefaultTenant()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddTenantId();

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert - Uses TenantDefaults.DefaultTenantId when no argument provided
		tenantId.Value.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void TryAddTenantId_RegistersWithSpecifiedTenant()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddTenantId("my-tenant");

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert
		tenantId.Value.ShouldBe("my-tenant");
	}

	[Fact]
	public void TryAddTenantId_DoesNotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddTenantId("first");
		services.TryAddTenantId("second");

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert - First registration should win
		tenantId.Value.ShouldBe("first");
	}

	[Fact]
	public void TryAddTenantId_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddTenantId();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddTenantId Tests

	#region TryAddTenantId Factory Overload Tests

	[Fact]
	public void TryAddTenantId_WithFactory_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddTenantId(sp => "tenant-1"));
	}

	[Fact]
	public void TryAddTenantId_WithFactory_ThrowsArgumentNullException_WhenResolverIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.TryAddTenantId((Func<IServiceProvider, string>)null!));
	}

	[Fact]
	public void TryAddTenantId_WithFactory_RegistersITenantId_AsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.TryAddTenantId(sp => "tenant-1");

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITenantId));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void TryAddTenantId_WithFactory_ResolvesPerScope()
	{
		// Arrange
		var callCount = 0;
		var services = new ServiceCollection();
		services.TryAddTenantId(sp =>
		{
			callCount++;
			return $"tenant-{callCount}";
		});

		// Act
		using var provider = services.BuildServiceProvider();

		using var scope1 = provider.CreateScope();
		var tenant1 = scope1.ServiceProvider.GetRequiredService<ITenantId>();

		using var scope2 = provider.CreateScope();
		var tenant2 = scope2.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert - Each scope gets its own resolution
		tenant1.Value.ShouldBe("tenant-1");
		tenant2.Value.ShouldBe("tenant-2");
	}

	[Fact]
	public void TryAddTenantId_WithFactory_DoesNotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.TryAddTenantId("first");
		services.TryAddTenantId(sp => "second");

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert - First registration should win
		tenantId.Value.ShouldBe("first");
	}

	[Fact]
	public void TryAddTenantId_WithFactory_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.TryAddTenantId(sp => "tenant-1");

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion TryAddTenantId Factory Overload Tests

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
		services.ShouldContain(d => d.ServiceType == typeof(ITenantId));
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
		using var scope = provider.CreateScope();
		var tenantId = scope.ServiceProvider.GetRequiredService<ITenantId>();

		// Assert
		tenantId.Value.ShouldBe("my-tenant");
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

		var tenantId = scope.ServiceProvider.GetService<ITenantId>();
		var correlationId = scope.ServiceProvider.GetService<ICorrelationId>();
		var etag = scope.ServiceProvider.GetService<IETag>();
		var clientAddress = scope.ServiceProvider.GetService<IClientAddress>();

		// Assert
		tenantId.ShouldNotBeNull();
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
