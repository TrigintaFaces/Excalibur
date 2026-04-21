// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Tests for <see cref="AotCompatibilityExtensions"/> covering DI registration,
/// RuntimeFeature branching, and helper methods.
/// Sprint 739 B.5: Wave 4 AOT-safe dispatch path tests.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Feature, TestFeatures.AOT)]
public sealed class AotCompatibilityExtensionsShould
{
	#region ConfigureHandlerInvoker Tests

	[Fact]
	public void ConfigureHandlerInvoker_RegistersHandlerInvokerInJitMode()
	{
		// In JIT mode (standard test execution), RuntimeFeature.IsDynamicCodeSupported is true
		// so ConfigureHandlerInvoker should register HandlerInvoker (not HandlerInvokerAot)
		var services = new ServiceCollection();

		services.ConfigureHandlerInvoker();

		var provider = services.BuildServiceProvider();
		var invoker = provider.GetService<IHandlerInvoker>();

		invoker.ShouldNotBeNull();

		// In JIT mode, should be the reflection-based HandlerInvoker
		if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
		{
			invoker.ShouldBeOfType<HandlerInvoker>();
		}
		else
		{
			invoker.ShouldBeOfType<HandlerInvokerAot>();
		}
	}

	[Fact]
	public void ConfigureHandlerInvoker_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();

		var result = services.ConfigureHandlerInvoker();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ConfigureHandlerInvoker_DoesNotOverwriteExistingRegistration()
	{
		var services = new ServiceCollection();
		var fakeInvoker = A.Fake<IHandlerInvoker>();
		services.AddSingleton(fakeInvoker);

		// TryAddSingleton should not overwrite
		services.ConfigureHandlerInvoker();

		var provider = services.BuildServiceProvider();
		var invoker = provider.GetService<IHandlerInvoker>();

		invoker.ShouldBeSameAs(fakeInvoker);
	}

	[Fact]
	public void ConfigureHandlerInvoker_RegistersAsSingleton()
	{
		var services = new ServiceCollection();
		services.ConfigureHandlerInvoker();

		var descriptor = services.ShouldHaveSingleItem();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
		descriptor.ServiceType.ShouldBe(typeof(IHandlerInvoker));
	}

	#endregion

	#region IsRunningAot Tests

	[Fact]
	public void IsRunningAot_ReturnsFalseInJitMode()
	{
		// Standard test execution is always JIT
		if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
		{
			AotCompatibilityExtensions.IsRunningAot().ShouldBeFalse();
		}
	}

	[Fact]
	public void IsRunningAot_ReflectsRuntimeFeatureState()
	{
		// IsRunningAot should always be the inverse of RuntimeFeature.IsDynamicCodeSupported
		var expected = !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;

		AotCompatibilityExtensions.IsRunningAot().ShouldBe(expected);
	}

	#endregion

	#region RegisterDiscoveredHandlers Tests

	[Fact]
	public void RegisterDiscoveredHandlers_ReturnsSameServiceCollection()
	{
		var services = new ServiceCollection();

		var result = services.RegisterDiscoveredHandlers(typeof(AotCompatibilityExtensionsShould).Assembly);

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterDiscoveredHandlers_RegistersHandlerRegistry()
	{
		var services = new ServiceCollection();

		services.RegisterDiscoveredHandlers(typeof(AotCompatibilityExtensionsShould).Assembly);

		var provider = services.BuildServiceProvider();
		var registry = provider.GetService<IHandlerRegistry>();

		registry.ShouldNotBeNull();
	}

	#endregion
}
