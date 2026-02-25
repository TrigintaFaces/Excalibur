// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Dispatch.Tests.Metadata;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageMetadataExtensionsShould
{
	private static IMessageMetadata CreateTestMetadata(Action<MessageMetadataBuilder>? configure = null)
	{
		var builder = new MessageMetadataBuilder();
		builder.WithMessageId("msg-1");
		builder.WithCorrelationId("corr-1");
		builder.WithMessageType("TestEvent");
		builder.WithUserId("user-1");
		builder.WithTenantId("tenant-1");
		builder.WithSource("source-svc");
		builder.WithDestination("dest-svc");
		builder.WithReplyTo("reply-queue");
		builder.WithPartitionKey("pk-1");
		builder.WithSessionId("session-1");
		builder.WithSentTimestampUtc(DateTimeOffset.UtcNow);
		builder.WithReceivedTimestampUtc(DateTimeOffset.UtcNow);

		configure?.Invoke(builder);

		return builder.Build();
	}

	// --- ToUnified ---

	[Fact]
	public void ConvertLegacyMetadataToUnified()
	{
		// Arrange
		var legacy = A.Fake<IMessageMetadata>();
		A.CallTo(() => legacy.MessageId).Returns("msg-1");
		A.CallTo(() => legacy.CorrelationId).Returns("corr-1");
		A.CallTo(() => legacy.CausationId).Returns("cause-1");
		A.CallTo(() => legacy.MessageType).Returns("Test");
		A.CallTo(() => legacy.ContentType).Returns("application/json");
		A.CallTo(() => legacy.SerializerVersion).Returns("1.0");
		A.CallTo(() => legacy.MessageVersion).Returns("1.0");
		A.CallTo(() => legacy.ContractVersion).Returns("1.0.0");
		A.CallTo(() => legacy.TenantId).Returns("tenant-legacy");
		A.CallTo(() => legacy.UserId).Returns("user-legacy");
		A.CallTo(() => legacy.Headers).Returns(new Dictionary<string, string>());
		A.CallTo(() => legacy.Attributes).Returns(new Dictionary<string, object>());
		A.CallTo(() => legacy.Properties).Returns(new Dictionary<string, object>());
		A.CallTo(() => legacy.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => legacy.Roles).Returns(new List<string>());
		A.CallTo(() => legacy.Claims).Returns(new List<Claim>());

		// Act — ToUnified without context generates new GUID for MessageId
		var unified = legacy.ToUnified();

		// Assert — preserved fields
		unified.ShouldNotBeNull();
		unified.CorrelationId.ShouldBe("corr-1");
		unified.CausationId.ShouldBe("cause-1");
		unified.ContentType.ShouldBe("application/json");
		unified.TenantId.ShouldBe("tenant-legacy");
		unified.UserId.ShouldBe("user-legacy");
		unified.MessageId.ShouldNotBeNullOrWhiteSpace(); // new GUID
		unified.MessageType.ShouldBe("Unknown"); // default when no context
	}

	// --- ToLegacy ---

	[Fact]
	public void ThrowWhenToLegacyCalledWithNull()
	{
		IMessageMetadata metadata = null!;
		Should.Throw<ArgumentNullException>(() => metadata.ToLegacy());
	}

	[Fact]
	public void ReturnSameInstanceFromToLegacy()
	{
		var metadata = CreateTestMetadata();
		var result = metadata.ToLegacy();

		result.ShouldBeSameAs(metadata);
	}

	// --- ApplyToContext ---

	[Fact]
	public void ThrowWhenApplyToContextWithNullMetadata()
	{
		IMessageMetadata metadata = null!;
		Should.Throw<ArgumentNullException>(() => metadata.ApplyToContext(new MessageContext()));
	}

	[Fact]
	public void ThrowWhenApplyToContextWithNullContext()
	{
		var metadata = CreateTestMetadata();
		Should.Throw<ArgumentNullException>(() => metadata.ApplyToContext(null!));
	}

	[Fact]
	public void ApplyMetadataToContext()
	{
		// Arrange
		var metadata = CreateTestMetadata(b =>
		{
			b.WithCausationId("cause-1");
			b.WithExternalId("ext-1");
			b.WithTraceParent("00-trace-parent-01");
			b.WithDeliveryCount(2);
			b.AddItem("custom-key", "custom-value");
		});

		var context = new MessageContext();

		// Act
		metadata.ApplyToContext(context);

		// Assert
		context.MessageId.ShouldBe("msg-1");
		context.ExternalId.ShouldBe("ext-1");
		context.UserId.ShouldBe("user-1");
		context.CorrelationId.ShouldBe("corr-1");
		context.CausationId.ShouldBe("cause-1");
		context.TenantId.ShouldBe("tenant-1");
		context.TraceParent.ShouldBe("00-trace-parent-01");
		context.MessageType.ShouldBe("TestEvent");
		context.Source.ShouldBe("source-svc");
		context.DeliveryCount.ShouldBe(2);
	}

	[Fact]
	public void ApplySkipsBlankCorrelationId()
	{
		// Arrange
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithCorrelationId("corr-explicit")
			.WithMessageType("Test")
			.Build();

		var context = new MessageContext { CorrelationId = "original" };

		// When metadata has correlation, it should apply
		metadata.ApplyToContext(context);
		context.CorrelationId.ShouldBe("corr-explicit");
	}

	// --- ExtractMetadata ---

	[Fact]
	public void ThrowWhenExtractMetadataWithNullContext()
	{
		IMessageContext context = null!;
		Should.Throw<ArgumentNullException>(() => context.ExtractMetadata());
	}

	[Fact]
	public void ExtractMetadataFromContext()
	{
		// Arrange
		var context = new MessageContext
		{
			MessageId = "msg-extract",
			CorrelationId = "corr-extract",
			CausationId = "cause-extract",
			UserId = "user-extract",
			TenantId = "tenant-extract",
			MessageType = "ExtractType",
			ContentType = "application/xml",
			Source = "extract-svc",
			DeliveryCount = 3,
			ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			SentTimestampUtc = DateTimeOffset.UtcNow,
		};

		// Act
		var metadata = context.ExtractMetadata();

		// Assert
		metadata.MessageId.ShouldBe("msg-extract");
		metadata.CorrelationId.ShouldBe("corr-extract");
		metadata.CausationId.ShouldBe("cause-extract");
		metadata.UserId.ShouldBe("user-extract");
		metadata.TenantId.ShouldBe("tenant-extract");
		metadata.MessageType.ShouldBe("ExtractType");
		metadata.ContentType.ShouldBe("application/xml");
		metadata.Source.ShouldBe("extract-svc");
		metadata.DeliveryCount.ShouldBe(3);
	}

	[Fact]
	public void ExtractMetadataFillsDefaults()
	{
		// Arrange - context with null MessageId and MessageType
		var context = new MessageContext();

		// Act
		var metadata = context.ExtractMetadata();

		// Assert
		metadata.MessageId.ShouldNotBeNullOrWhiteSpace(); // auto-generated GUID
		metadata.MessageType.ShouldBe("Unknown");
		metadata.ContentType.ShouldBe("application/json");
	}

	// --- IsExpired ---

	[Fact]
	public void ReturnTrueWhenExplicitlyExpired()
	{
		var metadata = CreateTestMetadata(b =>
			b.WithExpiresAtUtc(DateTimeOffset.UtcNow.AddHours(-1)));

		metadata.IsExpired().ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenNotExpired()
	{
		var metadata = CreateTestMetadata(b =>
			b.WithExpiresAtUtc(DateTimeOffset.UtcNow.AddHours(1)));

		metadata.IsExpired().ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenTtlExpired()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithSentTimestampUtc(DateTimeOffset.UtcNow.AddHours(-2));
			b.WithTimeToLive(TimeSpan.FromHours(1));
		});

		metadata.IsExpired().ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenTtlNotExpired()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithSentTimestampUtc(DateTimeOffset.UtcNow);
			b.WithTimeToLive(TimeSpan.FromHours(1));
		});

		metadata.IsExpired().ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenNoExpirationSet()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithMessageType("Test")
			.Build();

		metadata.IsExpired().ShouldBeFalse();
	}

	[Fact]
	public void UseProvidedCurrentTime()
	{
		var sentTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var metadata = CreateTestMetadata(b =>
		{
			b.WithSentTimestampUtc(sentTime);
			b.WithTimeToLive(TimeSpan.FromHours(1));
		});

		// Check at a time before expiration
		metadata.IsExpired(sentTime.AddMinutes(30)).ShouldBeFalse();
		// Check at a time after expiration
		metadata.IsExpired(sentTime.AddHours(2)).ShouldBeTrue();
	}

	// --- ShouldDeadLetter ---

	[Fact]
	public void ReturnFalseWhenNoMaxDeliveryCount()
	{
		var metadata = CreateTestMetadata(b => b.WithDeliveryCount(10));

		metadata.ShouldDeadLetter().ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenDeliveryCountExceedsMax()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithDeliveryCount(5);
			b.WithMaxDeliveryCount(5);
		});

		metadata.ShouldDeadLetter().ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenDeliveryCountBelowMax()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithDeliveryCount(2);
			b.WithMaxDeliveryCount(5);
		});

		metadata.ShouldDeadLetter().ShouldBeFalse();
	}

	// --- CreateDeadLetterMetadata ---

	[Fact]
	public void ThrowWhenCreateDeadLetterMetadataWithNullMetadata()
	{
		IMessageMetadata metadata = null!;
		Should.Throw<ArgumentNullException>(() => metadata.CreateDeadLetterMetadata("reason"));
	}

	[Fact]
	public void ThrowWhenCreateDeadLetterMetadataWithNullReason()
	{
		var metadata = CreateTestMetadata();
		Should.Throw<ArgumentException>(() => metadata.CreateDeadLetterMetadata(null!));
	}

	[Fact]
	public void CreateDeadLetterMetadataWithDefaults()
	{
		var metadata = CreateTestMetadata();

		var dlq = metadata.CreateDeadLetterMetadata("MaxRetries", "Failed after 5 attempts");

		dlq.DeadLetterReason.ShouldBe("MaxRetries");
		dlq.DeadLetterErrorDescription.ShouldBe("Failed after 5 attempts");
		dlq.DeadLetterQueue.ShouldBe("dead-letter");
		dlq.LastDeliveryError.ShouldBe("Failed after 5 attempts");
	}

	[Fact]
	public void CreateDeadLetterMetadataWithCustomQueue()
	{
		var metadata = CreateTestMetadata();

		var dlq = metadata.CreateDeadLetterMetadata("Poison", deadLetterQueue: "custom-dlq");

		dlq.DeadLetterQueue.ShouldBe("custom-dlq");
	}

	// --- CreateReplyMetadata ---

	[Fact]
	public void ThrowWhenCreateReplyMetadataWithNull()
	{
		IMessageMetadata metadata = null!;
		Should.Throw<ArgumentNullException>(() => metadata.CreateReplyMetadata());
	}

	[Fact]
	public void CreateReplyMetadataPreservesCorrelation()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithTraceParent("00-trace-parent-01");
			b.WithTraceState("state-1");
			b.WithBaggage("key=value");
		});

		var reply = metadata.CreateReplyMetadata("ReplyType");

		reply.CorrelationId.ShouldBe("corr-1"); // Same correlation
		reply.CausationId.ShouldBe("msg-1"); // Original message caused the reply
		reply.MessageType.ShouldBe("ReplyType");
		reply.Source.ShouldBe("dest-svc"); // Swapped
		reply.Destination.ShouldBe("reply-queue"); // ReplyTo address
	}

	[Fact]
	public void CreateReplyMetadataDefaultsMessageType()
	{
		var metadata = CreateTestMetadata();

		var reply = metadata.CreateReplyMetadata();

		reply.MessageType.ShouldBe("Reply.TestEvent");
	}

	[Fact]
	public void CreateReplyMetadataFallsBackToSourceForDestination()
	{
		// When ReplyTo is null, use Source
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithCorrelationId("corr-1")
			.WithMessageType("Test")
			.WithSource("origin-svc")
			.Build();

		var reply = metadata.CreateReplyMetadata();

		reply.Destination.ShouldBe("origin-svc");
	}

	[Fact]
	public void CreateReplyMetadataCopiesRelevantHeaders()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.AddHeader("X-Request-Id", "req-1");
			b.AddHeader("X-Custom-Ignore", "ignored");
		});

		var reply = metadata.CreateReplyMetadata();

		reply.Headers.ShouldContainKey("X-Request-Id");
		reply.Headers.ShouldNotContainKey("X-Custom-Ignore");
	}

	// --- GetClaimsPrincipal ---

	[Fact]
	public void ReturnNullWhenNoIdentityInfo()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithMessageType("Test")
			.Build();

		metadata.GetClaimsPrincipal().ShouldBeNull();
	}

	[Fact]
	public void CreatePrincipalWithUserId()
	{
		var metadata = CreateTestMetadata();

		var principal = metadata.GetClaimsPrincipal();

		principal.ShouldNotBeNull();
		principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("user-1");
	}

	[Fact]
	public void CreatePrincipalWithRoles()
	{
		var metadata = CreateTestMetadata(b => b.WithRoles(["Admin", "User"]));

		var principal = metadata.GetClaimsPrincipal();

		principal.ShouldNotBeNull();
		principal!.IsInRole("Admin").ShouldBeTrue();
		principal.IsInRole("User").ShouldBeTrue();
	}

	[Fact]
	public void CreatePrincipalWithTenantClaim()
	{
		var metadata = CreateTestMetadata();

		var principal = metadata.GetClaimsPrincipal();

		principal.ShouldNotBeNull();
		principal!.FindFirst("TenantId")?.Value.ShouldBe("tenant-1");
	}

	[Fact]
	public void NotDuplicateExistingClaims()
	{
		var existingClaim = new Claim(ClaimTypes.NameIdentifier, "user-1");
		var metadata = CreateTestMetadata(b => b.WithClaims([existingClaim]));

		var principal = metadata.GetClaimsPrincipal();

		principal.ShouldNotBeNull();
		// Should have exactly one NameIdentifier claim (not duplicated)
		principal!.FindAll(ClaimTypes.NameIdentifier).Count().ShouldBe(1);
	}

	// --- Merge ---

	[Fact]
	public void ThrowWhenMergePrimaryIsNull()
	{
		IMessageMetadata primary = null!;
		var secondary = CreateTestMetadata();
		Should.Throw<ArgumentNullException>(() => primary.Merge(secondary));
	}

	[Fact]
	public void ThrowWhenMergeSecondaryIsNull()
	{
		var primary = CreateTestMetadata();
		Should.Throw<ArgumentNullException>(() => primary.Merge(null!));
	}

	[Fact]
	public void MergeOverridesWithSecondaryValues()
	{
		var primary = CreateTestMetadata(b =>
		{
			b.WithUserId("primary-user");
			b.WithTenantId("primary-tenant");
			b.AddHeader("h1", "primary");
		});

		var secondaryBuilder = new MessageMetadataBuilder();
		secondaryBuilder.WithMessageId("msg-2");
		secondaryBuilder.WithCorrelationId("corr-2");
		secondaryBuilder.WithCausationId("cause-2");
		secondaryBuilder.WithUserId("secondary-user");
		secondaryBuilder.WithTenantId("secondary-tenant");
		secondaryBuilder.WithMessageType("SecondaryType");
		secondaryBuilder.AddHeader("h2", "secondary");
		var secondary = secondaryBuilder.Build();

		var merged = primary.Merge(secondary);

		merged.MessageId.ShouldBe("msg-2");
		merged.CorrelationId.ShouldBe("corr-2");
		merged.CausationId.ShouldBe("cause-2");
		merged.UserId.ShouldBe("secondary-user");
		merged.TenantId.ShouldBe("secondary-tenant");
		merged.Headers.ShouldContainKey("h1"); // from primary
		merged.Headers.ShouldContainKey("h2"); // from secondary
	}

	[Fact]
	public void MergeCombinesRolesWithoutDuplicates()
	{
		var primary = CreateTestMetadata(b => b.WithRoles(["Admin", "User"]));
		var secondary = new MessageMetadataBuilder()
			.WithMessageId("msg-2")
			.WithCorrelationId("corr-2")
			.WithMessageType("Test")
			.WithRoles(["User", "Manager"])
			.Build();

		var merged = primary.Merge(secondary);

		merged.Roles.ShouldContain("Admin");
		merged.Roles.ShouldContain("User");
		merged.Roles.ShouldContain("Manager");
	}

	// --- ToDictionary ---

	[Fact]
	public void ThrowWhenToDictionaryWithNull()
	{
		IMessageMetadata metadata = null!;
		Should.Throw<ArgumentNullException>(() => metadata.ToDictionary());
	}

	[Fact]
	public void ConvertMetadataToDictionary()
	{
		var metadata = CreateTestMetadata(b =>
		{
			b.WithDeliveryCount(2);
			b.WithPriority(3);
			b.WithGroupSequence(1);
			b.WithRoles(["Admin"]);
			b.AddHeader("h1", "v1");
			b.AddAttribute("a1", 42);
			b.AddProperty("p1", "val");
		});

		var dict = metadata.ToDictionary();

		dict.ShouldContainKey("MessageId");
		dict.ShouldContainKey("CorrelationId");
		dict.ShouldContainKey("MessageType");
		dict.ShouldContainKey("CreatedTimestampUtc");
		dict.ShouldContainKey("DeliveryCount");
		dict.ShouldContainKey("Priority");
		dict.ShouldContainKey("GroupSequence");
		dict.ShouldContainKey("Roles");
		dict.ShouldContainKey("Headers");
		dict.ShouldContainKey("Attributes");
		dict.ShouldContainKey("Properties");
	}

	[Fact]
	public void IncludeNullValuesWhenRequested()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithMessageType("Test")
			.Build();

		var dict = metadata.ToDictionary(includeNullValues: true);

		dict.ShouldContainKey("CausationId");
		dict.ShouldContainKey("ExternalId");
		dict.ShouldContainKey("TraceParent");
	}

	[Fact]
	public void ExcludeNullValuesByDefault()
	{
		var metadata = new MessageMetadataBuilder()
			.WithMessageId("msg-1")
			.WithMessageType("Test")
			.Build();

		var dict = metadata.ToDictionary();

		dict.ShouldNotContainKey("CausationId");
		dict.ShouldNotContainKey("ExternalId");
		dict.ShouldNotContainKey("TraceParent");
	}

	[Fact]
	public void IncludeTimestampsInIsoFormat()
	{
		var metadata = CreateTestMetadata();

		var dict = metadata.ToDictionary();

		dict["CreatedTimestampUtc"].ShouldBeOfType<string>();
		((string)dict["CreatedTimestampUtc"]!).ShouldContain("T"); // ISO 8601
	}

	// --- WithCurrentTraceContext ---

	[Fact]
	public void ReturnSameMetadataWhenNoActivity()
	{
		// Ensure no current activity
		var saved = Activity.Current;
		Activity.Current = null;
		try
		{
			var metadata = CreateTestMetadata();
			var result = metadata.WithCurrentTraceContext();

			result.ShouldNotBeNull();
		}
		finally
		{
			Activity.Current = saved;
		}
	}

	[Fact]
	public void EnrichWithActivityTraceContext()
	{
		using var source = new ActivitySource("Test.Trace");
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name == "Test.Trace",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = source.StartActivity("TestOp");
		activity.ShouldNotBeNull();
		activity!.TraceStateString = "state=test";
		activity.AddBaggage("key1", "val1");

		var metadata = CreateTestMetadata();
		var enriched = metadata.WithCurrentTraceContext();

		enriched.TraceParent.ShouldNotBeNullOrWhiteSpace();
		enriched.TraceState.ShouldBe("state=test");
		enriched.Baggage.ShouldContain("key1=val1");
	}
}
