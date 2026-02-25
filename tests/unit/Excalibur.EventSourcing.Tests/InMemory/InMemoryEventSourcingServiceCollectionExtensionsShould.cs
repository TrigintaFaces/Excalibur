// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryEventSourcingServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryEventSourcingServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddInMemoryEventStore_RegistersInMemoryEventStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryEventStore();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(InMemoryEventStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddInMemoryEventStore_RegistersIEventStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryEventStore();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IEventStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddInMemoryEventStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryEventStore();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryEventStore_ConcreteAndAbstract_ShareSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddInMemoryEventStore();

		using var provider = services.BuildServiceProvider();

		// Act
		var concrete = provider.GetRequiredService<InMemoryEventStore>();
		var abstraction = provider.GetRequiredService<IEventStore>();

		// Assert
		abstraction.ShouldBeSameAs(concrete);
	}

	[Fact]
	public void AddInMemoryEventStore_CanBeCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw
		_ = services.AddInMemoryEventStore();
		_ = services.AddInMemoryEventStore();

		// Assert - services should be registered
		services.Count(static sd => sd.ServiceType == typeof(InMemoryEventStore)).ShouldBe(2);
	}
}
