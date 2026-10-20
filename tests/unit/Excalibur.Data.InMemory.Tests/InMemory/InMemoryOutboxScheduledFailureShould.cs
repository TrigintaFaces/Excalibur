// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryOutboxScheduledFailureShould
{
    [Fact]
    public async Task Expose_And_Apply_Backoff_Through_The_Telemetry_Decorator()
    {
        using var store = CreateStore();
        using var decorated = new Excalibur.Outbox.Diagnostics.TelemetryOutboxStoreDecorator(store);
        var capability = decorated.GetService(typeof(IBackoffSchedulableOutboxStore));
        capability.ShouldNotBeNull();
        var message = new OutboundMessage("test.message", [1], "destination");
        await decorated.StageMessageAsync(message, CancellationToken.None);
        await ((IBackoffSchedulableOutboxStore)capability).MarkFailedWithBackoffAsync(message.Id, "retry", 1, DateTimeOffset.UtcNow, CancellationToken.None);
        message.Status.ShouldBe(OutboxStatus.Failed);
        (await decorated.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Leave_Refused_Scheduled_Completions_Unchanged_While_Live_Owner_Can_Complete(bool staleTenure)
    {
        using var store = CreateStore();
        var message = new OutboundMessage("test.message", [1], "destination");
        await store.StageMessageAsync(message, CancellationToken.None);
        var claimed = (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).Single();
        var before = (message.Status, message.RetryCount, message.LastError, message.LastAttemptAt);
        var authority = new OutboxWriteAuthority(staleTenure ? 5 : 10, staleTenure ? claimed.DispatcherId! : "foreign-claim");
        (await store.MarkFailedAsync(message.Id, "stale", 7, DateTimeOffset.UtcNow.AddDays(1), authority, CancellationToken.None))
            .ShouldBe(staleTenure ? OutboxCompletionOutcome.FenceRefused : OutboxCompletionOutcome.ClaimLost);
        (message.Status, message.RetryCount, message.LastError, message.LastAttemptAt).ShouldBe(before);
        await store.MarkSentAsync(message.Id, 10, CancellationToken.None);
        message.Status.ShouldBe(OutboxStatus.Sent);
    }

    [Fact]
    public async Task Keep_Sent_Terminal_When_Scheduled_Failure_And_Send_Race()
    {
        using var store = CreateStore();
        for (var index = 0; index < 100; index++)
        {
            var message = new OutboundMessage("test.message", [1], "destination");
            await store.StageMessageAsync(message, CancellationToken.None);
            await Task.WhenAll(
                Task.Run(async () => await store.MarkSentAsync(message.Id, CancellationToken.None)),
                Task.Run(async () => await store.MarkFailedWithBackoffAsync(message.Id, "late", 1, DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None)));
            message.Status.ShouldBe(OutboxStatus.Sent);
        }
        (await store.GetUnsentMessagesAsync(100, CancellationToken.None)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Honor_Later_Schedules_And_Eventually_Allow_Retry(bool fenced)
    {
        using var store = new InMemoryOutboxStore(
            Options.Create(new InMemoryOutboxOptions { FailureBackoffFloorSeconds = 1 }),
            NullLogger<InMemoryOutboxStore>.Instance);
        var message = new OutboundMessage("test.message", [1], "destination");
        await store.StageMessageAsync(message, CancellationToken.None);
        var claimed = (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).Single();
        var requested = DateTimeOffset.UtcNow.AddSeconds(3);
        if (fenced)
        {
            (await store.MarkFailedAsync(message.Id, "retry", 2, requested,
                new OutboxWriteAuthority(10, claimed.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        else
        {
            var capability = ((IOutboxStore)store).GetService(typeof(IBackoffSchedulableOutboxStore));
            capability.ShouldNotBeNull();
            await ((IBackoffSchedulableOutboxStore)capability).MarkFailedWithBackoffAsync(message.Id, "retry", 2, requested, CancellationToken.None);
        }
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).ShouldBeEmpty(
            "the caller's later schedule must still block retry after the shorter store floor elapsed");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        OutboundMessage[] retry;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
            retry = (await store.GetUnsentMessagesAsync(1, 10, deadline.Token)).ToArray();
        } while (retry.Length == 0);
        retry.Single().Id.ShouldBe(message.Id);
        retry.Single().RetryCount.ShouldBe(2);
        DateTimeOffset.UtcNow.ShouldBeGreaterThanOrEqualTo(requested);
        await store.MarkSentAsync(message.Id, 10, CancellationToken.None);
        (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Never_Reopen_A_Terminal_Message_With_Scheduled_Failure(bool deadLetter)
    {
        using var store = CreateStore();
        var message = new OutboundMessage("test.message", [1], "destination");
        await store.StageMessageAsync(message, CancellationToken.None);
        if (deadLetter) await store.MarkDeadLetteredAsync(message.Id, "terminal", 0, CancellationToken.None);
        else await store.MarkSentAsync(message.Id, CancellationToken.None);
        var before = message.Status;
        var capability = ((IOutboxStore)store).GetService(typeof(IBackoffSchedulableOutboxStore));
        capability.ShouldNotBeNull();
        await ((IBackoffSchedulableOutboxStore)capability).MarkFailedWithBackoffAsync(message.Id, "late", 7, DateTimeOffset.UtcNow, CancellationToken.None);
        message.Status.ShouldBe(before);
        message.RetryCount.ShouldBe(0);
        (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    public async Task Keep_The_Failure_Floor_On_The_Unfenced_Capability(int requestedSeconds)
    {
        using var store = CreateStore();
        var message = new OutboundMessage("test.message", [1], "destination");
        await store.StageMessageAsync(message, CancellationToken.None);
        var capability = ((IOutboxStore)store).GetService(typeof(IBackoffSchedulableOutboxStore));
        capability.ShouldNotBeNull();
        await ((IBackoffSchedulableOutboxStore)capability).MarkFailedWithBackoffAsync(message.Id, "retry", 4,
            DateTimeOffset.UtcNow.AddSeconds(requestedSeconds), CancellationToken.None);
        await ((IBackoffSchedulableOutboxStore)capability).MarkFailedWithBackoffAsync(message.Id, "late lower count", 1,
            DateTimeOffset.UtcNow.AddSeconds(requestedSeconds), CancellationToken.None);
        message.RetryCount.ShouldBe(4);
        (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public void Expose_Backoff_Capability_Through_The_Actual_Store_Contract()
    {
        using var store = CreateStore();
        var capability = ((IOutboxStore)store).GetService(typeof(IBackoffSchedulableOutboxStore));
        capability.ShouldNotBeNull();
        capability.ShouldBeAssignableTo<IBackoffSchedulableOutboxStore>();
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    public async Task Keep_The_Failure_Floor_When_A_Fenced_Caller_Requests_An_Earlier_Retry(int requestedSeconds)
    {
        using var store = CreateStore();
        var message = new OutboundMessage("test.message", [1], "destination");
        await store.StageMessageAsync(message, CancellationToken.None);
        var claimed = (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).Single();
        var result = await store.MarkFailedAsync(message.Id, "retry", 1,
            DateTimeOffset.UtcNow.AddSeconds(requestedSeconds), new OutboxWriteAuthority(10, claimed.DispatcherId!), CancellationToken.None);
        result.ShouldBe(OutboxCompletionOutcome.Applied);
        (await store.GetUnsentMessagesAsync(1, 10, CancellationToken.None)).ShouldBeEmpty();
        var failed = (await store.GetAllTenantsFailedMessagesAsync(10, null, 10, CancellationToken.None)).Single();
        failed.Id.ShouldBe(message.Id);
        failed.RetryCount.ShouldBe(1);
    }

    private static InMemoryOutboxStore CreateStore() => new(
        Options.Create(new InMemoryOutboxOptions { FailureBackoffFloorSeconds = 30 }),
        NullLogger<InMemoryOutboxStore>.Instance);
}
