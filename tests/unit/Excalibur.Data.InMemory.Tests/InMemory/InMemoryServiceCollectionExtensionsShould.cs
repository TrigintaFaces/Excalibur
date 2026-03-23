// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryServiceCollectionExtensions.AddExcaliburInMemory"/>.
/// </summary>
/// <remarks>
/// Sprint 698 T.7 (i4nq8): Verifies keyed DI registration pattern for InMemory persistence.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterKeyedPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddExcaliburInMemory();
		using var provider = services.BuildServiceProvider();

		// Assert
		var keyed = provider.GetKeyedService<IPersistenceProvider>("inmemory");
		keyed.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterDefaultAliasForPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddExcaliburInMemory();
		using var provider = services.BuildServiceProvider();

		// Assert
		var defaultProvider = provider.GetKeyedService<IPersistenceProvider>("default");
		defaultProvider.ShouldNotBeNull();
	}

	[Fact]
	public void WorkWithoutConfigureDelegate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburInMemory();
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryProviderOptions>>();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyConfigureDelegateWhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburInMemory(o => o.MaxItemsPerCollection = 5000);
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryProviderOptions>>();
		options.Value.MaxItemsPerCollection.ShouldBe(5000);
	}

	[Fact]
	public void BeIdempotentOnDoubleRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddExcaliburInMemory();
		services.AddExcaliburInMemory();
		using var provider = services.BuildServiceProvider();

		// Assert - should not throw
		var keyed = provider.GetKeyedService<IPersistenceProvider>("inmemory");
		keyed.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburInMemory());
	}
}
