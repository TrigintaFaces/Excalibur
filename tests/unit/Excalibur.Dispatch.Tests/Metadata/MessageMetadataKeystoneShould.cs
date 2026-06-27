// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json;

using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Metadata;

/// <summary>
/// Keystone regression lock for the <see cref="MessageMetadata"/> DTO-split (bead lh6wcw, sprint 854).
/// </summary>
/// <remarks>
/// <para>
/// This is the author≠impl lock for the MessageMetadata DTO-split keystone: the impl was APPLIED by
/// the implementer; this lock is authored independently by TestsDeveloper against the live tree.
/// </para>
/// <para>
/// Non-vacuity (each fact RED on a revert):
/// <list type="bullet">
/// <item>Fact 1 (structural) RED on reverting the parent to the flat 53-property surface — the parent
/// would no longer expose the seven grouped facades and the public-property count would balloon back
/// above fifty.</item>
/// <item>Fact 2 (flat-wire) RED on dropping <see cref="MessageMetadataJsonConverter"/> — default record
/// serialization would emit nested PascalCase group objects (<c>Identity</c>, <c>Routing</c>, …) and
/// PascalCase roots (<c>MessageId</c>) instead of the preserved flat camelCase wire.</item>
/// <item>Fact 3 (contract) RED on a flat-53 revert — the removed flat properties would re-appear on the
/// parent and the focused group types would no longer be the public contract.</item>
/// </list>
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessageMetadataKeystoneShould
{
	private static readonly DateTimeOffset Created = new(2026, 6, 26, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Sent = new(2026, 6, 26, 10, 0, 1, TimeSpan.Zero);
	private static readonly DateTimeOffset Received = new(2026, 6, 26, 10, 0, 2, TimeSpan.Zero);
	private static readonly DateTimeOffset Scheduled = new(2026, 6, 26, 10, 0, 5, TimeSpan.Zero);

	/// <summary>
	/// Builds a fully populated metadata instance touching every grouped facade via the production builder.
	/// </summary>
	private static MessageMetadata BuildPopulated() =>
		(MessageMetadata)new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithCorrelationId("corr-1")
			.WithCausationId("cause-1")
			.WithMessageType("OrderPlaced")
			.WithContentType("application/json")
			.WithSource("svc-a")
			.WithCreatedTimestampUtc(Created)
			// Identity group
			.WithExternalId("ext-1")
			.WithContentEncoding("gzip")
			.WithMessageVersion("2.0")
			.WithSerializerVersion("3.0")
			.WithContractVersion("1.1.0")
			// Routing group
			.WithDestination("queue-a")
			.WithReplyTo("reply-q")
			.WithSessionId("sess-1")
			.WithPartitionKey("pk-1")
			.WithRoutingKey("rk-1")
			.WithGroupId("grp-1")
			.WithGroupSequence(5)
			// Timing group
			.WithSentTimestampUtc(Sent)
			.WithReceivedTimestampUtc(Received)
			.WithScheduledEnqueueTimeUtc(Scheduled)
			.WithTimeToLive(TimeSpan.FromMinutes(30))
			// Observability group
			.WithTraceParent("00-abc")
			.WithTraceState("ts=1")
			.WithBaggage("b=2")
			// Delivery group
			.WithDeliveryCount(2)
			.WithMaxDeliveryCount(10)
			.WithLastDeliveryError("boom")
			.WithDeadLetterQueue("dlq")
			.WithDeadLetterReason("expired")
			.WithDeadLetterErrorDescription("ttl elapsed")
			.WithPriority(7)
			.WithDurable(true)
			.WithRequiresDuplicateDetection(true)
			.WithDuplicateDetectionWindow(TimeSpan.FromSeconds(45))
			// Event sourcing group
			.WithEventSourcing("agg-1", "Order", 4, "stream-1", 9)
			.WithGlobalPosition(100)
			.WithEventType("OrderPlacedEvent")
			.WithEventVersion(2)
			// Security group
			.WithUserId("user-1")
			.WithTenantId("tenant-1")
			.WithRoles(["admin", "ops"])
			.Build();

	// ===== Fact 1: structural — the parent exposes the seven grouped facades, NOT the flat 53-prop surface =====

	[Fact]
	public void ExposeTheSevenGroupedFacadesWithReducedParentSurface()
	{
		var md = BuildPopulated();

		// The seven grouped facades are reachable and typed as the focused value types.
		md.Identity.ShouldBeOfType<MessageIdentity>();
		md.Routing.ShouldBeOfType<MessageRouting>();
		md.Timing.ShouldBeOfType<MessageTiming>();
		md.Observability.ShouldBeOfType<MessageObservability>();
		md.Delivery.ShouldBeOfType<MessageDelivery>();
		md.EventSourcing.ShouldBeOfType<MessageEventSourcing>();
		md.Security.ShouldBeOfType<MessageSecurity>();

		// A representative property round-trips through each group.
		md.Identity.ExternalId.ShouldBe("ext-1");
		md.Routing.Destination.ShouldBe("queue-a");
		md.Timing.SentTimestampUtc.ShouldBe(Sent);
		md.Observability.TraceParent.ShouldBe("00-abc");
		md.Delivery.DeliveryCount.ShouldBe(2);
		md.EventSourcing.AggregateId.ShouldBe("agg-1");
		md.Security.TenantId.ShouldBe("tenant-1");

		// The parent now carries the reduced, grouped shape (~18 public properties), not the flat
		// 53-property surface. A revert to the flat surface balloons this back above fifty.
		var publicProps = typeof(MessageMetadata)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Length;
		publicProps.ShouldBeGreaterThanOrEqualTo(16);
		publicProps.ShouldBeLessThanOrEqualTo(20);
	}

	// ===== Fact 2: round-trip / flat-wire preserved via the production converter =====

	[Fact]
	public void PreserveTheFlatCamelCaseWireThroughTheProductionConverter()
	{
		var md = BuildPopulated();

		// Serialize through the production path — MessageMetadata is annotated with
		// [JsonConverter(typeof(MessageMetadataJsonConverter))], so this exercises the real converter.
		var json = JsonSerializer.Serialize(md);

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		// The wire is FLAT camelCase: group fields are hoisted to the top level.
		root.TryGetProperty("messageId", out _).ShouldBeTrue();
		root.TryGetProperty("correlationId", out _).ShouldBeTrue();
		root.TryGetProperty("externalId", out _).ShouldBeTrue();      // Identity group, flattened
		root.TryGetProperty("destination", out _).ShouldBeTrue();     // Routing group, flattened
		root.TryGetProperty("traceParent", out _).ShouldBeTrue();     // Observability group, flattened
		root.TryGetProperty("deliveryCount", out _).ShouldBeTrue();   // Delivery group, flattened
		root.TryGetProperty("aggregateId", out _).ShouldBeTrue();     // EventSourcing group, flattened
		root.TryGetProperty("tenantId", out _).ShouldBeTrue();        // Security group, flattened

		// The wire is NOT nested by group — dropping the converter would emit nested PascalCase group
		// objects (Identity/Routing/...) from default record serialization.
		foreach (var group in new[] { "Identity", "identity", "Routing", "routing", "Security", "security", "EventSourcing", "eventSourcing" })
		{
			root.TryGetProperty(group, out _).ShouldBeFalse($"flat wire must not nest the '{group}' group object");
		}

		// Deserialize back through the converter — all group values survive the round-trip.
		var restored = JsonSerializer.Deserialize<MessageMetadata>(json);
		restored.ShouldNotBeNull();

		restored.MessageId.ShouldBe("msg-1");
		restored.CorrelationId.ShouldBe("corr-1");
		restored.CausationId.ShouldBe("cause-1");
		restored.MessageType.ShouldBe("OrderPlaced");
		restored.Source.ShouldBe("svc-a");

		restored.Identity.ExternalId.ShouldBe("ext-1");
		restored.Identity.ContentEncoding.ShouldBe("gzip");
		restored.Identity.MessageVersion.ShouldBe("2.0");
		restored.Identity.SerializerVersion.ShouldBe("3.0");
		restored.Identity.ContractVersion.ShouldBe("1.1.0");

		restored.Routing.Destination.ShouldBe("queue-a");
		restored.Routing.ReplyTo.ShouldBe("reply-q");
		restored.Routing.SessionId.ShouldBe("sess-1");
		restored.Routing.PartitionKey.ShouldBe("pk-1");
		restored.Routing.RoutingKey.ShouldBe("rk-1");
		restored.Routing.GroupId.ShouldBe("grp-1");
		restored.Routing.GroupSequence.ShouldBe(5);

		restored.Timing.SentTimestampUtc.ShouldBe(Sent);
		restored.Timing.ReceivedTimestampUtc.ShouldBe(Received);
		restored.Timing.ScheduledEnqueueTimeUtc.ShouldBe(Scheduled);
		restored.Timing.TimeToLive.ShouldBe(TimeSpan.FromMinutes(30));

		restored.Observability.TraceParent.ShouldBe("00-abc");
		restored.Observability.TraceState.ShouldBe("ts=1");
		restored.Observability.Baggage.ShouldBe("b=2");

		restored.Delivery.DeliveryCount.ShouldBe(2);
		restored.Delivery.MaxDeliveryCount.ShouldBe(10);
		restored.Delivery.LastDeliveryError.ShouldBe("boom");
		restored.Delivery.DeadLetterQueue.ShouldBe("dlq");
		restored.Delivery.DeadLetterReason.ShouldBe("expired");
		restored.Delivery.DeadLetterErrorDescription.ShouldBe("ttl elapsed");
		restored.Delivery.Priority.ShouldBe(7);
		restored.Delivery.Durable.ShouldBe(true);
		restored.Delivery.RequiresDuplicateDetection.ShouldBe(true);
		restored.Delivery.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromSeconds(45));

		restored.EventSourcing.AggregateId.ShouldBe("agg-1");
		restored.EventSourcing.AggregateType.ShouldBe("Order");
		restored.EventSourcing.AggregateVersion.ShouldBe(4);
		restored.EventSourcing.StreamName.ShouldBe("stream-1");
		restored.EventSourcing.StreamPosition.ShouldBe(9);
		restored.EventSourcing.GlobalPosition.ShouldBe(100);
		restored.EventSourcing.EventType.ShouldBe("OrderPlacedEvent");
		restored.EventSourcing.EventVersion.ShouldBe(2);

		restored.Security.UserId.ShouldBe("user-1");
		restored.Security.TenantId.ShouldBe("tenant-1");
		restored.Security.Roles.ShouldBe(["admin", "ops"]);
	}

	// ===== Fact 3: contract — focused group types are public; the flat parent properties are gone =====

	[Fact]
	public void PublishTheFocusedGroupTypesAndRemoveTheFlatParentProperties()
	{
		// The seven focused group types are part of the public contract.
		foreach (var groupType in new[]
		{
			typeof(MessageIdentity), typeof(MessageRouting), typeof(MessageTiming),
			typeof(MessageObservability), typeof(MessageDelivery), typeof(MessageEventSourcing),
			typeof(MessageSecurity),
		})
		{
			groupType.IsPublic.ShouldBeTrue($"{groupType.Name} must be a public contract type");
		}

		// The flat properties that lived on the old 53-property parent are no longer exposed on the
		// parent — they now live only on the focused groups. Their re-appearance here is the flat revert.
		foreach (var removedFlatProperty in new[]
		{
			"ExternalId", "Destination", "ReplyTo", "PartitionKey", "TraceParent", "TenantId",
			"UserId", "DeliveryCount", "MaxDeliveryCount", "AggregateId", "StreamName",
			"GlobalPosition", "SentTimestampUtc", "TimeToLive", "Priority", "Durable",
		})
		{
			typeof(MessageMetadata)
				.GetProperty(removedFlatProperty, BindingFlags.Public | BindingFlags.Instance)
				.ShouldBeNull($"flat property '{removedFlatProperty}' must not be exposed on the grouped parent");
		}
	}
}
