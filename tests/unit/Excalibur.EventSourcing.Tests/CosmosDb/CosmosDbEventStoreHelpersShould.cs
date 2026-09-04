// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.CosmosDb;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class CosmosDbEventStoreHelpersShould : UnitTestBase
{
	/// <summary>
	/// The stream identifier — which is the Cosmos partition key — carries the owning tenant, and two
	/// tenants holding the same aggregate identifier compose to different partition keys.
	/// </summary>
	/// <remarks>
	/// Asserting the exact composed value rather than merely that the two differ: a store that appended a
	/// constant, or hashed the tenant into an unaddressable form, would satisfy "they differ" while making
	/// the partition key unreadable.
	/// </remarks>
	[Fact]
	public void BuildStreamId_ComposeTheTenantIntoThePartitionKey()
	{
		var method = typeof(CosmosDbEventStore).GetMethod("BuildStreamId", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();

		method!.Invoke(StoreFor(UntenantedContext.Instance), ["Order", "agg-42"])
			.ShouldBe($"t:{TenantScope.UntenantedSentinel}:Order:agg-42");
		method.Invoke(StoreFor(new FixedTenantContext("tenant-a")), ["Order", "agg-42"])
			.ShouldBe("t:tenant-a:Order:agg-42");
		method.Invoke(StoreFor(new FixedTenantContext("tenant-b")), ["Order", "agg-42"])
			.ShouldBe("t:tenant-b:Order:agg-42");
	}

	private static CosmosDbEventStore StoreFor(ITenantContext tenantContext)
	{
		var sut = (CosmosDbEventStore)RuntimeHelpers.GetUninitializedObject(typeof(CosmosDbEventStore));
		var field = typeof(CosmosDbEventStore).GetField("_tenantContext", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(sut, tenantContext);
		return sut;
	}

	/// <summary>
	/// Builds a store whose payload writer is present but carries no resolver -- the default reflection
	/// path -- so document creation can be exercised without a Cosmos client.
	/// </summary>
	/// <remarks>
	/// Resolving the writer's type from the field rather than naming it keeps this test off the internal
	/// type, and asserts that the store still routes document creation through a payload writer at all.
	/// </remarks>
	/// <returns>An uninitialized store with its payload writer populated.</returns>
	private static CosmosDbEventStore StoreWithPayloadWriter()
	{
		var sut = (CosmosDbEventStore)RuntimeHelpers.GetUninitializedObject(typeof(CosmosDbEventStore));
		var writerField = typeof(CosmosDbEventStore).GetField("_payloadWriter", BindingFlags.Instance | BindingFlags.NonPublic);
		writerField.ShouldNotBeNull();
		writerField!.SetValue(sut, Activator.CreateInstance(writerField.FieldType, new object?[] { null }));
		return sut;
	}

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId => tenantId;

		public bool HasTenant => true;
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
		// CreateEventDocument is an INSTANCE method: the document's payload and metadata are written through
		// the store's own payload writer, which carries the host's optional source-generated type-info
		// resolver. That resolver is per-store configuration, so document creation cannot be static.
		var createMethod = typeof(CosmosDbEventStore).GetMethod("CreateEventDocument", BindingFlags.NonPublic | BindingFlags.Instance);
		var toCloudMethod = typeof(CosmosDbEventStore).GetMethod("ToCloudStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		var toStoredMethod = typeof(CosmosDbEventStore).GetMethod("ToStoredEvent", BindingFlags.NonPublic | BindingFlags.Static);
		createMethod.ShouldNotBeNull();
		toCloudMethod.ShouldNotBeNull();
		toStoredMethod.ShouldNotBeNull();

		var domainEvent = new TestDomainEvent("evt-1", new Dictionary<string, object> { ["key"] = "value" });
		var document = createMethod!.Invoke(
			StoreWithPayloadWriter(),
			["Order:agg-1", "agg-1", "Order", new[] { (IDomainEvent)domainEvent }.AsNamedEvents()[0], 8L]);
		document.ShouldNotBeNull();

		var cloudEvent = toCloudMethod!.Invoke(null, [document!]);
		cloudEvent.ShouldNotBeNull();
		var storedEvent = (StoredEvent)toStoredMethod!.Invoke(null, [cloudEvent!])!;

		storedEvent.EventId.ShouldBe("evt-1");
		storedEvent.AggregateId.ShouldBe("agg-1");
		storedEvent.AggregateType.ShouldBe("Order");
		storedEvent.Version.ShouldBe(8);
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

	[MessageName("Test.Es.CosmosHelpersTestDomainEvent")]
	private sealed record TestDomainEvent(string EventId, IDictionary<string, object>? Metadata = null) : IDomainEvent
	{
		public string AggregateId => "agg-1";
		public long Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
	}
}
