// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.CosmosDb;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreHelpersShould : UnitTestBase
{
	[Fact]
	public void BuildStreamId_CombineAggregateTypeAndId()
	{
		var method = typeof(CosmosDbEventStore).GetMethod("BuildStreamId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var streamId = method!.Invoke(null, ["Order", "agg-42"]);
		streamId.ShouldBe("Order:agg-42");
	}

	[Fact]
	public void ExtractCorrelationId_ResolveBothKeyCasings()
	{
		var method = typeof(CosmosDbEventStore).GetMethod("ExtractCorrelationId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var eventsUpper = new IDomainEvent[]
		{
			new TestDomainEvent("evt-1", new Dictionary<string, object> { ["CorrelationId"] = "corr-1" })
		};
		method!.Invoke(null, [eventsUpper]).ShouldBe("corr-1");

		var eventsLower = new IDomainEvent[]
		{
			new TestDomainEvent("evt-2", new Dictionary<string, object> { ["correlationId"] = "corr-2" })
		};
		method.Invoke(null, [eventsLower]).ShouldBe("corr-2");

		var noCorrelation = new IDomainEvent[] { new TestDomainEvent("evt-3", Metadata: null) };
		method.Invoke(null, [noCorrelation]).ShouldBeNull();
	}

	[Fact]
	public void ExtractEventId_ReturnFirstNonEmptyId()
	{
		var method = typeof(CosmosDbEventStore).GetMethod("ExtractEventId", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var events = new IDomainEvent[]
		{
			new TestDomainEvent(""),
			new TestDomainEvent("evt-9"),
			new TestDomainEvent("evt-10")
		};

		method!.Invoke(null, [events]).ShouldBe("evt-9");
	}

	[Fact]
	public void ConvertBetweenEventDocumentAndStoredEventShapes()
	{
		var createMethod = typeof(CosmosDbEventStore).GetMethod("CreateEventDocument", BindingFlags.NonPublic | BindingFlags.Static);
		var toCloudMethod = typeof(CosmosDbEventStore).GetMethod("ToCloudStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		var toStoredMethod = typeof(CosmosDbEventStore).GetMethod("ToStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		createMethod.ShouldNotBeNull();
		toCloudMethod.ShouldNotBeNull();
		toStoredMethod.ShouldNotBeNull();

		var domainEvent = new TestDomainEvent("evt-1", new Dictionary<string, object> { ["key"] = "value" });
		var document = createMethod!.Invoke(null, ["Order:agg-1", "agg-1", "Order", domainEvent, 8L]);
		document.ShouldNotBeNull();

		var cloudEvent = toCloudMethod!.Invoke(null, [document!]);
		cloudEvent.ShouldNotBeNull();
		var storedEvent = (StoredEvent)toStoredMethod!.Invoke(null, [cloudEvent!])!;

		storedEvent.EventId.ShouldBe("evt-1");
		storedEvent.AggregateId.ShouldBe("agg-1");
		storedEvent.AggregateType.ShouldBe("Order");
		storedEvent.Version.ShouldBe(8);
		storedEvent.IsDispatched.ShouldBeFalse();
		storedEvent.Metadata.ShouldNotBeNull();
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var sut = (CosmosDbEventStore)RuntimeHelpers.GetUninitializedObject(typeof(CosmosDbEventStore));

		await sut.DisposeAsync();
		await sut.DisposeAsync();

		var disposedField = typeof(CosmosDbEventStore).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
		disposedField.ShouldNotBeNull();
		((bool)disposedField!.GetValue(sut)!).ShouldBeTrue();
	}

	private sealed record TestDomainEvent(string EventId, IDictionary<string, object>? Metadata = null) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
		public string EventType => "TestDomainEvent";
	}
}
