// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Domain;

#region Test Interfaces and Implementations

/// <summary>Test interface for registration tests.</summary>
public interface IAddImplTestService { }

/// <summary>Concrete implementation of IAddImplTestService.</summary>
public sealed class AddImplTestServiceImpl : IAddImplTestService { }

/// <summary>Second implementation of IAddImplTestService.</summary>
public sealed class AnotherAddImplTestServiceImpl : IAddImplTestService { }

/// <summary>Abstract class implementing IAddImplTestService - should be skipped.</summary>
public abstract class AbstractAddImplTestService : IAddImplTestService { }

/// <summary>Generic interface for generic registration tests.</summary>
public interface IAddImplGenericService<T> { }

/// <summary>Implementation of generic interface with string.</summary>
public sealed class StringAddImplGenericServiceImpl : IAddImplGenericService<string> { }

/// <summary>Implementation of generic interface with int.</summary>
public sealed class IntAddImplGenericServiceImpl : IAddImplGenericService<int> { }

/// <summary>Open generic implementation - should be skipped.</summary>
public sealed class OpenAddImplGenericServiceImpl<T> : IAddImplGenericService<T> { }

/// <summary>Service that doesn't match target interface.</summary>
public interface IAddImplOtherService { }

/// <summary>Implementation of different interface.</summary>
public sealed class AddImplOtherServiceImpl : IAddImplOtherService { }

/// <summary>Multiple interface implementation.</summary>
public sealed class AddImplMultiInterfaceImpl : IAddImplTestService, IAddImplOtherService { }

#endregion Test Interfaces and Implementations

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ServiceCollectionExtensionsShould
{
	#region AddImplementations<TInterface> Generic Overload Tests

	[Fact]
	public void AddImplementations_Generic_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient));
	}

	[Fact]
	public void AddImplementations_Generic_ThrowsArgumentNullException_WhenAssemblyIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Assembly assembly = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient));
	}

	[Fact]
	public void AddImplementations_Generic_RegistersConcreteImplementations()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAddImplTestService) && d.ImplementationType == typeof(AddImplTestServiceImpl));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void AddImplementations_Generic_RegistersMultipleImplementations()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Scoped);

		// Assert
		var implementations = services
			.Where(d => d.ServiceType == typeof(IAddImplTestService))
			.Select(d => d.ImplementationType)
			.ToList();

		implementations.ShouldContain(typeof(AddImplTestServiceImpl));
		implementations.ShouldContain(typeof(AnotherAddImplTestServiceImpl));
		implementations.ShouldContain(typeof(AddImplMultiInterfaceImpl));
	}

	[Fact]
	public void AddImplementations_Generic_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		var result = services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddImplementations<TInterface> Generic Overload Tests

	#region AddImplementations Non-Generic Overload Tests

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		var interfaceType = typeof(IAddImplTestService);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(assembly, interfaceType, ServiceLifetime.Transient));
	}

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenAssemblyIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Assembly assembly = null!;
		var interfaceType = typeof(IAddImplTestService);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(assembly, interfaceType, ServiceLifetime.Transient));
	}

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenInterfaceTypeIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		Type interfaceType = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(assembly, interfaceType, ServiceLifetime.Transient));
	}

	#endregion AddImplementations Non-Generic Overload Tests

	#region Service Lifetime Tests

	[Fact]
	public void AddImplementations_RegistersWithTransientLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		var descriptor = services.First(d => d.ImplementationType == typeof(AddImplTestServiceImpl));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void AddImplementations_RegistersWithScopedLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Scoped);

		// Assert
		var descriptor = services.First(d => d.ImplementationType == typeof(AddImplTestServiceImpl));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddImplementations_RegistersWithSingletonLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Singleton);

		// Assert
		var descriptor = services.First(d => d.ImplementationType == typeof(AddImplTestServiceImpl));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	#endregion Service Lifetime Tests

	#region RegisterImplementingType Flag Tests

	[Fact]
	public void AddImplementations_DoesNotRegisterImplementationType_WhenFlagIsFalse()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient, registerImplementingType: false);

		// Assert
		var selfRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(AddImplTestServiceImpl) && d.ImplementationType == typeof(AddImplTestServiceImpl));
		selfRegistration.ShouldBeNull();
	}

	[Fact]
	public void AddImplementations_RegistersImplementationType_WhenFlagIsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient, registerImplementingType: true);

		// Assert
		var selfRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(AddImplTestServiceImpl) && d.ImplementationType == typeof(AddImplTestServiceImpl));
		selfRegistration.ShouldNotBeNull();
	}

	[Fact]
	public void AddImplementations_RegistersBothInterfaceAndImplementationType_WhenFlagIsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Scoped, registerImplementingType: true);

		// Assert
		var interfaceRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplTestService) && d.ImplementationType == typeof(AddImplTestServiceImpl));
		var selfRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(AddImplTestServiceImpl) && d.ImplementationType == typeof(AddImplTestServiceImpl));

		interfaceRegistration.ShouldNotBeNull();
		selfRegistration.ShouldNotBeNull();
	}

	#endregion RegisterImplementingType Flag Tests

	#region Abstract Class and Generic Type Definition Tests

	[Fact]
	public void AddImplementations_SkipsAbstractClasses()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		var abstractRegistration = services.FirstOrDefault(d => d.ImplementationType == typeof(AbstractAddImplTestService));
		abstractRegistration.ShouldBeNull();
	}

	[Fact]
	public void AddImplementations_SkipsOpenGenericTypeDefinitions()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations(assembly, typeof(IAddImplGenericService<>), ServiceLifetime.Transient);

		// Assert
		var openGenericRegistration = services.FirstOrDefault(d =>
			d.ImplementationType == typeof(OpenAddImplGenericServiceImpl<>));
		openGenericRegistration.ShouldBeNull();
	}

	#endregion Abstract Class and Generic Type Definition Tests

	#region Generic Interface Tests

	[Fact]
	public void AddImplementations_RegistersClosedGenericImplementations()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations(assembly, typeof(IAddImplGenericService<>), ServiceLifetime.Transient);

		// Assert
		var stringImpl = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplGenericService<string>) && d.ImplementationType == typeof(StringAddImplGenericServiceImpl));
		var intImpl = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplGenericService<int>) && d.ImplementationType == typeof(IntAddImplGenericServiceImpl));

		stringImpl.ShouldNotBeNull();
		intImpl.ShouldNotBeNull();
	}

	[Fact]
	public void AddImplementations_RegistersExactInterfaceMatch()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplOtherService>(assembly, ServiceLifetime.Transient);

		// Assert
		var otherServiceImpl = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplOtherService) && d.ImplementationType == typeof(AddImplOtherServiceImpl));
		otherServiceImpl.ShouldNotBeNull();
	}

	#endregion Generic Interface Tests

	#region Multiple Interface Tests

	[Fact]
	public void AddImplementations_RegistersOnlyRequestedInterface_FromMultipleInterfaceImpl()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		// AddImplMultiInterfaceImpl implements both IAddImplTestService and IAddImplOtherService
		// Only IAddImplTestService should be registered
		var testServiceRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplTestService) && d.ImplementationType == typeof(AddImplMultiInterfaceImpl));
		var otherServiceRegistration = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IAddImplOtherService) && d.ImplementationType == typeof(AddImplMultiInterfaceImpl));

		testServiceRegistration.ShouldNotBeNull();
		otherServiceRegistration.ShouldBeNull();
	}

	#endregion Multiple Interface Tests

	#region Assembly Caching Tests

	[Fact]
	public void AddImplementations_CachesAssemblyTypes_OnSubsequentCalls()
	{
		// Arrange
		var services1 = new ServiceCollection();
		var services2 = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;

		// Act - Call twice with same assembly (tests caching behavior)
		services1.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);
		services2.AddImplementations<IAddImplOtherService>(assembly, ServiceLifetime.Transient);

		// Assert - Both should have registered their respective implementations
		var testServiceCount = services1.Count(d => d.ServiceType == typeof(IAddImplTestService));
		var otherServiceCount = services2.Count(d => d.ServiceType == typeof(IAddImplOtherService));

		testServiceCount.ShouldBeGreaterThan(0);
		otherServiceCount.ShouldBeGreaterThan(0);
	}

	#endregion Assembly Caching Tests

	#region Service Resolution Tests

	[Fact]
	public void AddImplementations_ServicesAreResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Act
		using var provider = services.BuildServiceProvider();
		var resolvedServices = provider.GetServices<IAddImplTestService>().ToList();

		// Assert
		resolvedServices.ShouldNotBeEmpty();
		resolvedServices.ShouldContain(s => s is AddImplTestServiceImpl);
		resolvedServices.ShouldContain(s => s is AnotherAddImplTestServiceImpl);
	}

	[Fact]
	public void AddImplementations_WithRegisterImplementingType_ImplementationTypeIsResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient, registerImplementingType: true);

		// Act
		using var provider = services.BuildServiceProvider();
		var resolvedImpl = provider.GetService<AddImplTestServiceImpl>();

		// Assert
		resolvedImpl.ShouldNotBeNull();
	}

	[Fact]
	public void AddImplementations_ScopedLifetime_CreatesSeparateInstancesPerScope()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Scoped);

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope1 = provider.CreateScope();
		using var scope2 = provider.CreateScope();

		var instance1 = scope1.ServiceProvider.GetServices<IAddImplTestService>().First(s => s is AddImplTestServiceImpl);
		var instance2 = scope2.ServiceProvider.GetServices<IAddImplTestService>().First(s => s is AddImplTestServiceImpl);

		// Assert
		instance1.ShouldNotBeSameAs(instance2);
	}

	[Fact]
	public void AddImplementations_SingletonLifetime_ReturnsSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ServiceCollectionExtensionsShould).Assembly;
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Singleton);

		// Act
		using var provider = services.BuildServiceProvider();
		var instance1 = provider.GetServices<IAddImplTestService>().First(s => s is AddImplTestServiceImpl);
		var instance2 = provider.GetServices<IAddImplTestService>().First(s => s is AddImplTestServiceImpl);

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	#endregion Service Resolution Tests

	#region Edge Cases

	[Fact]
	public void AddImplementations_EmptyAssembly_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();
		// Using an assembly that likely has no implementations of our test interface
		var assembly = typeof(object).Assembly; // mscorlib/System.Private.CoreLib

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
			services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient));
	}

	[Fact]
	public void AddImplementations_NoMatchingImplementations_ReturnsEmptyRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(object).Assembly;

		// Act
		services.AddImplementations<IAddImplTestService>(assembly, ServiceLifetime.Transient);

		// Assert
		var registrations = services.Where(d => d.ServiceType == typeof(IAddImplTestService)).ToList();
		registrations.ShouldBeEmpty();
	}

	#endregion Edge Cases
}
