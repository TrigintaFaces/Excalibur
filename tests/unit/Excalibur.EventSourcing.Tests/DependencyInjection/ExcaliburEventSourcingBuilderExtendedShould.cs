// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Extended unit tests for <see cref="ExcaliburEventSourcingBuilder"/> covering
/// builder methods not covered by the basic test class (UseSnapshotManager, UseEventStore,
/// UseEventSerializer, UseOutboxStore, AddRepository overloads, AddUpcastingPipeline).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExcaliburEventSourcingBuilderExtendedShould
{
	#region Test Aggregates

	internal sealed class OrderAggregate : AggregateRoot
	{
		public OrderAggregate() { }
		public OrderAggregate(string id) : base(id) { }
		protected override bool ApplyEventInternal(IDomainEvent @event) => false; // totality: recognizes no events => unhandled.
	}

	internal sealed class CustomerAggregate : AggregateRoot<Guid>
	{
		public CustomerAggregate() { }
		public CustomerAggregate(Guid id) : base(id) { }
		protected override bool ApplyEventInternal(IDomainEvent @event) => false; // totality: recognizes no events => unhandled.
	}

	internal sealed class ProductAggregate : AggregateRoot<Guid>, IAggregateRoot<ProductAggregate, Guid>
	{
		public ProductAggregate() { }
		public ProductAggregate(Guid id) : base(id) { }
		public static ProductAggregate Create(Guid id) => new(id);
		public static ProductAggregate FromEvents(Guid id, IEnumerable<HistoricEvent> events)
		{
			var agg = new ProductAggregate(id);
			agg.LoadFromHistory(events);
			return agg;
		}
		protected override bool ApplyEventInternal(IDomainEvent @event) => false; // totality: recognizes no events => unhandled.
	}

	#endregion

	#region Fake Implementations

	internal sealed class FakeSnapshotManager : ISnapshotManager
	{
		public Task<ISnapshot> CreateSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken = default)
			where TAggregate : IAggregateRoot, IAggregateSnapshotSupport
			=> throw new NotImplementedException();

		public Task SaveSnapshotAsync(string streamId, ISnapshot snapshot, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<ISnapshot?> GetLatestSnapshotAsync(string streamId, CancellationToken cancellationToken = default)
			=> Task.FromResult<ISnapshot?>(null);

		public Task<TAggregate> RestoreFromSnapshotAsync<TAggregate>(ISnapshot snapshot, CancellationToken cancellationToken = default)
			where TAggregate : IAggregateRoot, IAggregateSnapshotSupport, new()
			=> Task.FromResult(new TAggregate());
	}

	internal sealed class FakeEventSerializer : IEventSerializer
	{
		public byte[] SerializeEvent(IDomainEvent domainEvent) => Array.Empty<byte>();
		public IDomainEvent DeserializeEvent(byte[] data, Type eventType) => A.Fake<IDomainEvent>();
		public byte[] SerializeSnapshot(object snapshot) => Array.Empty<byte>();
		public object DeserializeSnapshot(byte[] data, Type snapshotType) => new object();
		public string GetTypeName(Type type) => type.Name;
		public Type ResolveType(string typeName) => typeof(object);
	}

	#endregion

	#region UseSnapshotManager Tests

	[Fact]
	public void UseSnapshotManager_ShouldRegisterSnapshotManager()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseSnapshotManager<FakeSnapshotManager>();
		var provider = services.BuildServiceProvider();

		// Assert
		result.ShouldBe(builder);
		var manager = provider.GetService<ISnapshotManager>();
		manager.ShouldNotBeNull();
		manager.ShouldBeOfType<FakeSnapshotManager>();
	}

	[Fact]
	public void UseSnapshotManager_ShouldNotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act - Register twice, first wins (TryAddSingleton)
		_ = builder.UseSnapshotManager<FakeSnapshotManager>();
		_ = builder.UseSnapshotManager<FakeSnapshotManager>();
		var provider = services.BuildServiceProvider();

		// Assert
		var manager = provider.GetRequiredService<ISnapshotManager>();
		manager.ShouldBeOfType<FakeSnapshotManager>();
	}

	#endregion

	#region UseEventStore Tests

	[Fact]
	public void UseEventStore_ShouldRegisterEventStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseEventStore<InMemoryEventStore>();

		// Assert - UseEventStore now registers as keyed service with "default" key
		result.ShouldBe(builder);
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IEventStore) &&
			d.IsKeyedService &&
			Equals(d.ServiceKey, "default"));
		descriptor.ShouldNotBeNull();
		descriptor.KeyedImplementationType.ShouldBe(typeof(InMemoryEventStore));
	}

	[Fact]
	public void UseEventStore_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseEventStore<InMemoryEventStore>();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region UseEventSerializer Tests

	[Fact]
	public void UseEventSerializer_ShouldRegisterSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseEventSerializer<FakeEventSerializer>();
		var provider = services.BuildServiceProvider();

		// Assert
		result.ShouldBe(builder);
		var serializer = provider.GetService<IEventSerializer>();
		serializer.ShouldNotBeNull();
		serializer.ShouldBeOfType<FakeEventSerializer>();
	}

	[Fact]
	public void UseEventSerializer_ShouldNotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseEventSerializer<FakeEventSerializer>();
		_ = builder.UseEventSerializer<FakeEventSerializer>();
		var provider = services.BuildServiceProvider();

		// Assert
		var serializer = provider.GetRequiredService<IEventSerializer>();
		serializer.ShouldBeOfType<FakeEventSerializer>();
	}

	#endregion

	#region AddRepository with Factory (String Key) Tests

	[Fact]
	public void AddRepository_StringKey_ShouldRegisterRepositoryWithFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		// The store resolves its tenant partition from an ITenantContext, so a registration that names the
		// implementation type directly must supply one. AddInMemoryEventStore does this for its callers.
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.AddRepository(id => new OrderAggregate(id));

		// Assert
		result.ShouldBe(builder);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetService<IEventSourcedRepository<OrderAggregate>>();
		repo.ShouldNotBeNull();
	}

	[Fact]
	public void AddRepository_StringKey_ShouldThrowOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddRepository<OrderAggregate>(null!));
	}

	#endregion

	#region AddRepository with Factory (Generic Key) Tests

	[Fact]
	public void AddRepository_GenericKey_ShouldRegisterRepositoryWithFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		// The store resolves its tenant partition from an ITenantContext, so a registration that names the
		// implementation type directly must supply one. AddInMemoryEventStore does this for its callers.
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.AddRepository<CustomerAggregate, Guid>(id => new CustomerAggregate(id));

		// Assert
		result.ShouldBe(builder);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetService<IEventSourcedRepository<CustomerAggregate, Guid>>();
		repo.ShouldNotBeNull();
	}

	[Fact]
	public void AddRepository_GenericKey_ShouldThrowOnNullFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddRepository((Func<Guid, CustomerAggregate>)null!));
	}

	#endregion

	#region AddRepository with Static Factory Tests

	[Fact]
	public void AddRepository_StaticFactory_ShouldRegisterRepositoryUsingCreate()
	{
		// Arrange
		var services = new ServiceCollection();
		// The store resolves its tenant partition from an ITenantContext, so a registration that names the
		// implementation type directly must supply one. AddInMemoryEventStore does this for its callers.
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.AddRepository<ProductAggregate, Guid>();

		// Assert
		result.ShouldBe(builder);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetService<IEventSourcedRepository<ProductAggregate, Guid>>();
		repo.ShouldNotBeNull();
	}

	#endregion

	#region AddRepository wires EventSourcedRepositoryOptions into the constructed repository (bd-py7p5h)

	// Reads a private instance field off the resolved repository -- the value the CONSTRUCTOR actually
	// received, not the options DTO. This is what let the pre-fix registration pass round-trip tests on
	// EventSourcedRepositoryOptions while the constructed repository never saw three of its four settings.
	private static T GetPrivateField<T>(object instance, string fieldName)
	{
		for (var type = instance.GetType(); type is not null; type = type.BaseType)
		{
			var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (field is not null)
			{
				return (T)field.GetValue(instance)!;
			}
		}

		throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType()} or its base types.");
	}

	[Fact]
	public void AddRepository_StringKey_ShouldWireEnableAutoUpcast_FromPerAggregateOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act -- no UpcastingOptions registered, so only the dedicated EventSourcedRepositoryOptions
		// setting can be the source of this value.
		_ = builder.AddRepository(id => new OrderAggregate(id), o => o.EnableAutoUpcast = true);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetRequiredService<IEventSourcedRepository<OrderAggregate>>();

		// Assert
		GetPrivateField<bool>(repo, "_enableAutoUpcast").ShouldBeTrue();
	}

	[Fact]
	public void AddRepository_StringKey_ShouldWireEnableAutoSnapshotUpgrade_FromPerAggregateOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act -- SnapshotUpgradingOptions.EnableAutoUpgradeOnLoad defaults to TRUE, so asserting "true"
		// here would pass even pre-fix by coincidence (the framework-wide default, not the dedicated
		// option, supplying the value). Asserting the EXPLICIT OVERRIDE to false is what discriminates:
		// pre-fix, the per-aggregate override never reaches the constructor and the framework default
		// (true) wins; post-fix, the override wins outright as documented.
		_ = builder.AddRepository(id => new OrderAggregate(id), o => o.EnableAutoSnapshotUpgrade = false);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetRequiredService<IEventSourcedRepository<OrderAggregate>>();

		// Assert
		GetPrivateField<bool>(repo, "_enableAutoSnapshotUpgrade").ShouldBeFalse();
	}

	[Fact]
	public void AddRepository_StringKey_ShouldWireTargetSnapshotVersion_FromPerAggregateOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act -- no SnapshotUpgradingOptions registered, and the value deliberately differs from the
		// shared default of 1 so the assertion cannot pass by coincidence.
		_ = builder.AddRepository(id => new OrderAggregate(id), o => o.TargetSnapshotVersion = 7);
		var provider = services.BuildServiceProvider();
		var repo = provider.GetRequiredService<IEventSourcedRepository<OrderAggregate>>();

		// Assert
		GetPrivateField<int>(repo, "_targetSnapshotVersion").ShouldBe(7);
	}

	[Fact]
	public void AddRepository_GenericKey_ShouldWireAllThreeSettings_FromPerAggregateOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.AddRepository<CustomerAggregate, Guid>(id => new CustomerAggregate(id), o =>
		{
			o.EnableAutoUpcast = true;
			o.EnableAutoSnapshotUpgrade = true;
			o.TargetSnapshotVersion = 9;
		});
		var provider = services.BuildServiceProvider();
		var repo = provider.GetRequiredService<IEventSourcedRepository<CustomerAggregate, Guid>>();

		// Assert -- the TKey ctor overload is a distinct DI factory from the string-key one; this proves
		// the fix reaches both, not just the overload exercised above.
		GetPrivateField<bool>(repo, "_enableAutoUpcast").ShouldBeTrue();
		GetPrivateField<bool>(repo, "_enableAutoSnapshotUpgrade").ShouldBeTrue();
		GetPrivateField<int>(repo, "_targetSnapshotVersion").ShouldBe(9);
	}

	[Fact]
	public void AddRepository_ShouldNotLetDedicatedOption_SilentlyDisable_WhatUpcastingOptionsEnabled()
	{
		// Arrange -- the framework-wide UpcastingOptions enables replay upcasting; the dedicated
		// EventSourcedRepositoryOptions is left at its default (false). Folding the dedicated option in
		// must combine rather than overwrite, or registering it would silently turn upcasting back off.
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		services.AddKeyedSingleton<IEventStore, InMemoryEventStore>("default");
		services.AddSingleton<IEventSerializer, FakeEventSerializer>();
		_ = services.Configure<UpcastingOptions>(o => o.EnableAutoUpcastOnReplay = true);
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.AddRepository(id => new OrderAggregate(id));
		var provider = services.BuildServiceProvider();
		var repo = provider.GetRequiredService<IEventSourcedRepository<OrderAggregate>>();

		// Assert
		GetPrivateField<bool>(repo, "_enableAutoUpcast").ShouldBeTrue();
	}

	#endregion

	#region AddUpcastingPipeline Tests

	[Fact]
	public void AddUpcastingPipeline_ShouldConfigureUpcastingServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.AddUpcastingPipeline(upcasting => { });

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddUpcastingPipeline_ShouldThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddUpcastingPipeline(null!));
	}

	#endregion

	#region Full Method Chaining Tests

	[Fact]
	public void Builder_ShouldSupportFullMethodChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act - chain multiple builder methods
		var result = builder
			.UseEventStore<InMemoryEventStore>()
			.UseEventSerializer<FakeEventSerializer>()
			.UseIntervalSnapshots(50)
			.UseSnapshotManager<FakeSnapshotManager>();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion
}
