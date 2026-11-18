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
public sealed class InMemoryOutboxLeaseGenerationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseExpiredAcquisitionWithoutReleasingSuccessorOrChangingRetry(bool backoff)
	{
		var clock = new ManualClock();
		using var store = Create(clock);
		var message = new OutboundMessage("event", [1, 2], "destination");
		await store.StageMessageAsync(message, CancellationToken.None);
		var first = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		var firstIdentity = first.DispatcherId;
		(await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).ShouldBeEmpty();
		clock.Advance(TimeSpan.FromSeconds(11));
		var successor = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		(await store.MarkFailedAsync(first.Id, "stale", 99,
			backoff ? clock.GetUtcNow().AddHours(1) : null,
			new OutboxWriteAuthority(7, firstIdentity!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).StagedMessageCount.ShouldBe(1);
		(await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).ShouldBeEmpty();
		successor.ShouldNotBeSameAs(first);
		first.DispatcherId.ShouldBe(firstIdentity);
		successor.DispatcherId.ShouldNotBe(firstIdentity);
		first.Payload[0] = 9;
		successor.Payload[0].ShouldBe((byte)1);
		(await store.MarkFailedAsync(successor.Id, "current", 2, null,
			new OutboxWriteAuthority(7, successor.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		(await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).ShouldBeEmpty();
		clock.Advance(TimeSpan.FromSeconds(5));
		var retry = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		retry.RetryCount.ShouldBe(2);
		retry.LastError.ShouldBe("current");
		retry.LastAttemptAt.ShouldBe(clock.GetUtcNow().AddSeconds(-5));
		retry.DispatcherId.ShouldNotBe(successor.DispatcherId);
		(await store.MarkSentAsync(retry.Id, 7, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		(await store.CleanupAllTenantsSentMessagesAsync(clock.GetUtcNow(), 1, CancellationToken.None)).ShouldBe(0);
		clock.Advance(TimeSpan.FromSeconds(1));
		(await store.CleanupAllTenantsSentMessagesAsync(clock.GetUtcNow(), 1, CancellationToken.None)).ShouldBe(1);
	}

	[Fact]
	public async Task PreserveFencePrecedenceTerminalStatusAndIndependentRowProgress()
	{
		var clock = new ManualClock();
		using var store = Create(clock);
		var first = new OutboundMessage("event", [1], "destination");
		var second = new OutboundMessage("event", [2], "destination");
		await store.StageMessageAsync(first, CancellationToken.None);
		await store.StageMessageAsync(second, CancellationToken.None);
		var claims = (await store.GetUnsentMessagesAsync(2, 7, CancellationToken.None)).ToArray();
		(await store.MarkSentAsync(first.Id, 7, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		var firstClaim = claims.Single(x => x.Id == first.Id);
		(await store.MarkFailedAsync(first.Id, "late", 1, null,
			new OutboxWriteAuthority(7, firstClaim.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.AlreadyTerminal);
		_ = await store.GetUnsentMessagesAsync(1, 8, CancellationToken.None);
		(await store.MarkFailedAsync("missing", "stale", 1, null,
			new OutboxWriteAuthority(7, "old"), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
		(await store.MarkFailedAsync(first.Id, "stale", 1, null,
			new OutboxWriteAuthority(7, firstClaim.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
		var secondClaim = claims.Single(x => x.Id == second.Id);
		(await store.MarkFailedAsync(second.Id, "current", 1, null,
			new OutboxWriteAuthority(8, secondClaim.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).SentMessageCount.ShouldBe(1);
		(await store.GetAllTenantsStatisticsAsync(CancellationToken.None)).FailedMessageCount.ShouldBe(1);
	}

	private static InMemoryOutboxStore Create(TimeProvider clock) => new(
		Options.Create(new InMemoryOutboxOptions { LeaseTimeoutSeconds = 10, FailureBackoffFloorSeconds = 5 }),
		NullLogger<InMemoryOutboxStore>.Instance, clock);

	private sealed class ManualClock : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		public override DateTimeOffset GetUtcNow() => _now;
		internal void Advance(TimeSpan amount) => _now += amount;
	}
}
