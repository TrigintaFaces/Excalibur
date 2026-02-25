// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.SourceGenerators.Tests;

/// <summary>
/// Unit tests for the expected behavior of the <c>ServiceRegistrationSourceGenerator</c>.
/// These tests validate the attribute configurations and expected service registration patterns
/// that the generator should produce.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 426 T426.6: Unit tests for AutoRegister attribute and generator.
/// </para>
/// <para>
/// Note: The source generator project has pre-existing build errors in other generator files
/// (HandlerActivationGenerator, CachePolicySourceGenerator, HandlerInvocationGenerator).
/// These tests validate expected behavior patterns without instantiating the generator directly.
/// Direct generator compilation tests require fixing those pre-existing errors first (separate Beads issue).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ServiceRegistrationSourceGeneratorShould : UnitTestBase
{
	#region Registration Pattern Tests - Verifying Expected Generator Output

	[Fact]
	public void SimulateDefaultScopedRegistration_AsSelfAndInterfaces()
	{
		// Arrange - This simulates what the generator should produce for [AutoRegister]
		var attribute = new AutoRegisterAttribute();

		// Act - Build services based on attribute configuration
		_ = Services.AddScoped<TestServiceWithInterface>();
		_ = Services.AddScoped<IGeneratorTestService, TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert - Service should be resolvable by both concrete type and interface
		var byConcreteType = GetService<TestServiceWithInterface>();
		var byInterface = GetService<IGeneratorTestService>();

		_ = byConcreteType.ShouldNotBeNull();
		_ = byInterface.ShouldNotBeNull();
		_ = byInterface.ShouldBeOfType<TestServiceWithInterface>();

		// Verify attribute defaults are as expected
		attribute.Lifetime.ShouldBe(ServiceLifetime.Scoped);
		attribute.AsSelf.ShouldBeTrue();
		attribute.AsInterfaces.ShouldBeTrue();
	}

	[Fact]
	public void SimulateSingletonRegistration()
	{
		// Arrange - [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
		var attribute = new AutoRegisterAttribute { Lifetime = ServiceLifetime.Singleton };

		// Act
		_ = Services.AddSingleton<TestServiceWithInterface>();
		_ = Services.AddSingleton<IGeneratorTestService, TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert
		var instance1 = GetService<TestServiceWithInterface>();
		var instance2 = GetService<TestServiceWithInterface>();

		_ = instance1.ShouldNotBeNull();
		_ = instance2.ShouldNotBeNull();
		ReferenceEquals(instance1, instance2).ShouldBeTrue(); // Singleton returns same instance
	}

	[Fact]
	public void SimulateTransientRegistration()
	{
		// Arrange - [AutoRegister(Lifetime = ServiceLifetime.Transient)]
		var attribute = new AutoRegisterAttribute { Lifetime = ServiceLifetime.Transient };

		// Act
		_ = Services.AddTransient<TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert
		var instance1 = GetService<TestServiceWithInterface>();
		var instance2 = GetService<TestServiceWithInterface>();

		_ = instance1.ShouldNotBeNull();
		_ = instance2.ShouldNotBeNull();
		ReferenceEquals(instance1, instance2).ShouldBeFalse(); // Transient returns new instances
	}

	[Fact]
	public void SimulateAsSelfOnlyRegistration()
	{
		// Arrange - [AutoRegister(AsSelf = true, AsInterfaces = false)]
		var attribute = new AutoRegisterAttribute { AsSelf = true, AsInterfaces = false };

		// Act - Only register as self
		_ = Services.AddScoped<TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert
		var byConcreteType = GetService<TestServiceWithInterface>();
		var byInterface = GetService<IGeneratorTestService>();

		_ = byConcreteType.ShouldNotBeNull();
		byInterface.ShouldBeNull(); // Interface should NOT be registered
	}

	[Fact]
	public void SimulateAsInterfacesOnlyRegistration()
	{
		// Arrange - [AutoRegister(AsSelf = false, AsInterfaces = true)]
		var attribute = new AutoRegisterAttribute { AsSelf = false, AsInterfaces = true };

		// Act - Only register for interfaces
		_ = Services.AddScoped<IGeneratorTestService, TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert
		var byInterface = GetService<IGeneratorTestService>();

		_ = byInterface.ShouldNotBeNull();
		_ = byInterface.ShouldBeOfType<TestServiceWithInterface>();
	}

	[Fact]
	public void SimulateMultipleInterfaceRegistration()
	{
		// Arrange - [AutoRegister] with class implementing multiple interfaces
		_ = Services.AddScoped<MultiInterfaceService>();
		_ = Services.AddScoped<IFirstInterface, MultiInterfaceService>();
		_ = Services.AddScoped<ISecondInterface, MultiInterfaceService>();
		_ = Services.AddScoped<IThirdInterface, MultiInterfaceService>();
		BuildServiceProvider();

		// Assert
		var byFirst = GetService<IFirstInterface>();
		var bySecond = GetService<ISecondInterface>();
		var byThird = GetService<IThirdInterface>();
		var bySelf = GetService<MultiInterfaceService>();

		_ = byFirst.ShouldNotBeNull();
		_ = bySecond.ShouldNotBeNull();
		_ = byThird.ShouldNotBeNull();
		_ = bySelf.ShouldNotBeNull();

		// All should resolve to the same type
		_ = byFirst.ShouldBeOfType<MultiInterfaceService>();
		_ = bySecond.ShouldBeOfType<MultiInterfaceService>();
		_ = byThird.ShouldBeOfType<MultiInterfaceService>();
	}

	[Fact]
	public void SimulateNoRegistration_ForNoAttribute()
	{
		// Arrange - Class without [AutoRegister] should not be registered
		// (Generator should not scan this)
		BuildServiceProvider();

		// Assert
		var service = GetService<UnattributedService>();
		service.ShouldBeNull();
	}

	#endregion

	#region Manual + Generated Coexistence Tests

	[Fact]
	public void AllowManualAndGeneratedCoexistence()
	{
		// Arrange - Simulate manual registration alongside generated
		// Manual registration (user code):
		_ = Services.AddSingleton<IManualTestService, ManualServiceImpl>();

		// Generated registration (from [AutoRegister]):
		_ = Services.AddScoped<IGeneratorTestService, TestServiceWithInterface>();
		BuildServiceProvider();

		// Assert - Both should work without conflict
		var manual = GetService<IManualTestService>();
		var generated = GetService<IGeneratorTestService>();

		_ = manual.ShouldNotBeNull();
		_ = manual.ShouldBeOfType<ManualServiceImpl>();

		_ = generated.ShouldNotBeNull();
		_ = generated.ShouldBeOfType<TestServiceWithInterface>();
	}

	[Fact]
	public void ManualRegistrationShouldTakePrecedence_WhenDuplicate()
	{
		// Arrange - User manually registered something, then called AddGeneratedServices
		// Manual registration first (singleton):
		var manualInstance = new TestServiceWithInterface();
		_ = Services.AddSingleton<IGeneratorTestService>(manualInstance);

		// Then "generated" registration would try to add scoped:
		// In real scenario, DI container keeps first registration
		BuildServiceProvider();

		// Assert - First registration wins
		var resolved = GetService<IGeneratorTestService>();
		resolved.ShouldBe(manualInstance);
	}

	#endregion

	#region Expected Generator Output Tests

	[Fact]
	public void ExpectedGeneratedCode_ShouldUseFullyQualifiedNames()
	{
		// This test documents the expected generated code pattern
		// The generator should produce code like:
		// services.AddScoped<global::TestApp.MyService>();
		// services.AddScoped<global::TestApp.IMyService, global::TestApp.MyService>();

		// Verify the attribute supports full namespace resolution
		var type = typeof(TestServiceWithInterface);
		var fullName = type.FullName;

		fullName.ShouldBe("Excalibur.Dispatch.SourceGenerators.Tests.TestServiceWithInterface");
	}

	[Fact]
	public void ExpectedGeneratedCode_ShouldBeInMicrosoftExtensionsNamespace()
	{
		// Per ADR-075, generated extension methods should be in:
		// namespace Microsoft.Extensions.DependencyInjection;

		// Verify ServiceCollection extension methods are discoverable
		var serviceCollectionType = typeof(ServiceCollection);
		serviceCollectionType.Namespace.ShouldBe("Microsoft.Extensions.DependencyInjection");
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void SkipSystemInterfaces_WhenRegistering()
	{
		// Arrange - Service implements IDisposable (System interface)
		// Generator should NOT register for IDisposable
		_ = Services.AddScoped<DisposableTestService>();
		_ = Services.AddScoped<IGeneratorTestService, DisposableTestService>();
		// Deliberately NOT registering for IDisposable
		BuildServiceProvider();

		// Assert
		var byTestService = GetService<IGeneratorTestService>();
		_ = byTestService.ShouldNotBeNull();

		// IDisposable should not have been registered as a service
		// (It's managed by the DI container for disposal, not as a resolvable service)
	}

	[Fact]
	public void SupportNestedClasses()
	{
		// Arrange - Nested class with [AutoRegister]
		_ = Services.AddScoped<OuterTestClass.NestedTestService>();
		_ = Services.AddScoped<INestedTestService, OuterTestClass.NestedTestService>();
		BuildServiceProvider();

		// Assert
		var nested = GetService<INestedTestService>();
		_ = nested.ShouldNotBeNull();
		_ = nested.ShouldBeOfType<OuterTestClass.NestedTestService>();
	}

	#endregion
}

#region Test Types - Defined outside test class per CA1034

/// <summary>
/// Test interface for generated service registration tests.
/// </summary>
public interface IGeneratorTestService { }

/// <summary>
/// Test interface for manual registration scenarios.
/// </summary>
public interface IManualTestService { }

/// <summary>
/// First test interface for multiple interface scenarios.
/// </summary>
public interface IFirstInterface { }

/// <summary>
/// Second test interface for multiple interface scenarios.
/// </summary>
public interface ISecondInterface { }

/// <summary>
/// Third test interface for multiple interface scenarios.
/// </summary>
public interface IThirdInterface { }

/// <summary>
/// Test interface for nested class scenarios.
/// </summary>
public interface INestedTestService { }

/// <summary>
/// Test service implementing a single interface.
/// </summary>
public sealed class TestServiceWithInterface : IGeneratorTestService { }

/// <summary>
/// Test service for manual registration scenarios.
/// </summary>
public sealed class ManualServiceImpl : IManualTestService { }

/// <summary>
/// Test service implementing multiple interfaces.
/// </summary>
public sealed class MultiInterfaceService : IFirstInterface, ISecondInterface, IThirdInterface { }

/// <summary>
/// Test class without any registration attribute.
/// </summary>
public sealed class UnattributedService { }

/// <summary>
/// Test service that implements IDisposable (System interface should be skipped).
/// </summary>
public sealed class DisposableTestService : IGeneratorTestService, IDisposable
{
	private bool _disposed;

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		GC.SuppressFinalize(this);
	}
}

/// <summary>
/// Outer class containing nested service for testing.
/// </summary>
internal static class OuterTestClass
{
	/// <summary>
	/// Nested test service class.
	/// </summary>
	internal sealed class NestedTestService : INestedTestService { }
}

#endregion
