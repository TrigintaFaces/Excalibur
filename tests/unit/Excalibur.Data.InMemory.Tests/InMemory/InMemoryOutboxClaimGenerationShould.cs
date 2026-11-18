// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Pattern", "Outbox")]
public sealed class InMemoryOutboxClaimGenerationShould
{
	[Fact]
	public async Task OwnStagedEnvelopeAndClaimSnapshot()
	{
		using var store = Create();
		var source = new OutboundMessage("event", [1, 2], "destination")
		{
			CorrelationId = "correlation", CausationId = "causation", TenantId = "tenant",
			PartitionKey = "partition", GroupKey = "group", SequenceNumber = 42,
			TargetTransports = "bus", IsMultiTransport = true, Priority = 3,
		};
		source.Headers["key"] = "original";
		source.TransportDeliveries.Add(new OutboundMessageTransport(source.Id, "bus")
		{
			Destination = "transport-destination", RetryCount = 2, TransportMetadata = "metadata",
		});
		await store.StageMessageAsync(source, CancellationToken.None);
		source.Payload[0] = 9;
		source.Headers["key"] = "changed";
		source.Destination = "changed";
		source.TransportDeliveries.Single().Destination = "changed";
		var first = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		first.ShouldNotBeSameAs(source);
		first.Payload.ShouldBe(new byte[] { 1, 2 });
		first.Headers["key"].ShouldBe("original");
		first.Destination.ShouldBe("destination");
		first.CorrelationId.ShouldBe("correlation");
		first.CausationId.ShouldBe("causation");
		first.TenantId.ShouldBe("tenant");
		first.PartitionKey.ShouldBe("partition");
		first.GroupKey.ShouldBe("group");
		first.SequenceNumber.ShouldBe(42);
		first.TargetTransports.ShouldBe("bus");
		first.IsMultiTransport.ShouldBeTrue();
		first.Priority.ShouldBe(3);
		first.TransportDeliveries.Single().Destination.ShouldBe("transport-destination");
		first.TransportDeliveries.Single().RetryCount.ShouldBe(2);
		first.TransportDeliveries.Single().TransportMetadata.ShouldBe("metadata");
		first.Status = OutboxStatus.Sent;
		first.Payload[0] = 8;
		first.Headers["key"] = "claim-changed";
		first.TransportDeliveries.Clear();
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).StagedMessageCount.ShouldBe(1);
		(await store.MarkFailedAsync(first.Id, "retry", 1, null,
			new OutboxWriteAuthority(7, first.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		var failed = (await store.GetAllTenantsFailedMessagesAsync(3, null, 1, CancellationToken.None)).Single();
		failed.Payload.ShouldBe(new byte[] { 1, 2 });
		failed.Headers["key"].ShouldBe("original");
		failed.TransportDeliveries.Count.ShouldBe(1);
		failed.Status = OutboxStatus.Sent;
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).FailedMessageCount.ShouldBe(1);
	}

	[Theory]
	[InlineData(0, "claim")]
	[InlineData(-1, "claim")]
	[InlineData(7, null)]
	[InlineData(7, "")]
	[InlineData(7, " ")]
	[InlineData(0, null)]
	public async Task RejectInvalidStrongAuthorityWithoutMutation(long token, string? identity)
	{
		using var store = Create();
		var message = new OutboundMessage("event", [1], "destination");
		await store.StageMessageAsync(message, CancellationToken.None);
		await Assert.ThrowsAnyAsync<ArgumentException>(() => store.MarkFailedAsync(message.Id, "invalid", 9, null,
			new OutboxWriteAuthority(token, identity!), CancellationToken.None).AsTask());
		(await store.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBeNull();
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).StagedMessageCount.ShouldBe(1);
		(await store.GetUnsentMessagesAsync(1, 1, CancellationToken.None)).Count().ShouldBe(1);
	}

	[Fact]
	public async Task RefuseUnleasedAndReleasedStrongReportsWhilePreservingLegacyFailure()
	{
		using var store = Create();
		var message = new OutboundMessage("event", [1], "destination");
		await store.StageMessageAsync(message, CancellationToken.None);
		(await store.MarkFailedAsync(message.Id, "unleased", 9, null,
			new OutboxWriteAuthority(7, "invented"), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
		var claim = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		var authority = new OutboxWriteAuthority(7, claim.DispatcherId!);
		(await store.MarkFailedAsync(claim.Id, "current", 1, null, authority, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		(await store.MarkFailedAsync(claim.Id, "duplicate", 9, null, authority, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
		var failed = (await store.GetAllTenantsFailedMessagesAsync(0, null, 1, CancellationToken.None)).Single();
		failed.RetryCount.ShouldBe(1);
		failed.LastError.ShouldBe("current");
		await store.MarkFailedAsync(claim.Id, "administrative", 2, CancellationToken.None);
		failed = (await store.GetAllTenantsFailedMessagesAsync(0, null, 1, CancellationToken.None)).Single();
		failed.RetryCount.ShouldBe(2);
		failed.LastError.ShouldBe("administrative");
	}

	[Fact]
	public async Task OwnScheduledReadWithoutChangingEnvelopeMetadata()
	{
		using var store = Create();
		var scheduledAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var source = new OutboundMessage("event", [1], "destination")
		{
			ScheduledAt = scheduledAt, CreatedAt = scheduledAt.AddDays(-1),
			LastAttemptAt = scheduledAt.AddDays(-2), SentAt = scheduledAt.AddDays(-3),
			RetryCount = 2, LastError = "previous", DispatcherId = "producer-stamp",
		};
		await store.StageMessageAsync(source, CancellationToken.None);
		var snapshot = (await store.GetAllTenantsScheduledMessagesAsync(scheduledAt, 1, CancellationToken.None)).Single();
		snapshot.ShouldNotBeSameAs(source);
		snapshot.CreatedAt.ShouldBe(source.CreatedAt);
		snapshot.ScheduledAt.ShouldBe(source.ScheduledAt);
		snapshot.LastAttemptAt.ShouldBe(source.LastAttemptAt);
		snapshot.SentAt.ShouldBe(source.SentAt);
		snapshot.RetryCount.ShouldBe(2);
		snapshot.LastError.ShouldBe("previous");
		snapshot.DispatcherId.ShouldBe("producer-stamp");
		snapshot.ScheduledAt = null;
		snapshot.Payload[0] = 9;
		var again = (await store.GetAllTenantsScheduledMessagesAsync(scheduledAt, 1, CancellationToken.None)).Single();
		again.ScheduledAt.ShouldBe(scheduledAt);
		again.Payload[0].ShouldBe((byte)1);
		(await store.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldBeEmpty();
	}

	private static InMemoryOutboxStore Create() => new(
		Options.Create(new InMemoryOutboxOptions()), NullLogger<InMemoryOutboxStore>.Instance);
}
