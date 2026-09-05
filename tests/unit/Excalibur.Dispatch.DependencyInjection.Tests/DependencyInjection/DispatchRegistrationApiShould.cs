// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the Dispatch registration API (AddDispatch overloads).
/// Validates service registration, fluent chaining, and handler auto-discovery.
/// </summary>
/// <remarks>
/// Sprint 443 S443.3: Add API tests for the unified Dispatch registration API.
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
[Trait("Priority", "0")]
public sealed class DispatchRegistrationApiShould : UnitTestBase
{
	#region AddDispatch() - No Parameters

	[Fact]
	public void RegisterCoreServices_WhenAddDispatchCalledWithNoParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch();

		// Assert
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = provider.GetService<IDispatchPipeline>().ShouldNotBeNull();
	}

	[Fact]
	public void ReturnIServiceCollection_WhenAddDispatchCalledWithNoParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddDispatch();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void SupportFluentChaining_WhenAddDispatchCalledWithNoParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - verify chaining works
		var result = services
			.AddDispatch()
			.AddSingleton<ITestService, TestService>();

		// Assert
		result.ShouldBeSameAs(services);
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<ITestService>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHandlerRegistry_WhenAddDispatchCalledWithNoParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch();

		// Assert
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IHandlerRegistry>().ShouldNotBeNull();
	}

	#endregion

	#region AddDispatch(Action<IDispatchBuilder>) - With Configuration

	[Fact]
	public void InvokeConfigureAction_WhenAddDispatchCalledWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var wasInvoked = false;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			wasInvoked = true;
		});

		// Assert
		wasInvoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterCoreServices_WhenAddDispatchCalledWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch => { });

		// Assert
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = provider.GetService<IDispatchPipeline>().ShouldNotBeNull();
	}

	[Fact]
	public void ReturnIServiceCollection_WhenAddDispatchCalledWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddDispatch(dispatch => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void SupportFluentChaining_WhenAddDispatchCalledWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services
			.AddDispatch(dispatch =>
			{
				_ = dispatch.AddHandlersFromAssembly(typeof(DispatchRegistrationApiShould).Assembly);
			})
			.AddSingleton<ITestService, TestService>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void PassValidDispatchBuilder_ToConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			capturedBuilder = dispatch;
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
		capturedBuilder.Services.ShouldBeSameAs(services);
	}

	[Fact]
	public void HandleNullConfigureAction_WhenAddDispatchCalledWithNull()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - null configure action should be allowed
		Action<IDispatchBuilder>? nullConfigure = null;
		var exception = Record.Exception(() => services.AddDispatch(nullConfigure));

		// Assert
		exception.ShouldBeNull();
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IDispatcher>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_WithConfiguration()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddDispatch(dispatch => { }))
			.ParamName.ShouldBe("services");
	}

	#endregion

	#region AddDispatch(params Assembly[]) - Assembly Scanning

	[Fact]
	public void RegisterCoreServices_WhenAddDispatchCalledWithAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var testAssembly = typeof(DispatchRegistrationApiShould).Assembly;

		// Act
		_ = services.AddDispatch(testAssembly);

		// Assert
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IDispatcher>().ShouldNotBeNull();
	}

	[Fact]
	public void ReturnIServiceCollection_WhenAddDispatchCalledWithAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddDispatch(typeof(DispatchRegistrationApiShould).Assembly);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AcceptNullAssemblies_WithoutThrowing()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		Assembly[]? nullAssemblies = null;

		// Act
		var exception = Record.Exception(() => services.AddDispatch(nullAssemblies));

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void AcceptEmptyAssemblyArray_WithoutThrowing()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var exception = Record.Exception(() => services.AddDispatch());

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_WithAssemblies()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddDispatch(typeof(DispatchRegistrationApiShould).Assembly))
			.ParamName.ShouldBe("services");
	}

	#endregion

	#region AddHandlersFromAssembly() - Handler Auto-Registration

	[Fact]
	public void AutoRegisterHandlers_WhenAddHandlersFromAssemblyCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IActionHandler<RegistrationTestAction>>();
		_ = handler.ShouldNotBeNull();
		_ = handler.ShouldBeOfType<RegistrationTestActionHandler>();
	}

	[Fact]
	public void RegisterHandlersWithTransientLifetime_ByDefault()
	{
		// This replaces a test that asserted the default was Scoped. The default changed to Transient by
		// SoftwareArchitect ruling: scope ownership belongs to the framework, not the registration, because
		// HandlerScopeResolver already opens a scope whenever a handler's dependency graph reaches a Scoped
		// service. Scoped registration was buying protection that already existed and charging a
		// per-dispatch scope for it.
		//
		// Note what this test does NOT assert. It cannot assert "two resolutions give different instances"
		// the way the old Scoped test did, because a stateless handler under a Transient registration is
		// eligible for the framework's singleton promotion (on by default), so identity is legitimately
		// unstable here. Asserting identity would bind an optimisation rather than the registration
		// contract, and would go red the moment promotion changed. The descriptor is the contract; the
		// sibling test RegisterHandlersWithTransientLifetime_WhenSpecified covers instance behaviour with
		// promotion explicitly disabled.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// AutoPromote is disabled for the same reason the sibling Transient test disables it: promotion
		// rewrites the descriptor for an eligible stateless handler, so leaving it on would mean asserting
		// against whatever promotion produced rather than against the registration default under test.
		_ = services.AddDispatch(dispatch =>
		{
			dispatch.WithOptions(o => o.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = false);
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly);
		});

		var descriptor = services.LastOrDefault(d =>
			d.ServiceType == typeof(IActionHandler<RegistrationTestAction>));

		_ = descriptor.ShouldNotBeNull(
			"assembly scanning must register the handler interface, or this assertion passes vacuously");
		descriptor.Lifetime.ShouldBe(
			ServiceLifetime.Transient,
			"AddHandlersFromAssembly defaults to Transient; the framework must not impose the strongest "
			+ "lifetime on a consumer who stated none, when the resolver already supplies a scope to any "
			+ "handler that actually needs one");
	}

	[Fact]
	public void StillHonorAnExplicitlyRequestedScopedLifetime()
	{
		// LIVENESS PARTNER. The default moving to Transient must not mean Scoped became unreachable — a
		// consumer who asks for it explicitly must still get it, or the change has removed a capability
		// rather than changed a default. Without this arm, an implementation that hardcoded Transient and
		// ignored the parameter entirely would satisfy the test above.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDispatch(dispatch =>
		{
			dispatch.WithOptions(o => o.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = false);
			_ = dispatch.AddHandlersFromAssembly(
				typeof(RegistrationTestActionHandler).Assembly,
				ServiceLifetime.Scoped);
		});

		var descriptor = services.LastOrDefault(d =>
			d.ServiceType == typeof(IActionHandler<RegistrationTestAction>));

		_ = descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(
			ServiceLifetime.Scoped,
			"the lifetime parameter must still be honoured; only the DEFAULT changed");
	}

	[Fact]
	public void RegisterHandlersWithTransientLifetime_WhenSpecified()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- disable AutoPromote so transient lifetime is preserved (Sprint 679 T.12)
		_ = services.AddDispatch(dispatch =>
		{
			dispatch.WithOptions(o => o.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = false);
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly, ServiceLifetime.Transient);
		});

		// Assert - Transient handlers should be different on each resolution
		var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		var handler1 = scope.ServiceProvider.GetService<IActionHandler<RegistrationTestAction>>();
		var handler2 = scope.ServiceProvider.GetService<IActionHandler<RegistrationTestAction>>();

		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public void AutoRegisterEventHandlers_WhenAddHandlersFromAssemblyCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestEventHandler).Assembly);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IEventHandler<RegistrationTestDomainEvent>>();
		_ = handler.ShouldNotBeNull();
	}

	[Fact]
	public void AutoRegisterActionHandlersWithResponse_WhenAddHandlersFromAssemblyCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestQueryHandler).Assembly);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IActionHandler<RegistrationTestQuery, RegistrationTestQueryResult>>();
		_ = handler.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnDispatchBuilder_ForFluentChaining_AfterAddHandlersFromAssembly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? result = null;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			result = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly);
		});

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForAddHandlersFromAssembly()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenAssemblyIsNull_ForAddHandlersFromAssembly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act & Assert
		_ = services.AddDispatch(dispatch =>
		{
			Should.Throw<ArgumentNullException>(() => dispatch.AddHandlersFromAssembly(null!))
				.ParamName.ShouldBe("assemblies");
		});
	}

	[Fact]
	public void IgnoreAbstractHandlers_WhenAddHandlersFromAssemblyCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationAbstractTestHandler).Assembly);
		});

		// Assert - abstract handlers should not be registered
		var serviceDescriptor = services.FirstOrDefault(d =>
			d.ImplementationType == typeof(RegistrationAbstractTestHandler));
		serviceDescriptor.ShouldBeNull();
	}

	[Fact]
	public void IgnoreInterfaceHandlers_WhenAddHandlersFromAssemblyCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(IRegistrationInterfaceHandler).Assembly);
		});

		// Assert - interface handlers should not be registered
		var serviceDescriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IRegistrationInterfaceHandler));
		serviceDescriptor.ShouldBeNull();
	}

	#endregion

	#region Multiple AddDispatch Calls

	[Fact]
	public void BeIdempotent_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - call multiple times
		_ = services.AddDispatch();
		_ = services.AddDispatch();
		_ = services.AddDispatch();

		// Assert - should not throw and core services should be registered once
		var provider = services.BuildServiceProvider();
		var dispatchers = provider.GetServices<IDispatcher>().ToList();
		dispatchers.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportMixedRegistrationStyles()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - mix different registration styles
		_ = services.AddDispatch();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(RegistrationTestActionHandler).Assembly);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		_ = provider.GetService<IDispatcher>().ShouldNotBeNull();
		_ = provider.GetService<IActionHandler<RegistrationTestAction>>().ShouldNotBeNull();
	}

	#endregion

	#region Test Fixtures

	// Test services for fluent chaining verification
	private interface ITestService { }
	private sealed class TestService : ITestService { }

	#endregion
}

#region Test Fixtures (must be in separate file scope for public visibility)

// Test action types - public for handler registration scanning
public sealed class RegistrationTestAction : IDispatchAction { }
public sealed class RegistrationTestQuery : IDispatchAction<RegistrationTestQueryResult> { }
public sealed class RegistrationTestQueryResult { }

// Test domain event
[MessageName("Test.RegistrationTestDomainEvent")]
public sealed class RegistrationTestDomainEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = Guid.NewGuid().ToString();
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public IDictionary<string, object>? Metadata { get; init; }
}

// Test handler implementations - public for assembly scanning
public sealed class RegistrationTestActionHandler : IActionHandler<RegistrationTestAction>
{
	public Task HandleAsync(RegistrationTestAction action, CancellationToken cancellationToken) =>
		Task.CompletedTask;
}

public sealed class RegistrationTestQueryHandler : IActionHandler<RegistrationTestQuery, RegistrationTestQueryResult>
{
	public Task<RegistrationTestQueryResult> HandleAsync(RegistrationTestQuery action, CancellationToken cancellationToken) =>
		Task.FromResult(new RegistrationTestQueryResult());
}

public sealed class RegistrationTestEventHandler : IEventHandler<RegistrationTestDomainEvent>
{
	public Task HandleAsync(RegistrationTestDomainEvent @event, CancellationToken cancellationToken) =>
		Task.CompletedTask;
}

// Abstract handler (should not be registered)
public abstract class RegistrationAbstractTestHandler : IActionHandler<RegistrationTestAction>
{
	public abstract Task HandleAsync(RegistrationTestAction action, CancellationToken cancellationToken);
}

// Interface handler (should not be registered)
public interface IRegistrationInterfaceHandler : IActionHandler<RegistrationTestAction> { }

#endregion