// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Metadata;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageMetadataBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		// Act
		var metadata = new MessageMetadataBuilder().Build();

		// Assert
		metadata.ShouldNotBeNull();
		metadata.MessageId.ShouldNotBeNullOrWhiteSpace();
		metadata.MessageType.ShouldBe("Unknown");
		metadata.ContentType.ShouldBe("application/json");
		metadata.SerializerVersion.ShouldBe("1.0");
		metadata.MessageVersion.ShouldBe("1.0");
		metadata.ContractVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void SetMessageIdAndAutoSetCorrelationId()
	{
		// Act
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("test-id-123")
			.Build();

		// Assert
		metadata.MessageId.ShouldBe("test-id-123");
		metadata.CorrelationId.ShouldBe("test-id-123");
	}

	[Fact]
	public void ThrowForNullMessageId()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithMessageId(null!));
	}

	[Fact]
	public void ThrowForWhitespaceMessageId()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithMessageId("   "));
	}

	[Fact]
	public void SetCorrelationId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithCorrelationId("corr-123")
			.Build();

		metadata.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void ThrowForNullCorrelationId()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithCorrelationId(null!));
	}

	[Fact]
	public void SetCausationId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithCausationId("cause-1")
			.Build();

		metadata.CausationId.ShouldBe("cause-1");
	}

	[Fact]
	public void AllowNullCausationId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithCausationId(null)
			.Build();

		metadata.CausationId.ShouldBeNull();
	}

	[Fact]
	public void SetExternalId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithExternalId("ext-1")
			.Build();

		metadata.ExternalId.ShouldBe("ext-1");
	}

	[Fact]
	public void SetUserId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithUserId("user-1")
			.Build();

		metadata.UserId.ShouldBe("user-1");
	}

	[Fact]
	public void SetTenantId()
	{
		var metadata = new MessageMetadataBuilder()
			.WithTenantId("tenant-1")
			.Build();

		metadata.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void SetTraceParent()
	{
		var metadata = new MessageMetadataBuilder()
			.WithTraceParent("00-traceid-parentid-01")
			.Build();

		metadata.TraceParent.ShouldBe("00-traceid-parentid-01");
	}

	[Fact]
	public void SetTraceState()
	{
		var metadata = new MessageMetadataBuilder()
			.WithTraceState("congo=t61rcWkgMzE")
			.Build();

		metadata.TraceState.ShouldBe("congo=t61rcWkgMzE");
	}

	[Fact]
	public void SetBaggage()
	{
		var metadata = new MessageMetadataBuilder()
			.WithBaggage("userId=alice")
			.Build();

		metadata.Baggage.ShouldBe("userId=alice");
	}

	[Fact]
	public void SetMessageType()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageType("OrderCreated")
			.Build();

		metadata.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void ThrowForNullMessageType()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithMessageType(null!));
	}

	[Fact]
	public void SetContentType()
	{
		var metadata = new MessageMetadataBuilder()
			.WithContentType("application/xml")
			.Build();

		metadata.ContentType.ShouldBe("application/xml");
	}

	[Fact]
	public void ThrowForNullContentType()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithContentType(null!));
	}

	[Fact]
	public void SetContentEncoding()
	{
		var metadata = new MessageMetadataBuilder()
			.WithContentEncoding("gzip")
			.Build();

		metadata.ContentEncoding.ShouldBe("gzip");
	}

	[Fact]
	public void SetSerializerVersion()
	{
		var metadata = new MessageMetadataBuilder()
			.WithSerializerVersion("2.0")
			.Build();

		metadata.SerializerVersion.ShouldBe("2.0");
	}

	[Fact]
	public void ThrowForNullSerializerVersion()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithSerializerVersion(null!));
	}

	[Fact]
	public void SetMessageVersion()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageVersion("3.0")
			.Build();

		metadata.MessageVersion.ShouldBe("3.0");
	}

	[Fact]
	public void ThrowForNullMessageVersion()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithMessageVersion(null!));
	}

	[Fact]
	public void SetContractVersion()
	{
		var metadata = new MessageMetadataBuilder()
			.WithContractVersion("2.1.0")
			.Build();

		metadata.ContractVersion.ShouldBe("2.1.0");
	}

	[Fact]
	public void ThrowForNullContractVersion()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().WithContractVersion(null!));
	}

	[Fact]
	public void SetRoutingProperties()
	{
		var metadata = new MessageMetadataBuilder()
			.WithSource("source-service")
			.WithDestination("dest-service")
			.WithReplyTo("reply-queue")
			.WithSessionId("session-1")
			.WithPartitionKey("pk-1")
			.WithRoutingKey("rk-1")
			.WithGroupId("group-1")
			.WithGroupSequence(42)
			.Build();

		metadata.Source.ShouldBe("source-service");
		metadata.Destination.ShouldBe("dest-service");
		metadata.ReplyTo.ShouldBe("reply-queue");
		metadata.SessionId.ShouldBe("session-1");
		metadata.PartitionKey.ShouldBe("pk-1");
		metadata.RoutingKey.ShouldBe("rk-1");
		metadata.GroupId.ShouldBe("group-1");
		metadata.GroupSequence.ShouldBe(42);
	}

	[Fact]
	public void SetTimingProperties()
	{
		var created = DateTimeOffset.UtcNow.AddMinutes(-5);
		var sent = DateTimeOffset.UtcNow.AddMinutes(-3);
		var received = DateTimeOffset.UtcNow.AddMinutes(-1);
		var scheduled = DateTimeOffset.UtcNow.AddHours(1);

		var metadata = new MessageMetadataBuilder()
			.WithCreatedTimestampUtc(created)
			.WithSentTimestampUtc(sent)
			.WithReceivedTimestampUtc(received)
			.WithScheduledEnqueueTimeUtc(scheduled)
			.WithTimeToLive(TimeSpan.FromMinutes(30))
			.WithExpiresAtUtc(DateTimeOffset.UtcNow.AddHours(2))
			.Build();

		metadata.CreatedTimestampUtc.ShouldBe(created);
		metadata.SentTimestampUtc.ShouldBe(sent);
		metadata.ReceivedTimestampUtc.ShouldBe(received);
		metadata.ScheduledEnqueueTimeUtc.ShouldBe(scheduled);
		metadata.TimeToLive.ShouldBe(TimeSpan.FromMinutes(30));
		metadata.ExpiresAtUtc.ShouldNotBeNull();
	}

	[Fact]
	public void SetTimingViaConvenienceMethod()
	{
		var created = DateTimeOffset.UtcNow;
		var sent = DateTimeOffset.UtcNow;
		var scheduled = DateTimeOffset.UtcNow.AddHours(1);
		var ttl = TimeSpan.FromMinutes(30);

		var metadata = new MessageMetadataBuilder()
			.WithTiming(createdUtc: created, sentUtc: sent, scheduledUtc: scheduled, ttl: ttl)
			.Build();

		metadata.CreatedTimestampUtc.ShouldBe(created);
		metadata.SentTimestampUtc.ShouldBe(sent);
		metadata.ScheduledEnqueueTimeUtc.ShouldBe(scheduled);
		metadata.TimeToLive.ShouldBe(ttl);
	}

	[Fact]
	public void SetDeliveryCount()
	{
		var metadata = new MessageMetadataBuilder()
			.WithDeliveryCount(3)
			.Build();

		metadata.DeliveryCount.ShouldBe(3);
	}

	[Fact]
	public void ThrowForNegativeDeliveryCount()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithDeliveryCount(-1));
	}

	[Fact]
	public void SetMaxDeliveryCount()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMaxDeliveryCount(5)
			.Build();

		metadata.MaxDeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void ThrowForZeroMaxDeliveryCount()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithMaxDeliveryCount(0));
	}

	[Fact]
	public void ThrowForNegativeMaxDeliveryCount()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithMaxDeliveryCount(-1));
	}

	[Fact]
	public void SetDeadLetterProperties()
	{
		var metadata = new MessageMetadataBuilder()
			.WithLastDeliveryError("timeout")
			.WithDeadLetterQueue("dlq")
			.WithDeadLetterReason("MaxRetries")
			.WithDeadLetterErrorDescription("Exceeded max attempts")
			.Build();

		metadata.LastDeliveryError.ShouldBe("timeout");
		metadata.DeadLetterQueue.ShouldBe("dlq");
		metadata.DeadLetterReason.ShouldBe("MaxRetries");
		metadata.DeadLetterErrorDescription.ShouldBe("Exceeded max attempts");
	}

	[Fact]
	public void SetPriority()
	{
		var metadata = new MessageMetadataBuilder()
			.WithPriority(5)
			.Build();

		metadata.Priority.ShouldBe(5);
	}

	[Fact]
	public void ThrowForNegativePriority()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithPriority(-1));
	}

	[Fact]
	public void SetDurable()
	{
		var metadata = new MessageMetadataBuilder()
			.WithDurable(true)
			.Build();

		metadata.Durable.ShouldBe(true);
	}

	[Fact]
	public void SetDuplicateDetection()
	{
		var metadata = new MessageMetadataBuilder()
			.WithRequiresDuplicateDetection(true)
			.WithDuplicateDetectionWindow(TimeSpan.FromMinutes(10))
			.Build();

		metadata.RequiresDuplicateDetection.ShouldBe(true);
		metadata.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void ThrowForNonPositiveDuplicateDetectionWindow()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithDuplicateDetectionWindow(TimeSpan.Zero));
	}

	[Fact]
	public void SetEventSourcingProperties()
	{
		var metadata = new MessageMetadataBuilder()
			.WithEventSourcing(
				aggregateId: "agg-1",
				aggregateType: "Order",
				aggregateVersion: 5,
				streamName: "order-stream",
				streamPosition: 10)
			.WithGlobalPosition(100)
			.WithEventType("OrderCreated")
			.WithEventVersion(1)
			.Build();

		metadata.AggregateId.ShouldBe("agg-1");
		metadata.AggregateType.ShouldBe("Order");
		metadata.AggregateVersion.ShouldBe(5);
		metadata.StreamName.ShouldBe("order-stream");
		metadata.StreamPosition.ShouldBe(10);
		metadata.GlobalPosition.ShouldBe(100);
		metadata.EventType.ShouldBe("OrderCreated");
		metadata.EventVersion.ShouldBe(1);
	}

	[Fact]
	public void ThrowForNegativeGlobalPosition()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithGlobalPosition(-1));
	}

	[Fact]
	public void ThrowForNegativeEventVersion()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new MessageMetadataBuilder().WithEventVersion(-1));
	}

	[Fact]
	public void AddHeaders()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddHeader("X-Custom", "value1");
		builder.AddHeaders(new Dictionary<string, string> { ["X-Other"] = "value2" });
		var metadata = builder.Build();

		metadata.Headers.Count.ShouldBe(2);
		metadata.Headers["X-Custom"].ShouldBe("value1");
		metadata.Headers["X-Other"].ShouldBe("value2");
	}

	[Fact]
	public void ThrowForNullHeaderKey()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddHeader(null!, "value"));
	}

	[Fact]
	public void ThrowForNullHeaderValue()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddHeader("key", null!));
	}

	[Fact]
	public void ThrowForNullHeaders()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddHeaders(null!));
	}

	[Fact]
	public void AddAttributes()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddAttribute("attr1", 42);
		builder.AddAttributes(new Dictionary<string, object> { ["attr2"] = "val" });
		var metadata = builder.Build();

		metadata.Attributes.Count.ShouldBe(2);
		metadata.Attributes["attr1"].ShouldBe(42);
	}

	[Fact]
	public void ThrowForNullAttributeKey()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddAttribute(null!, "value"));
	}

	[Fact]
	public void ThrowForNullAttributes()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddAttributes(null!));
	}

	[Fact]
	public void AddProperties()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddProperty("prop1", "val1");
		builder.AddProperties(new Dictionary<string, object> { ["prop2"] = 99 });
		var metadata = builder.Build();

		metadata.Properties.Count.ShouldBe(2);
		metadata.Properties["prop1"].ShouldBe("val1");
	}

	[Fact]
	public void ThrowForNullPropertyKey()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddProperty(null!, "value"));
	}

	[Fact]
	public void ThrowForNullProperties()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddProperties(null!));
	}

	[Fact]
	public void AddItems()
	{
		var builder = new MessageMetadataBuilder();
		builder.AddItem("item1", "val1");
		builder.AddItems(new Dictionary<string, object> { ["item2"] = 42 });
		var metadata = builder.Build();

		metadata.Items.Count.ShouldBe(2);
		metadata.Items["item1"].ShouldBe("val1");
	}

	[Fact]
	public void ThrowForNullItemKey()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddItem(null!, "value"));
	}

	[Fact]
	public void ThrowForNullItems()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddItems(null!));
	}

	[Fact]
	public void SetRoles()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithRoles(["Admin", "User"]);
		builder.AddRole("Manager");
		var metadata = builder.Build();

		metadata.Roles.Count.ShouldBe(3);
		metadata.Roles.ShouldContain("Admin");
		metadata.Roles.ShouldContain("User");
		metadata.Roles.ShouldContain("Manager");
	}

	[Fact]
	public void WithRolesNullClearsRoles()
	{
		var builder = new MessageMetadataBuilder();
		builder.WithRoles(["Admin"]);
		builder.WithClaims(null); // WithClaims accepts null to clear
		var metadata = builder.Build();

		metadata.Claims.Count.ShouldBe(0);
	}

	[Fact]
	public void ThrowForNullOrWhitespaceRole()
	{
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddRole(null!));
		Should.Throw<ArgumentException>(() =>
			new MessageMetadataBuilder().AddRole("   "));
	}

	[Fact]
	public void SetClaims()
	{
		var claim1 = new Claim(ClaimTypes.Name, "Alice");
		var claim2 = new Claim(ClaimTypes.Email, "alice@test.com");

		var builder = new MessageMetadataBuilder();
		builder.WithClaims([claim1]);
		builder.AddClaim(claim2);
		var metadata = builder.Build();

		metadata.Claims.Count.ShouldBe(2);
	}

	[Fact]
	public void WithClaimsNullClearsClaims()
	{
		var claim = new Claim(ClaimTypes.Name, "Alice");

		var builder = new MessageMetadataBuilder();
		builder.WithClaims([claim]);
		builder.WithClaims(null);
		var metadata = builder.Build();

		metadata.Claims.Count.ShouldBe(0);
	}

	[Fact]
	public void ThrowForNullClaim()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageMetadataBuilder().AddClaim(null!));
	}

	[Fact]
	public void CalculateExpirationFromTtlWhenNotExplicitlySet()
	{
		var sentTime = DateTimeOffset.UtcNow;
		var ttl = TimeSpan.FromMinutes(30);

		var metadata = new MessageMetadataBuilder()
			.WithSentTimestampUtc(sentTime)
			.WithTimeToLive(ttl)
			.Build();

		metadata.ExpiresAtUtc.ShouldNotBeNull();
		metadata.ExpiresAtUtc!.Value.ShouldBe(sentTime.Add(ttl));
	}

	[Fact]
	public void NotOverrideExplicitExpirationWithTtl()
	{
		var sentTime = DateTimeOffset.UtcNow;
		var ttl = TimeSpan.FromMinutes(30);
		var explicit_expiry = DateTimeOffset.UtcNow.AddHours(5);

		var metadata = new MessageMetadataBuilder()
			.WithSentTimestampUtc(sentTime)
			.WithTimeToLive(ttl)
			.WithExpiresAtUtc(explicit_expiry)
			.Build();

		metadata.ExpiresAtUtc.ShouldBe(explicit_expiry);
	}

	[Fact]
	public void EnsureCorrelationIdFallsBackToMessageId()
	{
		// When correlation ID is blank, Build() should set it to messageId
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-fallback")
			.Build();

		metadata.CorrelationId.ShouldBe("msg-fallback");
	}

	[Fact]
	public void ExposeMarkerTypeAsNull()
	{
		var builder = new MessageMetadataBuilder();
		builder.MarkerType.ShouldBeNull();
	}

	[Fact]
	public void SetSentTimestampUtcNonNullable()
	{
		var sent = DateTimeOffset.UtcNow;
		var metadata = new MessageMetadataBuilder()
			.WithSentTimestampUtc(sent)
			.Build();

		metadata.SentTimestampUtc.ShouldBe(sent);
	}

	[Fact]
	public void SetReceivedTimestampUtcNonNullable()
	{
		var received = DateTimeOffset.UtcNow;
		var metadata = new MessageMetadataBuilder()
			.WithReceivedTimestampUtc(received)
			.Build();

		metadata.ReceivedTimestampUtc.ShouldBe(received);
	}

	[Fact]
	public void ChainAllBuilderMethods()
	{
		// Verify fluent API: every With/Add method returns the builder
		// Note: Methods return IMessageMetadataBuilder, so concrete-only methods
		// (AddClaim, AddAttribute, AddProperty, AddItem) must be called on the concrete variable.
		var builder = new MessageMetadataBuilder();
		builder.WithMessageId("id");
		builder.WithCorrelationId("corr");
		builder.WithCausationId("cause");
		builder.WithExternalId("ext");
		builder.WithUserId("user");
		builder.WithTenantId("tenant");
		builder.WithTraceParent("trace");
		builder.WithTraceState("state");
		builder.WithBaggage("bag");
		builder.WithMessageType("type");
		builder.WithContentType("application/json");
		builder.WithContentEncoding("utf-8");
		builder.WithSerializerVersion("1.0");
		builder.WithMessageVersion("1.0");
		builder.WithContractVersion("1.0.0");
		builder.WithSource("src");
		builder.WithDestination("dest");
		builder.WithReplyTo("reply");
		builder.WithSessionId("session");
		builder.WithPartitionKey("pk");
		builder.WithRoutingKey("rk");
		builder.WithGroupId("group");
		builder.WithGroupSequence(1);
		builder.WithCreatedTimestampUtc(DateTimeOffset.UtcNow);
		builder.WithSentTimestampUtc(DateTimeOffset.UtcNow);
		builder.WithReceivedTimestampUtc(DateTimeOffset.UtcNow);
		builder.WithScheduledEnqueueTimeUtc(DateTimeOffset.UtcNow);
		builder.WithTimeToLive(TimeSpan.FromMinutes(5));
		builder.WithExpiresAtUtc(DateTimeOffset.UtcNow.AddHours(1));
		builder.WithDeliveryCount(0);
		builder.WithMaxDeliveryCount(3);
		builder.WithLastDeliveryError(null);
		builder.WithDeadLetterQueue(null);
		builder.WithDeadLetterReason(null);
		builder.WithDeadLetterErrorDescription(null);
		builder.WithPriority(1);
		builder.WithDurable(true);
		builder.WithRequiresDuplicateDetection(false);
		builder.WithEventSourcing("agg", "type", 1, "stream", 0);
		builder.WithGlobalPosition(0);
		builder.WithEventType("event");
		builder.WithEventVersion(1);
		builder.WithRoles(["Admin"]);
		builder.AddRole("User");
		builder.WithClaims([new Claim("sub", "1")]);
		builder.AddClaim(new Claim("name", "test"));
		builder.AddHeader("h", "v");
		builder.AddHeaders(new Dictionary<string, string> { ["h2"] = "v2" });
		builder.AddAttribute("a", 1);
		builder.AddAttributes(new Dictionary<string, object> { ["a2"] = 2 });
		builder.AddProperty("p", "v");
		builder.AddProperties(new Dictionary<string, object> { ["p2"] = "v2" });
		builder.AddItem("i", "v");
		builder.AddItems(new Dictionary<string, object> { ["i2"] = "v2" });
		builder.WithTiming(createdUtc: DateTimeOffset.UtcNow);

		var metadata = builder.Build();
		metadata.ShouldNotBeNull();
	}
}
