// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryEventStoreFailurePathShould
{
	[Fact]
	public async Task LoadAsync_Rethrows_WhenCorruptedAggregateEventListContainsNull()
	{
		var store = new InMemoryEventStore();
		var eventsByAggregate = GetPrivateField<ConcurrentDictionary<(string, string), List<StoredEvent>>>(
			store,
			"_events");
		eventsByAggregate[("agg-1", "Order")] = [null!];

		await Should.ThrowAsync<NullReferenceException>(() =>
			store.LoadAsync("agg-1", "Order", fromVersion: 0, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AppendAsync_Rethrows_WhenCorruptedAggregateEventListIsNull()
	{
		var store = new InMemoryEventStore();
		var eventsByAggregate = GetPrivateField<ConcurrentDictionary<(string, string), List<StoredEvent>>>(
			store,
			"_events");
		eventsByAggregate[("agg-1", "Order")] = null!;

		var evt = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 0,
			EventType = "Test",
			OccurredAt = DateTimeOffset.UtcNow,
			Data = "data"
		};

		await Should.ThrowAsync<ArgumentNullException>(() =>
			store.AppendAsync("agg-1", "Order", [evt], expectedVersion: -1, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_Rethrows_WhenCorruptedEventIndexContainsNull()
	{
		var store = new InMemoryEventStore();
		var eventsById = GetPrivateField<ConcurrentDictionary<string, StoredEvent>>(store, "_eventsById");
		eventsById["evt-null"] = null!;

		await Should.ThrowAsync<NullReferenceException>(() =>
			store.GetUndispatchedEventsAsync(10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Rethrows_WhenCorruptedEventIndexContainsNull()
	{
		var store = new InMemoryEventStore();
		var eventsById = GetPrivateField<ConcurrentDictionary<string, StoredEvent>>(store, "_eventsById");
		eventsById["evt-null"] = null!;

		await Should.ThrowAsync<NullReferenceException>(() =>
			store.MarkEventAsDispatchedAsync("evt-null", CancellationToken.None).AsTask());
	}

	private static T GetPrivateField<T>(object instance, string fieldName)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
		return (T)field.GetValue(instance)!;
	}

	private sealed class TestDomainEvent : IDomainEvent
	{
		public required string EventId { get; init; }
		public required string AggregateId { get; init; }
		public required long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public required string EventType { get; init; }
		public required string Data { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
