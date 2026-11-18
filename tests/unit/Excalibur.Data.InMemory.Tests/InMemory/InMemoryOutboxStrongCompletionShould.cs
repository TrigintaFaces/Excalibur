// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Pattern", "Outbox")]
public sealed class InMemoryOutboxStrongCompletionShould
{
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task RefuseOldAcquisitionWithoutDamagingCurrentOwner(bool fenced, int operation)
    {
        var clock = new CompletionClock();
        using var store = Create(clock);
        var source = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(source, CancellationToken.None);
        var old = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        var oldIdentity = old.DispatcherId!;
        clock.Advance(TimeSpan.FromSeconds(3));
        var current = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        current.DispatcherId.ShouldNotBe(oldIdentity);
        old.DispatcherId.ShouldBe(oldIdentity);
        (await Complete(store, source.Id, oldIdentity, fenced, operation)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, source.Id, current.DispatcherId!, fenced, 0)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, source.Id, oldIdentity, fenced, operation)).ShouldBe(OutboxCompletionOutcome.AlreadyTerminal);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task RefuseExpiredAndReleasedClaimsWithoutWaitingForAReclaim(bool fenced, int operation)
    {
        var clock = new CompletionClock();
        using var store = Create(clock);
        var source = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(source, CancellationToken.None);
        var claim = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        clock.Advance(TimeSpan.FromSeconds(2));
        (await Complete(store, source.Id, claim.DispatcherId!, fenced, operation)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var successor = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        var plain = ((IServiceProvider)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
        (await plain.MarkFailedAsync(source.Id, "observed", 1, successor.DispatcherId!, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, source.Id, successor.DispatcherId!, fenced, operation)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var failed = (await store.GetAllTenantsFailedMessagesAsync(5, null, 10, CancellationToken.None)).Single();
        failed.RetryCount.ShouldBe(1);
        failed.LastError.ShouldBe("observed");
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task CurrentOwnerProgressesAndCannotRepeatCompletion(bool fenced, int operation)
    {
        var clock = new CompletionClock();
        using var store = Create(clock);
        var source = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(source, CancellationToken.None);
        (await Complete(store, source.Id, "unclaimed", fenced, operation)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var claim = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        (await Complete(store, source.Id, claim.DispatcherId!, fenced, operation)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, source.Id, claim.DispatcherId!, fenced, operation)).ShouldBe(operation is 0 or 3
            ? OutboxCompletionOutcome.AlreadyTerminal : OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, "missing", "unknown", fenced, operation)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReclaimExactlyAtExpiryButNotBefore(bool fenced)
    {
        var clock = new CompletionClock();
        using var store = Create(clock);
        var source = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(source, CancellationToken.None);
        var first = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        var firstIdentity = first.DispatcherId!;

        clock.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).ShouldBeEmpty();
        first.DispatcherId.ShouldBe(firstIdentity);

        clock.Advance(TimeSpan.FromTicks(1));
        var successor = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
        successor.DispatcherId.ShouldNotBe(firstIdentity);
        successor.Id.ShouldBe(source.Id);
        (await Complete(store, source.Id, firstIdentity, fenced, 0)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, source.Id, successor.DispatcherId!, fenced, 0)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, source.Id, firstIdentity, fenced, 0)).ShouldBe(OutboxCompletionOutcome.AlreadyTerminal);
    }

    [Fact]
    public async Task ValidateAuthorityBeforeMutationAndEvaluateValidFenceBeforeRowOutcomes()
    {
        using var store = Create(new CompletionClock());
        var strong = ((IServiceProvider)store).GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeAssignableTo<IFencedClaimScopedOutboxStore>();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await strong.MarkSentAsync("missing", default, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(async () => await store.MarkSentAsync("missing", " ", CancellationToken.None));
        (await store.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBeNull();
        (await strong.MarkSentAsync("missing", new OutboxWriteAuthority(8, "valid"), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
        (await strong.MarkSentAsync("missing", new OutboxWriteAuthority(7, "old"), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        var dead = ((IServiceProvider)store).GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IFencedDeadLetterableOutboxStore>();
        (await dead.MarkDeadLetteredAsync("missing", "reason", 0, new OutboxWriteAuthority(7, "old"), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await dead.MarkDeadLetteredAsync("missing", "reason", -1, new OutboxWriteAuthority(9, "valid"), CancellationToken.None));
        (await store.GetFencingHighWaterAsync(CancellationToken.None)).ShouldBe(8);
    }

    private static ValueTask<OutboxCompletionOutcome> Complete(InMemoryOutboxStore store, string id, string claim, bool fenced, int operation)
    {
        if (fenced)
        {
            var authority = new OutboxWriteAuthority(7, claim);
            var strong = ((IServiceProvider)store).GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeAssignableTo<IFencedClaimScopedOutboxStore>();
            return operation switch
            {
                0 => strong.MarkSentAsync(id, authority, CancellationToken.None),
                1 => strong.MarkFailedAsync(id, "failure", 2, null, authority, CancellationToken.None),
                2 => strong.MarkFailedAsync(id, "backoff", 2, DateTimeOffset.UnixEpoch.AddDays(1), authority, CancellationToken.None),
                _ => ((IServiceProvider)store).GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IFencedDeadLetterableOutboxStore>()
                    .MarkDeadLetteredAsync(id, "dead", 2, authority, CancellationToken.None),
            };
        }
        var plain = ((IServiceProvider)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
        return operation switch
        {
            0 => plain.MarkSentAsync(id, claim, CancellationToken.None),
            1 => plain.MarkFailedAsync(id, "failure", 2, claim, CancellationToken.None),
            2 => plain.MarkFailedWithBackoffAsync(id, "backoff", 2, DateTimeOffset.UnixEpoch.AddDays(1), claim, CancellationToken.None),
            _ => ((IServiceProvider)store).GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>()
                .MarkDeadLetteredAsync(id, "dead", 2, claim, CancellationToken.None),
        };
    }

    private static InMemoryOutboxStore Create(TimeProvider clock) => new(Options.Create(new InMemoryOutboxOptions
    {
        LeaseTimeoutSeconds = 2, FailureBackoffFloorSeconds = 1,
    }), NullLogger<InMemoryOutboxStore>.Instance, clock);

    private sealed class CompletionClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan amount) => _now += amount;
    }
}
