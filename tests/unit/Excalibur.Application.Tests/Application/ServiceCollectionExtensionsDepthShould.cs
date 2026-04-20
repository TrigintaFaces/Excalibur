// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test (assembly scanning)

namespace Excalibur.Tests.Application;

/// <summary>
/// Depth unit tests for the canonical AddExcalibur(...) + ScanAssemblies(...) path that
/// replaced the deleted AddExcaliburApplicationServices(...) aggregator (S804 bd-sdhocq A7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class ServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddExcalibur_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcalibur(b => b.ScanAssemblies(typeof(ServiceCollectionExtensionsDepthShould).Assembly));

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcalibur_RegistersActivityContextAsScoped()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcalibur(b => b.ScanAssemblies(typeof(ServiceCollectionExtensionsDepthShould).Assembly));

		// Assert
		var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IActivityContext));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddExcalibur_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — call twice
		services.AddExcalibur(b => b.ScanAssemblies(typeof(ServiceCollectionExtensionsDepthShould).Assembly));
		services.AddExcalibur(b => b.ScanAssemblies(typeof(ServiceCollectionExtensionsDepthShould).Assembly));

		// Assert — TryAddScoped should prevent duplicates
		var activityContextCount = services.Count(s => s.ServiceType == typeof(IActivityContext));
		activityContextCount.ShouldBe(1);
	}

	[Fact]
	public void AddActivities_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddActivities(typeof(ServiceCollectionExtensionsDepthShould).Assembly);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddActivities_WithEmptyAssembly_RegistersNoActivities()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — use an assembly that has no IActivity implementations
		services.AddActivities(typeof(string).Assembly);

		// Assert
		services.Where(s => s.ServiceType == typeof(IActivity)).ShouldBeEmpty();
	}
}

#pragma warning restore IL2026, IL3050
