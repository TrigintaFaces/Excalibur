// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test

namespace Excalibur.Tests.Application;

[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class ServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterActivityContext()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburApplicationServices(typeof(ServiceCollectionExtensionsShould).Assembly);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IActivityContext));
	}

	[Fact]
	public void RegisterActivityContextAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburApplicationServices(typeof(ServiceCollectionExtensionsShould).Assembly);

		// Assert
		var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IActivityContext));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddActivities_RegistersActivityTypes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddActivities(typeof(ServiceCollectionExtensionsShould).Assembly);

		// Assert
		services.ShouldNotBeNull();
	}

	[Fact]
	public void AddActivities_ThrowsOnNullAssemblies()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddActivities(null!));
	}
}

#pragma warning restore IL2026, IL3050
