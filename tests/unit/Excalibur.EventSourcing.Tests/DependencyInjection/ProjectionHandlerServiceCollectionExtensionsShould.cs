// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="ProjectionHandlerServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHandlerServiceCollectionExtensionsShould : UnitTestBase
{
	// --- Test types for assembly scanning ---

	private sealed class ConcreteProjectionHandlerA : IProjectionHandler { }

	private sealed class ConcreteProjectionHandlerB : IProjectionHandler { }

	private abstract class AbstractProjectionHandler : IProjectionHandler { }

	private interface IExtendedProjectionHandler : IProjectionHandler { }

	// --- Null guard tests ---

	[Fact]
	public void ThrowOnNullServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			ProjectionHandlerServiceCollectionExtensions.AddProjectionHandlersFromAssembly(
				null!, typeof(ConcreteProjectionHandlerA).Assembly));
	}

	[Fact]
	public void ThrowOnNullAssembly()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddProjectionHandlersFromAssembly(null!));
	}

	// --- Registration tests ---

	[Fact]
	public void RegisterConcreteHandlersFromAssembly()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(typeof(ConcreteProjectionHandlerA).Assembly);

		// Assert - concrete type registrations exist
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteProjectionHandlerA) &&
			sd.ImplementationType == typeof(ConcreteProjectionHandlerA));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteProjectionHandlerB) &&
			sd.ImplementationType == typeof(ConcreteProjectionHandlerB));
	}

	[Fact]
	public void RegisterHandlersAsIProjectionHandlerForEnumerableResolution()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(typeof(ConcreteProjectionHandlerA).Assembly);

		// Assert - IProjectionHandler registrations exist for each concrete type
		services.Where(sd => sd.ServiceType == typeof(IProjectionHandler))
			.Count().ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void SkipAbstractClasses()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(typeof(AbstractProjectionHandler).Assembly);

		// Assert - abstract type should NOT be registered
		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(AbstractProjectionHandler));
	}

	[Fact]
	public void SkipInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(typeof(IExtendedProjectionHandler).Assembly);

		// Assert - interface type should NOT be registered
		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IExtendedProjectionHandler));
	}

	// --- Idempotency tests ---

	[Fact]
	public void NotDuplicateRegistrationsWhenCalledTwice()
	{
		// Arrange
		var services = new ServiceCollection();
		var assembly = typeof(ConcreteProjectionHandlerA).Assembly;

		// Act - register same assembly twice
		services.AddProjectionHandlersFromAssembly(assembly);
		services.AddProjectionHandlersFromAssembly(assembly);

		// Assert - TryAdd means no duplicate concrete registrations
		services.Count(sd =>
			sd.ServiceType == typeof(ConcreteProjectionHandlerA) &&
			sd.ImplementationType == typeof(ConcreteProjectionHandlerA)).ShouldBe(1);
	}

	// --- ServiceLifetime tests ---

	[Fact]
	public void DefaultToSingletonLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(typeof(ConcreteProjectionHandlerA).Assembly);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteProjectionHandlerA) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void RespectCustomLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddProjectionHandlersFromAssembly(
			typeof(ConcreteProjectionHandlerA).Assembly,
			ServiceLifetime.Transient);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ConcreteProjectionHandlerA) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	// --- Fluent chaining test ---

	[Fact]
	public void ReturnSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddProjectionHandlersFromAssembly(
			typeof(ConcreteProjectionHandlerA).Assembly);

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- Empty assembly test ---

	[Fact]
	public void HandleAssemblyWithNoHandlersGracefully()
	{
		// Arrange
		var services = new ServiceCollection();
		// System.Runtime has no IProjectionHandler implementations
		var emptyAssembly = typeof(int).Assembly;
		var countBefore = services.Count;

		// Act
		services.AddProjectionHandlersFromAssembly(emptyAssembly);

		// Assert - no new registrations added
		services.Count.ShouldBe(countBefore);
	}
}
