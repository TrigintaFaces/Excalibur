// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class OutboxAuthorityScenarios
{
    internal static async Task SharedScopesAndNewerTenureAsync(OutboxAuthorityComposition composition, CancellationToken token)
    {
        var first = await composition.StageAsync("first-refused", priority: 0, token).ConfigureAwait(false);
        var remaining = await composition.StageAsync("must-not-publish", priority: 1, token).ConfigureAwait(false);
        composition.Adapter.OnPublish = async (context, cancellationToken) =>
        {
            OutboxAuthoritySmoke.Require(context.MessageId == first.Id, "the lower-priority row must be first");
            await AdvanceActualStoreAsync(composition, cancellationToken).ConfigureAwait(false);
        };

        ILeaderProcessingGate firstGate;
        IOutboxPublisher firstPublisher;
        await using (var scope = composition.Services.CreateAsyncScope())
        {
            firstGate = scope.ServiceProvider.GetRequiredService<ILeaderProcessingGate>();
            firstPublisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
            var refused = await firstPublisher.PublishPendingMessagesAsync(token).ConfigureAwait(false);
            RequireRefusedAcceptance(refused);
            OutboxAuthoritySmoke.Require(refused.SkippedCount == 1, "the second claimed row must remain unattempted");
        }

        await RequireStoredCountsAsync(composition, staged: 2, sent: 0, token).ConfigureAwait(false);
        var fresh = await composition.StageAsync("new-tenure", priority: 2, token).ConfigureAwait(false);
        await using (var scope = composition.Services.CreateAsyncScope())
        {
            var gate = scope.ServiceProvider.GetRequiredService<ILeaderProcessingGate>();
            var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
            OutboxAuthoritySmoke.Require(!ReferenceEquals(gate, firstGate) && !ReferenceEquals(publisher, firstPublisher),
                "the new scope must actually recreate gate and publisher");
            OutboxAuthoritySmoke.Require(ReferenceEquals(((ILeaderProcessingAuthorityDomain)gate).AuthorityDomain,
                ((ILeaderProcessingAuthorityDomain)firstGate).AuthorityDomain), "recreated wrappers must retain one authority identity");
            var blocked = await publisher.PublishScheduledMessagesAsync(token).ConfigureAwait(false);
            OutboxAuthoritySmoke.Require(blocked.IsTenureRefused && !blocked.IsSuccess
                && blocked.SuccessCount == 0 && blocked.FailureCount == 0, "the same refused token must not resume through a new scope");
            OutboxAuthoritySmoke.Require(composition.Adapter.Accepted.Length == 1,
                "a newly staged row must not publish under refused authority");
            await RequireStoredCountsAsync(composition, staged: 3, sent: 0, token).ConfigureAwait(false);
        }

        Volatile.Write(ref composition.Owner.Token, 8);
        composition.Adapter.OnPublish = null;
        await using (var scope = composition.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
            var resumed = await publisher.RetryFailedMessagesAsync(3, token).ConfigureAwait(false);
            OutboxAuthoritySmoke.Require(resumed.Outcome == PublishingOutcome.Completed && resumed.IsSuccess
                && resumed.SuccessCount == 1 && resumed.FailureCount == 0, "a genuinely newer token must publish fresh work");
        }
        var accepted = composition.Adapter.Accepted;
        OutboxAuthoritySmoke.Require(accepted.Length == 2 && accepted[0] == "first-refused" && accepted[1] == "new-tenure",
            "only the accepted old send and the fresh newer-tenure row may reach the recording endpoint");
        await RequireStoredCountsAsync(composition, staged: 2, sent: 1, token).ConfigureAwait(false);
        OutboxAuthoritySmoke.Require(fresh.Status == OutboxStatus.Staged && remaining.Status == OutboxStatus.Staged,
            "producer snapshots stay detached from stored completion; they are not persisted-state queries");
    }

    internal static async Task IndependentDomainAsync(CancellationToken token)
    {
        await using var independent = await OutboxAuthorityComposition.CreateAsync().ConfigureAwait(false);
        await independent.StageAsync("independent-token-seven", priority: 0, token).ConfigureAwait(false);
        await using var scope = independent.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
        var result = await publisher.PublishPendingMessagesAsync(token).ConfigureAwait(false);
        OutboxAuthoritySmoke.Require(result.Outcome == PublishingOutcome.Completed && result.IsSuccess && result.SuccessCount == 1,
            "a distinct live authority domain with the same numeric token must not be poisoned");
        OutboxAuthoritySmoke.Require(await independent.Diagnostics.GetFencingHighWaterAsync(token).ConfigureAwait(false) == 7,
            "the independent store must record its own token-seven claim");
    }

    internal static async Task CapturedAuthorityAsync(bool failTransport, CancellationToken token)
    {
        await using var composition = await OutboxAuthorityComposition.CreateAsync().ConfigureAwait(false);
        _ = await composition.StageAsync(failTransport ? "failure-handover" : "completion-handover", priority: 0, token)
            .ConfigureAwait(false);
        composition.Adapter.OnPublish = async (_, cancellationToken) =>
        {
            await AdvanceActualStoreAsync(composition, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref composition.Owner.Token, 8); // A later live token must not relabel this old claim.
            if (failTransport) { throw new IOException("arranged recording endpoint failure after handover"); }
        };

        await using var scope = composition.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
        var result = await publisher.PublishPendingMessagesAsync(token).ConfigureAwait(false);
        OutboxAuthoritySmoke.Require(result.IsTenureRefused && !result.IsSuccess,
            "old claim completion/failure must still present token seven despite a live token-eight gate");
        OutboxAuthoritySmoke.Require(result.SuccessCount == (failTransport ? 0 : 1)
            && result.FailureCount == (failTransport ? 1 : 0), "only actual transport outcomes belong in counters");
        await RequireStoredCountsAsync(composition, staged: 1, sent: 0, token).ConfigureAwait(false);
        OutboxAuthoritySmoke.Require(composition.Adapter.Accepted.Length == (failTransport ? 0 : 1),
            "the recording endpoint must distinguish accepted publication from an actual send failure");
    }

    private static async Task RequireStoredCountsAsync(OutboxAuthorityComposition composition, int staged, int sent,
        CancellationToken token)
    {
        var admin = composition.Store.GetService(typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin
            ?? throw new InvalidOperationException("The real store must expose its public statistics capability.");
        var state = await admin.GetAllTenantsStatisticsAsync(token).ConfigureAwait(false);
        // The isolated population plus accepted identities above proves observable settlement, without
        // treating detached producer objects as live storage or claiming a per-row terminal read API.
        OutboxAuthoritySmoke.Require(state.StagedMessageCount == staged && state.SentMessageCount == sent
            && state.FailedMessageCount == 0 && state.SendingMessageCount == 0 && state.ScheduledMessageCount == 0
            && state.TotalMessageCount == staged + sent, "actual stored population must match accepted and refused work");
    }

    private static async Task AdvanceActualStoreAsync(OutboxAuthorityComposition composition, CancellationToken token)
    {
        // All staged rows are held by the current batch. A real newer-tenure claim records its fence
        // atomically even though no row is currently claimable. This does not reset or shorten a lease.
        var newer = await composition.FencedStore.GetUnsentMessagesAsync(1, 8, token).ConfigureAwait(false);
        OutboxAuthoritySmoke.Require(!newer.Any(), "the higher-tenure observation must not reclaim an unexpired lease");
        OutboxAuthoritySmoke.Require(await composition.Diagnostics.GetFencingHighWaterAsync(token).ConfigureAwait(false) == 8,
            "the real store must have advanced the high-water before the old completion");
    }

    private static void RequireRefusedAcceptance(PublishingResult result) => OutboxAuthoritySmoke.Require(
        result.Outcome == PublishingOutcome.TenureRefused && !result.IsSuccess && result.SuccessCount == 1
        && result.FailureCount == 0, "an accepted send followed by a real fenced refusal must remain counted and stand down");
}
