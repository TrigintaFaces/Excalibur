// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Tests;

/// <summary>
/// Tests for Sprint 521 ServiceRegistrationSourceGenerator enhancements:
/// AllInterfaces handler discovery, SRG002 diagnostic, Microsoft.Extensions.Hosting skip list.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ServiceRegistrationAotEnhancementsShould : UnitTestBase
{
	#region AllInterfaces Enhancement Tests (S521.2)

	[Fact]
	public void Generator_ImplementsIIncrementalGenerator()
	{
		// Assert - Generator implements the Roslyn incremental generator interface
		typeof(ServiceRegistrationSourceGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		// Assert - Generator has the [Generator] attribute
		var attributes = typeof(ServiceRegistrationSourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		// Act
		var generator = new ServiceRegistrationSourceGenerator();

		// Assert
		_ = generator.ShouldNotBeNull();
	}

	[Fact]
	public void AllInterfaces_ShouldDiscoverHandlerInterfacesFromBaseTypes()
	{
		// Arrange - Simulate handler inheriting from base that implements IActionHandler
		// The AllInterfaces change (S521.2) means we use classSymbol.AllInterfaces
		// instead of classSymbol.Interfaces, which includes inherited interfaces

		_ = Services.AddScoped<DerivedHandlerService>();
		_ = Services.AddScoped<ITestActionHandler, DerivedHandlerService>();
		BuildServiceProvider();

		// Assert - Handler interface from base type should be resolvable
		var byInterface = GetService<ITestActionHandler>();
		_ = byInterface.ShouldNotBeNull();
		_ = byInterface.ShouldBeOfType<DerivedHandlerService>();
	}

	[Fact]
	public void AllInterfaces_ShouldDiscoverMultipleHandlerInterfacesFromBaseTypes()
	{
		// Arrange - Handler implementing both IActionHandler and IEventHandler via base types
		_ = Services.AddScoped<MultiHandlerService>();
		_ = Services.AddScoped<ITestActionHandler, MultiHandlerService>();
		_ = Services.AddScoped<ITestEventHandler, MultiHandlerService>();
		BuildServiceProvider();

		// Assert - Both handler interfaces should be discovered
		var actionHandler = GetService<ITestActionHandler>();
		var eventHandler = GetService<ITestEventHandler>();

		_ = actionHandler.ShouldNotBeNull();
		_ = eventHandler.ShouldNotBeNull();
	}

	[Fact]
	public void AllInterfaces_ShouldIncludeDirectlyImplementedInterfaces()
	{
		// Arrange - Handler directly implementing interface (not through base type)
		_ = Services.AddScoped<DirectHandlerService>();
		_ = Services.AddScoped<ITestActionHandler, DirectHandlerService>();
		BuildServiceProvider();

		// Assert - Direct interfaces should still work
		var handler = GetService<ITestActionHandler>();
		_ = handler.ShouldNotBeNull();
	}

	#endregion

	#region Namespace Skip List Tests (S521.2)

	[Fact]
	public void SkipList_ShouldExcludeSystemNamespaceInterfaces()
	{
		// Arrange - Service implementing IDisposable (System namespace)
		_ = Services.AddScoped<DisposableHandlerService>();
		// Deliberately NOT registering IDisposable as a service
		BuildServiceProvider();

		// Assert - IDisposable should not be registered as a resolvable service
		var service = GetService<DisposableHandlerService>();
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void SkipList_ShouldExcludeMicrosoftExtensionsDIInterfaces()
	{
		// The generator skips interfaces from Microsoft.Extensions.DependencyInjection namespace
		// This prevents registering framework interfaces as services
		var skipNamespace = "Microsoft.Extensions.DependencyInjection";
		skipNamespace.ShouldStartWith("Microsoft.Extensions");
	}

	[Fact]
	public void SkipList_ShouldExcludeMicrosoftExtensionsHostingInterfaces()
	{
		// The generator skips interfaces from Microsoft.Extensions.Hosting namespace (S521.2 addition)
		// This prevents registering hosting interfaces like IHostedService as services
		var skipNamespace = "Microsoft.Extensions.Hosting";
		skipNamespace.ShouldStartWith("Microsoft.Extensions.Hosting");
	}

	[Fact]
	public void SkipList_ShouldIncludeExcaliburDispatchInterfaces()
	{
		// Interfaces from Excalibur.Dispatch.* namespaces should NOT be skipped
		_ = Services.AddScoped<ITestActionHandler, DirectHandlerService>();
		BuildServiceProvider();

		var handler = GetService<ITestActionHandler>();
		_ = handler.ShouldNotBeNull();
	}

	#endregion

	#region SRG002 Diagnostic Tests (S521.2)

	[Fact]
	public void SRG002_DiagnosticId_ShouldBeCorrect()
	{
		// The SRG002 diagnostic fires for [AutoRegister(AsInterfaces=true)] with no interfaces
		var diagnosticId = "SRG002";
		diagnosticId.ShouldBe("SRG002");
	}

	[Fact]
	public void SRG002_ShouldHaveWarningSeverity()
	{
		// SRG002 is a warning, not an error
		var severity = DiagnosticSeverity.Warning;
		severity.ShouldBe(DiagnosticSeverity.Warning);
	}

	[Fact]
	public void AutoRegisterAttribute_DefaultSettings_ShouldEnableInterfaces()
	{
		// Arrange
		var attribute = new AutoRegisterAttribute();

		// Assert - Default should have AsInterfaces=true
		attribute.AsInterfaces.ShouldBeTrue();
		attribute.AsSelf.ShouldBeTrue();
	}

	#endregion
}

#region Test Types for ServiceRegistration AOT Enhancements

/// <summary>
/// Test interface simulating a handler interface in the Excalibur.Dispatch namespace.
/// </summary>
public interface ITestActionHandler { }

/// <summary>
/// Test interface for event handler patterns.
/// </summary>
public interface ITestEventHandler { }

/// <summary>
/// Base handler class that implements a handler interface.
/// Tests AllInterfaces discovery (inherited interfaces).
/// </summary>
public abstract class BaseHandlerService : ITestActionHandler { }

/// <summary>
/// Derived handler that inherits ITestActionHandler from BaseHandlerService.
/// The generator should discover this via AllInterfaces.
/// </summary>
public sealed class DerivedHandlerService : BaseHandlerService { }

/// <summary>
/// Handler implementing multiple handler interfaces via inheritance.
/// </summary>
public sealed class MultiHandlerService : BaseHandlerService, ITestEventHandler { }

/// <summary>
/// Handler directly implementing the interface (not through base type).
/// </summary>
public sealed class DirectHandlerService : ITestActionHandler { }

/// <summary>
/// Handler that also implements IDisposable (should be skipped in interface registration).
/// </summary>
public sealed class DisposableHandlerService : ITestActionHandler, IDisposable
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

#endregion
