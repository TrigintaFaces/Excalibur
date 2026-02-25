// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Implementation;

using ConcurrencyException = Excalibur.Dispatch.Exceptions.ConcurrencyException;
using IEventStore = Excalibur.EventSourcing.Abstractions.IEventStore;
using ISnapshotManager = Excalibur.EventSourcing.Abstractions.ISnapshotManager;
using ISnapshotStrategy = Excalibur.EventSourcing.Abstractions.ISnapshotStrategy;
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.EventSourcing.Tests.Core;

/// <summary>
/// Depth coverage tests for <see cref="EventSourcedRepository{TAggregate,TKey}"/>
/// covering GetByIdAsync, SaveAsync, ExistsAsync, DeleteAsync, QueryAsync, FindAsync,
/// ETag validation, concurrency failures, and snapshot integration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcedRepositoryDepthShould
{
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenEventStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSourcedRepository<TestRepoAggregate, string>(
				null!, _eventSerializer, id => new TestRepoAggregate(id)));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenSerializerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSourcedRepository<TestRepoAggregate, string>(
				_eventStore, null!, id => new TestRepoAggregate(id)));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EventSourcedRepository<TestRepoAggregate, string>(
				_eventStore, _eventSerializer, null!));
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNoEventsExist()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repo = CreateRepository();

		// Act
		var result = await repo.GetByIdAsync("non-existent", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetByIdAsync_ThrowsArgumentNullException_WhenIdIsNull()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<ArgumentNullException>(() => repo.GetByIdAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_RehydratesAggregate_FromEvents()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			new("ev-1", "agg-1", "TestRepoAggregate", "TestCreated", [1], null, 0, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", A<string>._, A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventSerializer.ResolveType("TestCreated"))
			.Returns(typeof(TestRepoCreatedEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, typeof(TestRepoCreatedEvent)))
			.Returns(new TestRepoCreatedEvent("agg-1"));

		var repo = CreateRepository();

		// Act
		var result = await repo.GetByIdAsync("agg-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result!.Id.ShouldBe("agg-1");
		result.Version.ShouldBe(1);
	}

	[Fact]
	public async Task SaveAsync_ThrowsArgumentNullException_WhenAggregateIsNull()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<ArgumentNullException>(() => repo.SaveAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_DoesNothing_WhenNoUncommittedEvents()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		var repo = CreateRepository();

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SaveAsync_AppendsEvents_AndCommits()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.Create("agg-1");

		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repo = CreateRepository();

		// Act
		await repo.SaveAsync(aggregate, CancellationToken.None);

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeFalse();
		aggregate.Version.ShouldBe(1);
		aggregate.ETag.ShouldNotBeNull();
	}

	[Fact]
	public async Task SaveAsync_ThrowsConcurrencyException_WhenConflict()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.Create("agg-1");

		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateConcurrencyConflict(0, 5));

		var repo = CreateRepository();

		// Act & Assert
		await Should.ThrowAsync<ConcurrencyException>(() => repo.SaveAsync(aggregate, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowsResourceException_WhenNonConcurrencyFailure()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.Create("agg-1");

		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateFailure("generic error"));

		var repo = CreateRepository();

		// Act & Assert
		await Should.ThrowAsync<ResourceException>(() => repo.SaveAsync(aggregate, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_WithETag_ThrowsConcurrencyException_OnMismatch()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.ETag = "old-etag";
		aggregate.Create("agg-1");

		var repo = CreateRepository();

		// Act & Assert
		await Should.ThrowAsync<ConcurrencyException>(() =>
			repo.SaveAsync(aggregate, "different-etag", CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_WithETag_Succeeds_WhenETagMatches()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.ETag = "my-etag";
		aggregate.Create("agg-1");

		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repo = CreateRepository();

		// Act — should not throw
		await repo.SaveAsync(aggregate, "my-etag", CancellationToken.None);

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public async Task SaveAsync_WithNullETag_SkipsValidation()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		aggregate.ETag = "some-etag";
		aggregate.Create("agg-1");

		A.CallTo(() => _eventStore.AppendAsync(A<string>._, A<string>._, A<IReadOnlyList<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repo = CreateRepository();

		// Act — null expectedETag skips validation
		await repo.SaveAsync(aggregate, (string?)null, CancellationToken.None);

		// Assert
		aggregate.HasUncommittedEvents.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteAsync_ThrowsNotSupportedException()
	{
		// Arrange
		var aggregate = new TestRepoAggregate("agg-1");
		var repo = CreateRepository();

		// Act & Assert
		await Should.ThrowAsync<NotSupportedException>(() =>
			repo.DeleteAsync(aggregate, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowsArgumentNullException_WhenAggregateIsNull()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			repo.DeleteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExistsAsync_ReturnsTrue_WhenEventsExist()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync("agg-1", A<string>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { new("ev-1", "agg-1", "Test", "Test", [1], null, 0, DateTimeOffset.UtcNow, false) });

		var repo = CreateRepository();

		// Act
		var exists = await repo.ExistsAsync("agg-1", CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
	}

	[Fact]
	public async Task ExistsAsync_ReturnsFalse_WhenNoEventsExist()
	{
		// Arrange
		A.CallTo(() => _eventStore.LoadAsync("agg-1", A<string>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var repo = CreateRepository();

		// Act
		var exists = await repo.ExistsAsync("agg-1", CancellationToken.None);

		// Assert
		exists.ShouldBeFalse();
	}

	[Fact]
	public async Task ExistsAsync_ThrowsArgumentNullException_WhenIdIsNull()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			repo.ExistsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_ThrowsNotSupportedException()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<NotSupportedException>(() =>
			repo.QueryAsync(new TestQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task FindAsync_ThrowsNotSupportedException()
	{
		var repo = CreateRepository();
		await Should.ThrowAsync<NotSupportedException>(() =>
			repo.FindAsync(new TestQuery(), CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_SkipsNullDeserializedEvents()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			new("ev-1", "agg-1", "TestRepoAggregate", "UnknownType", [1], null, 0, DateTimeOffset.UtcNow, false),
			new("ev-2", "agg-1", "TestRepoAggregate", "TestCreated", [2], null, 1, DateTimeOffset.UtcNow, false),
		};

		A.CallTo(() => _eventStore.LoadAsync("agg-1", A<string>._, A<long>._, A<CancellationToken>._))
			.Returns(storedEvents);
		A.CallTo(() => _eventSerializer.ResolveType("UnknownType"))
			.Throws(new System.Text.Json.JsonException("unknown"));
		A.CallTo(() => _eventSerializer.ResolveType("TestCreated"))
			.Returns(typeof(TestRepoCreatedEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>.That.Contains((byte)2), typeof(TestRepoCreatedEvent)))
			.Returns(new TestRepoCreatedEvent("agg-1"));

		var repo = CreateRepository();

		// Act
		var result = await repo.GetByIdAsync("agg-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result!.Id.ShouldBe("agg-1");
	}

	private EventSourcedRepository<TestRepoAggregate, string> CreateRepository(
		ISnapshotManager? snapshotManager = null,
		ISnapshotStrategy? snapshotStrategy = null)
	{
		return new EventSourcedRepository<TestRepoAggregate, string>(
			_eventStore,
			_eventSerializer,
			id => new TestRepoAggregate(id),
			snapshotManager: snapshotManager,
			snapshotStrategy: snapshotStrategy);
	}

	// Test types
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Test helper")]
	public sealed class TestRepoAggregate : AggregateRoot, IAggregateSnapshotSupport
	{
		public TestRepoAggregate() { }
		public TestRepoAggregate(string id) : base(id) { }

		public void Create(string id) => RaiseEvent(new TestRepoCreatedEvent(id));

		protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
		{
			TestRepoCreatedEvent e => Apply(e),
			_ => throw new InvalidOperationException(),
		};

		private bool Apply(TestRepoCreatedEvent e)
		{
			Id = e.AggregateId;
			return true;
		}
	}

	private sealed record TestRepoCreatedEvent(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => "TestCreated";
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed class TestQuery : IAggregateQuery<TestRepoAggregate>;
}
