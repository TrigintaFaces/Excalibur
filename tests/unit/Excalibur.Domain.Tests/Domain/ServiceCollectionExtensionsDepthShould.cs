// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Tests.Domain;

/// <summary>
/// Depth coverage tests for <see cref="ServiceCollectionExtensions"/>.
/// Covers AddImplementations with various configurations, caching, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddImplementations_Generic_RegistersInterfaceImplementations()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddImplementations<IDepthTestService>(
			typeof(DepthTestServiceImpl).Assembly,
			ServiceLifetime.Singleton);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDepthTestService));
	}

	[Fact]
	public void AddImplementations_WithRegisterImplementingType_RegistersConcrete()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddImplementations<IDepthTestService>(
			typeof(DepthTestServiceImpl).Assembly,
			ServiceLifetime.Transient,
			registerImplementingType: true);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDepthTestService));
		services.ShouldContain(sd => sd.ServiceType == typeof(DepthTestServiceImpl));
	}

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			ServiceCollectionExtensions.AddImplementations(
				null!,
				typeof(IDepthTestService).Assembly,
				typeof(IDepthTestService),
				ServiceLifetime.Singleton));
	}

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenAssemblyIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(
				null!,
				typeof(IDepthTestService),
				ServiceLifetime.Singleton));
	}

	[Fact]
	public void AddImplementations_ThrowsArgumentNullException_WhenInterfaceTypeIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddImplementations(
				typeof(IDepthTestService).Assembly,
				null!,
				ServiceLifetime.Singleton));
	}

	[Fact]
	public void AddImplementations_SkipsAbstractTypes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddImplementations<IDepthTestService>(
			typeof(DepthTestServiceImpl).Assembly,
			ServiceLifetime.Singleton);

		// Assert — abstract types should not be registered
		services.ShouldNotContain(sd => sd.ImplementationType == typeof(AbstractDepthTestService));
	}

	[Fact]
	public void AddImplementations_ReturnsServices_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddImplementations<IDepthTestService>(
			typeof(DepthTestServiceImpl).Assembly,
			ServiceLifetime.Singleton);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddImplementations_UsesCorrectLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddImplementations<IDepthTestService>(
			typeof(DepthTestServiceImpl).Assembly,
			ServiceLifetime.Scoped);

		// Assert
		var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IDepthTestService));
		descriptor.ShouldNotBeNull();
		descriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}
}

// Public test types for assembly scanning (GetExportedTypes only returns public types)
#pragma warning disable CA1515 // Consider making public types internal
public interface IDepthTestService
{
	void DoWork();
}

public sealed class DepthTestServiceImpl : IDepthTestService
{
	public void DoWork() { }
}

public abstract class AbstractDepthTestService : IDepthTestService
{
	public abstract void DoWork();
}
#pragma warning restore CA1515
