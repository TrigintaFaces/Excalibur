// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;

namespace Excalibur.Hosting.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ExcaliburBuilder"/> and <see cref="IExcaliburBuilder"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Builders")]
public sealed class ExcaliburBuilderShould : UnitTestBase
{
	[Fact]
	public void ExposeServicesProperty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcalibur(builder =>
		{
			// Assert
			builder.Services.ShouldNotBeNull();
			builder.Services.ShouldBeSameAs(services);
		});
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcalibur(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcalibur(null!));
	}

	[Fact]
	public void RegisterDispatchPrimitives()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcalibur(_ => { });

		// Assert - Dispatch primitives should be registered
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterExcaliburOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcalibur(_ => { });

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType.Name.Contains("IConfigureOptions") ||
			(sd.ServiceType.FullName != null && sd.ServiceType.FullName.Contains("ExcaliburOptions")));
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builderInstance = (IExcaliburBuilder?)null;

		// Act
		services.AddExcalibur(builder =>
		{
			builderInstance = builder;
		});

		// Assert
		builderInstance.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburHealthChecksWithDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburHealthChecks();

		// Assert - Health checks should be registered
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void AddExcaliburHealthChecksWithCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var configurationCalled = false;

		// Act
		services.AddExcaliburHealthChecks(hc =>
		{
			configurationCalled = true;
		});

		// Assert
		configurationCalled.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNullForHealthChecks()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburHealthChecks());
	}

	[Fact]
	public void AddExcaliburBaseServicesWithAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[] { typeof(ExcaliburBuilderShould).Assembly };

		// Act
		services.AddExcaliburBaseServices(assemblies);

		// Assert - Services should be registered
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNullForBaseServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburBaseServices([]));
	}
}
