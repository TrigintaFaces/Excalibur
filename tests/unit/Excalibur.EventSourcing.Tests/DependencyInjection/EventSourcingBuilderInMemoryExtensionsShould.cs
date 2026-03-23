// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderInMemoryExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderInMemoryExtensionsShould
{
	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseInMemory());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseInMemory();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterInMemoryEventStore_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseInMemory();

		// Assert - IEventStore is now registered as keyed service
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore) && sd.IsKeyedService);
	}

	[Fact]
	public void RegisterInMemoryEventStoreImplementation_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseInMemory();
		var provider = services.BuildServiceProvider();

		// Assert - IEventStore is now keyed, resolve via "default" key
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryEventStore>();
	}

	[Fact]
	public void SupportFluentChaining_WithOtherBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder
			.UseInMemory()
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}
}
