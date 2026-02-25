// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using OutboxMessage = Excalibur.EventSourcing.Outbox.OutboxMessage;

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
		protected override void ApplyEventInternal(IDomainEvent @event) { }
	}

	internal sealed class CustomerAggregate : AggregateRoot<Guid>
	{
		public CustomerAggregate() { }
		public CustomerAggregate(Guid id) : base(id) { }
		protected override void ApplyEventInternal(IDomainEvent @event) { }
	}

	internal sealed class ProductAggregate : AggregateRoot<Guid>, IAggregateRoot<ProductAggregate, Guid>
	{
		public ProductAggregate() { }
		public ProductAggregate(Guid id) : base(id) { }
		public static ProductAggregate Create(Guid id) => new(id);
		public static ProductAggregate FromEvents(Guid id, IEnumerable<IDomainEvent> events)
		{
			var agg = new ProductAggregate(id);
			agg.LoadFromHistory(events);
			return agg;
		}
		protected override void ApplyEventInternal(IDomainEvent @event) { }
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

	internal sealed class FakeOutboxStore : IEventSourcedOutboxStore
	{
		public Task AddAsync(OutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 100, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());

		public Task MarkAsPublishedAsync(Guid messageId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<int> DeletePublishedOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
			=> Task.FromResult(0);

		public Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;
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
		var provider = services.BuildServiceProvider();

		// Assert
		result.ShouldBe(builder);
		var store = provider.GetService<IEventStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemoryEventStore>();
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

	#region UseOutboxStore Tests

	[Fact]
	public void UseOutboxStore_ShouldRegisterOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseOutboxStore<FakeOutboxStore>();
		var provider = services.BuildServiceProvider();

		// Assert
		result.ShouldBe(builder);
		var outbox = provider.GetService<IEventSourcedOutboxStore>();
		outbox.ShouldNotBeNull();
		outbox.ShouldBeOfType<FakeOutboxStore>();
	}

	[Fact]
	public void UseOutboxStore_ShouldReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		var result = builder.UseOutboxStore<FakeOutboxStore>();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region AddRepository with Factory (String Key) Tests

	[Fact]
	public void AddRepository_StringKey_ShouldRegisterRepositoryWithFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IEventStore, InMemoryEventStore>();
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
		services.AddSingleton<IEventStore, InMemoryEventStore>();
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
		services.AddSingleton<IEventStore, InMemoryEventStore>();
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
			.UseSnapshotManager<FakeSnapshotManager>()
			.UseOutboxStore<FakeOutboxStore>();

		// Assert
		result.ShouldBe(builder);
	}

	#endregion
}
