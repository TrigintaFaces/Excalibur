// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Dispatch;
using Excalibur.Outbox.ElasticSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(ElasticsearchOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Outbox")]
[Trait("Database", "Elasticsearch")]
public sealed class ElasticsearchOutboxStrongCompletionShould(ElasticsearchOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RefuseOldAndExpiredClaimsWhileCurrentOwnerProgresses(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("claim scripts and CAS must run against the actual server, never skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var index = "strong-claim-" + Guid.NewGuid().ToString("N");
        await using var store = new ElasticsearchOutboxStore(fixture.Client,
            Options.Create(new ElasticsearchOutboxOptions
            {
                IndexName = index, ProcessorId = "same-process", LeaseTimeoutSeconds = 5,
                FailureBackoffFloorSeconds = 1, RefreshPolicy = ElasticsearchRefreshPolicy.Immediate,
            }), NullLogger<ElasticsearchOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        var first = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        first.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        // delay-ok: the actual node-clock lease must expire before an unreclaimed report or reclaim is tested.
        await Task.Delay(TimeSpan.FromMilliseconds(5200), deadline.Token); // delay-ok: The real Elasticsearch node-clock lease must expire; stale/current claim outcomes independently verify expiry and reclaim.
        (await Complete(store, row.Id, first.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var current = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        current.DispatcherId.ShouldNotBe(first.DispatcherId);
        (await Complete(store, row.Id, first.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, row.Id, current.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, row.Id, current.DispatcherId!, operation, deadline.Token)).ShouldBe(operation is 0 or 3
            ? OutboxCompletionOutcome.AlreadyTerminal : OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, "missing", "known", operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
    }

    [Fact]
    public async Task AdmitOnlyOneCompetingCompletionForTheSameAcquisition()
    {
        fixture.DockerAvailable.ShouldBeTrue("competing claim completions require real optimistic concurrency, never skipped");
        var index = "strong-race-" + Guid.NewGuid().ToString("N");
        await using var store = new ElasticsearchOutboxStore(fixture.Client,
            Options.Create(new ElasticsearchOutboxOptions { IndexName = index, ProcessorId = "owner", LeaseTimeoutSeconds = 120,
                RefreshPolicy = ElasticsearchRefreshPolicy.Immediate }), NullLogger<ElasticsearchOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(row, CancellationToken.None);
        var claim = (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        var attempts = Enumerable.Range(0, 4).Select(_ => Complete(store, row.Id, claim.DispatcherId!, 0, CancellationToken.None).AsTask()).ToArray();
        var outcomes = await Task.WhenAll(attempts);
        outcomes.Count(value => value is OutboxCompletionOutcome.Applied).ShouldBe(1);
        outcomes.Count(value => value is OutboxCompletionOutcome.AlreadyTerminal).ShouldBe(3);
    }

    private static ValueTask<OutboxCompletionOutcome> Complete(ElasticsearchOutboxStore store, string id, string claim, int operation, CancellationToken token)
    {
        var strong = ((IServiceProvider)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
        return operation switch
        {
            0 => strong.MarkSentAsync(id, claim, token),
            1 => strong.MarkFailedAsync(id, "failure", 2, claim, token),
            2 => strong.MarkFailedWithBackoffAsync(id, "backoff", 2, DateTimeOffset.UtcNow.AddMinutes(1), claim, token),
            _ => ((IServiceProvider)store).GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>()
                .MarkDeadLetteredAsync(id, "dead", 2, claim, token),
        };
    }
}
