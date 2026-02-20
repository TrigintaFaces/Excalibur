// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreBehaviorShould : UnitTestBase
{
	[Fact]
	public async Task LoadAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.LoadAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), null, CancellationToken.None));
	}

	[Fact]
	public async Task LoadFromVersionAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.LoadFromVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), 5, null, CancellationToken.None));
	}

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = CreateInitializedStore();

		var result = await sut.AppendAsync(
			aggregateId: "agg-1",
			aggregateType: "Order",
			partitionKey: new PartitionKey("Order:agg-1"),
			events: [],
			expectedVersion: 7,
			cancellationToken: CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(7);
		result.RequestCharge.ShouldBe(0);
	}

	[Fact]
	public async Task AppendAsync_Throw_WhenTransactionalBatchPathCannotAccessContainer()
	{
		var sut = CreateInitializedStore(useTransactionalBatch: true);
		var events = new IDomainEvent[]
		{
			new TestDomainEvent("evt-1"),
			new TestDomainEvent("evt-2")
		};

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.AppendAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), events, expectedVersion: 0, CancellationToken.None));
	}

	[Fact]
	public async Task AppendAsync_Throw_WhenSequentialPathCannotAccessContainer()
	{
		var sut = CreateInitializedStore(useTransactionalBatch: false);
		var events = new IDomainEvent[] { new TestDomainEvent("evt-1") };

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.AppendAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), events, expectedVersion: 0, CancellationToken.None));
	}

	[Fact]
	public async Task IEventStoreAppendAsync_MapSuccess_WhenNoEvents()
	{
		var sut = CreateInitializedStore();
		var eventStore = (IEventStore)sut;

		var result = await eventStore.AppendAsync(
			aggregateId: "agg-2",
			aggregateType: "Order",
			events: [],
			expectedVersion: 11,
			cancellationToken: CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(11);
		result.FirstEventPosition.ShouldBe(0);
	}

	[Fact]
	public async Task GetCurrentVersionAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.GetCurrentVersionAsync("agg-1", "Order", new PartitionKey("Order:agg-1"), CancellationToken.None));
	}

	[Fact]
	public async Task GetUndispatchedEventsAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.GetUndispatchedEventsAsync(10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkEventAsDispatchedAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<NullReferenceException>(() =>
			sut.MarkEventAsDispatchedAsync("evt-1", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task SubscribeToChangesAsync_Throw_WhenContainerIsMissing()
	{
		var sut = CreateInitializedStore();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.SubscribeToChangesAsync(options: null, CancellationToken.None));
	}

	private static CosmosDbEventStore CreateInitializedStore(bool useTransactionalBatch = true)
	{
		var sut = (CosmosDbEventStore)RuntimeHelpers.GetUninitializedObject(typeof(CosmosDbEventStore));
		SetPrivateField(
			sut,
			"_options",
			Options.Create(new CosmosDbEventStoreOptions
			{
				UseTransactionalBatch = useTransactionalBatch
			}));
		SetPrivateField(sut, "_logger", NullLogger<CosmosDbEventStore>.Instance);
		SetPrivateField(sut, "_initialized", true);
		return sut;
	}

	private static void SetPrivateField(object instance, string fieldName, object value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}

	private sealed record TestDomainEvent(string EventId) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
		public IDictionary<string, object>? Metadata => null;
	}
}
