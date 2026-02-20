// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;

namespace Excalibur.Caching.Tests.Projections;

/// <summary>
/// Unit tests for <see cref="ExcaliburProjectionCachingServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ExcaliburProjectionCachingServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterProjectionCacheInvalidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburProjectionCaching();

		// Assert
		services.ShouldContain(d =>
			d.ServiceType == typeof(IProjectionCacheInvalidator) &&
			d.ImplementationType == typeof(ProjectionCacheInvalidator));
	}

	[Fact]
	public void RegisterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburProjectionCaching();

		// Assert
		var descriptor = services.First(d => d.ServiceType == typeof(IProjectionCacheInvalidator));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void NotRegisterDuplicate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburProjectionCaching();
		services.AddExcaliburProjectionCaching(); // Second call

		// Assert
		services.Count(d => d.ServiceType == typeof(IProjectionCacheInvalidator)).ShouldBe(1);
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburProjectionCaching();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
