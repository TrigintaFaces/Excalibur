// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.InMemory;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryOutboxExtensions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "DataInMemory")]
[Trait("Priority", "0")]
public sealed class InMemoryOutboxExtensionsShould : UnitTestBase
{
	private static ServiceCollection CreateServicesWithLogging()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		return services;
	}

	#region AddInMemoryOutboxStore Tests

	[Fact]
	public void AddInMemoryOutboxStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryOutboxStore());
	}

	[Fact]
	public void AddInMemoryOutboxStore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInMemoryOutboxStore();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryOutboxStore_RegistersInMemoryOutboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryOutboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemoryOutboxStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryOutboxStore_RegistersIOutboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryOutboxStore();

		// Assert - IOutboxStore is now registered as a keyed service
		var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IOutboxStore) && sd.IsKeyedService);
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddInMemoryOutboxStore_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryOutboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<InMemoryOutboxOptions>>();
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryOutboxStore_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		const int expectedMaxMessages = 5000;

		// Act
		_ = services.AddInMemoryOutboxStore(opt =>
		{
			opt.MaxMessages = expectedMaxMessages;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(expectedMaxMessages);
	}

	[Fact]
	public void AddInMemoryOutboxStore_AppliesRetentionPeriodConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedRetention = TimeSpan.FromDays(14);

		// Act
		_ = services.AddInMemoryOutboxStore(opt =>
		{
			opt.DefaultRetentionPeriod = expectedRetention;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.DefaultRetentionPeriod.ShouldBe(expectedRetention);
	}

	[Fact]
	public void AddInMemoryOutboxStore_IsSingleton()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryOutboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store1 = provider.GetRequiredService<InMemoryOutboxStore>();
		var store2 = provider.GetRequiredService<InMemoryOutboxStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void AddInMemoryOutboxStore_IsIdempotent()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryOutboxStore();
		_ = services.AddInMemoryOutboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var stores = provider.GetServices<InMemoryOutboxStore>().ToList();
		stores.Count.ShouldBe(1);
	}

	#endregion

	#region UseInMemoryOutboxStore Tests

	[Fact]
	public void UseInMemoryOutboxStore_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseInMemoryOutboxStore());
	}

	[Fact]
	public void UseInMemoryOutboxStore_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseInMemoryOutboxStore();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseInMemoryOutboxStore_RegistersServices()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		_ = builder.UseInMemoryOutboxStore();
		var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetService<InMemoryOutboxStore>();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void UseInMemoryOutboxStore_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		const int expectedMaxMessages = 2500;

		// Act
		_ = builder.UseInMemoryOutboxStore(opt =>
		{
			opt.MaxMessages = expectedMaxMessages;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>();
		options.Value.MaxMessages.ShouldBe(expectedMaxMessages);
	}

	#endregion

	#region Non-keyed contract resolution

	/// <summary>
	/// A host that calls only AddInMemoryOutboxStore() must be able to inject the plain
	/// <see cref="IOutboxStore"/>. Without the non-keyed alias the consumer meets
	/// "Unable to resolve service for type ..." at startup, after wiring, which no documentation can
	/// prevent.
	/// </summary>
	[Fact]
	public void AddInMemoryOutboxStore_RegistersNonKeyedIOutboxStore()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act -- ONLY the in-memory extension, never the AddExcaliburOutbox composition, which
		// registers the alias itself and would make this arm pass either way.
		_ = services.AddInMemoryOutboxStore();
		using var provider = services.BuildServiceProvider();

		// Assert
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldNotBeNull();
	}

	/// <summary>
	/// The non-keyed alias must resolve the same singleton the keyed registrations serve, not a second
	/// instance -- two stores would mean two independent in-memory states behind one contract.
	/// </summary>
	[Fact]
	public void AddInMemoryOutboxStore_ResolvesNonKeyedToTheSameInstanceAsKeyed()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		_ = services.AddInMemoryOutboxStore();
		using var provider = services.BuildServiceProvider();

		// Act
		var nonKeyed = provider.GetRequiredService<IOutboxStore>();
		var keyedInMemory = provider.GetRequiredKeyedService<IOutboxStore>("inmemory");
		var keyedDefault = provider.GetRequiredKeyedService<IOutboxStore>("default");

		// Assert
		nonKeyed.ShouldBeSameAs(keyedInMemory);
		nonKeyed.ShouldBeSameAs(keyedDefault);
	}

	/// <summary>
	/// The alias uses TryAdd semantics, so calling the extension twice leaves exactly one non-keyed
	/// descriptor rather than shadowing the first.
	/// </summary>
	[Fact]
	public void AddInMemoryOutboxStore_RegistersTheNonKeyedAliasOnce_WhenCalledTwice()
	{
		// Arrange
		var services = CreateServicesWithLogging();

		// Act
		_ = services.AddInMemoryOutboxStore();
		_ = services.AddInMemoryOutboxStore();

		// Assert
		services.Count(d => d.ServiceType == typeof(IOutboxStore) && !d.IsKeyedService).ShouldBe(1);
	}

	/// <summary>
	/// TryAdd must not clobber a consumer's own non-keyed registration made before the extension runs.
	/// </summary>
	[Fact]
	public void AddInMemoryOutboxStore_KeepsAConsumerOwnNonKeyedRegistration()
	{
		// Arrange
		var services = CreateServicesWithLogging();
		var consumerStore = A.Fake<IOutboxStore>();
		services.AddSingleton(consumerStore);

		// Act
		_ = services.AddInMemoryOutboxStore();
		using var provider = services.BuildServiceProvider();

		// Assert
		provider.GetRequiredService<IOutboxStore>().ShouldBeSameAs(consumerStore);
	}

	#endregion
}
