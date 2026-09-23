// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Transport;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Functional.Fencing;

/// <summary>
///     End-to-end arm for the <b>outbox leadership-fencing</b> guarantee: a superseded leader cannot
///     complete outbox work it no longer owns, and a drain that cannot present a token for an active
///     tenure refuses rather than draining unfenced.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Why an E2E arm exists for this at all.</strong> Fencing is a <em>composition</em> guarantee,
///     not a component one. Three independently-registered pieces have to line up: the store must answer the
///     <see cref="IFencedOutboxStore"/> capability probe, the container must supply an
///     <see cref="ILeaderProcessingGate"/>, and the drain must decide — per call — to present the tenure's
///     token instead of calling the unfenced members. A unit test over the store proves the store refuses a
///     stale token; it structurally cannot observe that the drain never presented one. That is the exact gap
///     this arm closes, and it is why the assertions below are made through
///     <see cref="IOutboxPublisher.PublishPendingMessagesAsync"/> — the consumer-facing drain — rather than
///     against the store directly.
///     </para>
///     <para>
///     <strong>Everything under test is the shipped composition, wired the way a consumer wires it:</strong>
///     <c>AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.UseInMemory()).AddLeaderElection(le =&gt; le.UseInMemory()))</c>.
///     The store is the shipped <c>InMemoryOutboxStore</c> (a real <see cref="IFencedOutboxStore"/> with a real
///     durable-in-process high-water mark); the gate is the shipped <c>LeaderElectionProcessingGate</c> over a
///     real <c>InMemoryLeaderElection</c>; the drain is the shipped <see cref="MessageBusOutboxPublisher"/>.
///     Two containers are composed independently and share one store instance and one election shared-state —
///     that is the two-node topology fencing exists for, expressed in-process so the arm is deterministic.
///     </para>
///     <para>
///     <strong>The only substituted pieces are the two a consumer supplies anyway:</strong> the transport
///     adapter (<see cref="RecordingMessageBusAdapter"/>, which records what was handed to a transport so the
///     arms can assert on delivery) and, in <see cref="RefuseAStaleTenureCompletingWorkItNoLongerOwns"/>, a
///     pinned gate standing in for a partitioned node whose lease has not yet expired locally. Neither is
///     framework code, and neither is what the assertions are about.
///     </para>
///     <para>
///     <strong>Non-vacuity.</strong> Each safety arm is paired with the liveness arm
///     <see cref="DrainAndMarkSentUnderAHeldLeadershipTenure"/>: if the drain refused everything the safety
///     arms would pass for the wrong reason, and the liveness arm would go red. The arms were additionally
///     proven RED by breaking fencing in place — see the bead's evidence.
///     </para>
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Core")]
[Trait("Feature", "OutboxLeaderFencing")]
public sealed class OutboxLeaderFencingE2EShould : FunctionalTestBase
{
    private const string Destination = "fencing-e2e";

    /// <summary>
    ///     LIVENESS. A node holding leadership drains its outbox end-to-end through the real
    ///     <see cref="IOutboxPublisher"/>: the message reaches the transport and the row is marked sent.
    /// </summary>
    /// <remarks>
    ///     This arm is what makes the two safety arms below non-vacuous. Fencing that refuses a superseded
    ///     leader is worthless if it also refuses the live one, and a test suite that only asserted refusals
    ///     would be green over a drain that had stopped working entirely.
    /// </remarks>
    [Fact]
    public async Task DrainAndMarkSentUnderAHeldLeadershipTenure()
    {
        await using var cluster = await FencedOutboxCluster.StartAsync().ConfigureAwait(false);

        // Node A holds leadership, so its gate carries a real minted fencing token.
        cluster.NodeA.Gate.ShouldProcess.ShouldBeTrue("node A was started first and must hold the tenure");
        var tokenA = cluster.NodeA.Gate.FencingToken;
        tokenA.ShouldNotBeNull("a held tenure must mint a fencing token; without one the drain cannot be fenced");

        var staged = await cluster.NodeA.Publisher
            .PublishAsync(new WidgetShipped { WidgetId = "w-1" }, Destination, null, TestCancellationToken)
            .ConfigureAwait(false);

        // Act — the consumer-facing drain. Internally this claims with node A's token and marks sent with it.
        var result = await cluster.NodeA.Publisher
            .PublishPendingMessagesAsync(TestCancellationToken)
            .ConfigureAwait(false);

        result.SuccessCount.ShouldBe(1, "the leader's drain must publish the one staged message");
        cluster.Transport.Published.ShouldContain(
            m => string.Equals(m, staged.Id, StringComparison.Ordinal),
            "the staged message must actually reach a transport, not merely be claimed");

        // Read the real store back (observation-only, never a claim): the row is Sent, which only a
        // mark-sent the store ACCEPTED produces.
        var afterDrain = await cluster.StatisticsAsync().ConfigureAwait(false);
        afterDrain.SentMessageCount.ShouldBe(1, "the store must record the row as sent, not merely claimed");
        afterDrain.StagedMessageCount.ShouldBe(0, "a completed drain leaves nothing awaiting delivery");
    }

    /// <summary>
    ///     SAFETY — the guarantee. Once leadership moves, the superseded node's drain must not publish. Its
    ///     tenure is over, so it can present no token, and the drain <b>refuses</b> with
    ///     <see cref="OutboxFencingTokenUnavailableException"/> rather than silently falling through to the
    ///     unfenced members.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The fall-through is the failure this arm exists to detect, and it is invisible from the store: a
    ///     drain that calls <c>IOutboxStore.GetUnsentMessagesAsync(batchSize, ct)</c> instead of the fenced
    ///     overload presents no token, so the store has nothing to refuse and happily hands over rows the
    ///     live leader owns. Every component behaves correctly; the composition does not.
    ///     </para>
    ///     <para>
    ///     Checking leadership and <em>then</em> draining would not close this either — that is check-then-act,
    ///     and a node paused past the end of its tenure resumes on a stale observation. The token presented at
    ///     the moment of the write is what makes the refusal sound, which is why this asserts a throw from the
    ///     drain rather than a <c>ShouldProcess == false</c> read.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task RefuseToDrainUnfencedAfterLeadershipMovesToAnotherNode()
    {
        await using var cluster = await FencedOutboxCluster.StartAsync().ConfigureAwait(false);

        _ = await cluster.NodeA.Publisher
            .PublishAsync(new WidgetShipped { WidgetId = "w-2" }, Destination, null, TestCancellationToken)
            .ConfigureAwait(false);

        // Leadership moves: node A steps down, node B is elected and mints a strictly higher token.
        await cluster.HandOverLeadershipToNodeBAsync().ConfigureAwait(false);

        cluster.NodeB.Gate.FencingToken.ShouldNotBeNull("node B now holds the tenure and must carry a token");
        cluster.NodeB.Gate.FencingToken!.Value.ShouldBeGreaterThan(
            cluster.TokenHeldByNodeABeforeHandover,
            "fencing tokens must be monotonic across a handover, or a superseded leader is indistinguishable from the live one");

        // Act + Assert — node A's drain refuses. It is still composed for a fenced drain (a leader election is
        // registered), but it has no tenure and therefore no token, so it must fail closed.
        var refused = await Should.ThrowAsync<OutboxFencingTokenUnavailableException>(
            async () => await cluster.NodeA.Publisher
                .PublishPendingMessagesAsync(TestCancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);

        refused.Message.ShouldNotBeNullOrWhiteSpace(
            "the refusal must say why it refused; a bare throw teaches the operator nothing");

        cluster.Transport.Published.ShouldBeEmpty(
            "a superseded node must publish NOTHING — the message is the live leader's to deliver");
        var afterRefusal = await cluster.StatisticsAsync().ConfigureAwait(false);
        afterRefusal.StagedMessageCount.ShouldBe(
            1, "the message must remain staged and claimable by the live leader — a refused drain must not " +
               "consume, lease or lose it");
        afterRefusal.SentMessageCount.ShouldBe(0, "nothing was delivered, so nothing may be recorded as sent");

        // LIVENESS half, in the same arm: the message is not stranded. The live leader drains it.
        var result = await cluster.NodeB.Publisher
            .PublishPendingMessagesAsync(TestCancellationToken)
            .ConfigureAwait(false);

        result.SuccessCount.ShouldBe(1, "the NEW leader must be able to drain what the old one could not");
        (await cluster.StatisticsAsync().ConfigureAwait(false)).SentMessageCount
            .ShouldBe(1, "refusing the superseded node must not strand the message");
    }

    /// <summary>
    ///     SAFETY — the harder half. A partitioned node whose lease has not yet expired locally still believes
    ///     it leads and still holds its old token. Its drain therefore <em>does</em> present a token — a stale
    ///     one — and the store must refuse it: the claim yields nothing, and a completion attempted against the
    ///     stale tenure is rejected with <see cref="StaleOutboxFencingTokenException"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This is the split-brain case the previous arm does not reach. There, node A had no token and the
    ///     drain refused itself; here the drain proceeds and the <em>store's</em> durable high-water mark is
    ///     what refuses, which is the only defence that holds when a node's own view of leadership is wrong.
    ///     Both halves are needed: the first proves the drain never falls through to the unfenced members, the
    ///     second proves the token it does present is actually compared against something.
    ///     </para>
    ///     <para>
    ///     The pinned gate is the one substitution, and it is the node's own (wrong) belief about its
    ///     leadership — which is exactly what cannot be produced by asking the real election, because the real
    ///     election is correct. Everything downstream of the gate is shipped code: the real publisher's
    ///     capability probe, the real fenced claim, the real high-water comparison.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task RefuseAStaleTenureCompletingWorkItNoLongerOwns()
    {
        await using var cluster = await FencedOutboxCluster.StartAsync().ConfigureAwait(false);
        var staleToken = cluster.NodeA.Gate.FencingToken!.Value;

        // A third node composed exactly like the others, except its gate is pinned to the tenure node A held
        // before the handover — a partitioned node that has not noticed it was superseded.
        await using var partitioned = await cluster
            .AddPartitionedNodeAsync(pinnedToken: staleToken)
            .ConfigureAwait(false);

        // Leadership moves, and the live leader drains once. Its claim carries the NEW token, which advances
        // the store's high-water mark past the stale one.
        await cluster.HandOverLeadershipToNodeBAsync().ConfigureAwait(false);
        _ = await cluster.NodeB.Publisher
            .PublishAsync(new WidgetShipped { WidgetId = "w-3" }, Destination, null, TestCancellationToken)
            .ConfigureAwait(false);
        var liveResult = await cluster.NodeB.Publisher
            .PublishPendingMessagesAsync(TestCancellationToken)
            .ConfigureAwait(false);
        liveResult.SuccessCount.ShouldBe(1, "precondition: the live leader claims and publishes its message");
        cluster.Transport.Published.Count.ShouldBe(1, "precondition: exactly one delivery so far — the live leader's");

        // A message the partitioned node would be perfectly able to publish if nothing refused it. This is
        // what makes the assertion below about FENCING rather than about an empty outbox: the row is staged,
        // unclaimed, due, and would be handed to any drain that asked without presenting a stale token.
        var available = await cluster.NodeB.Publisher
            .PublishAsync(new WidgetShipped { WidgetId = "w-4" }, Destination, null, TestCancellationToken)
            .ConfigureAwait(false);
        (await cluster.StatisticsAsync().ConfigureAwait(false)).StagedMessageCount.ShouldBe(
            1, "precondition: a genuinely claimable message is waiting");

        // Assert SAFETY (store-level) — a completion forced against the stale tenure is rejected outright
        // rather than silently ignored. Silent acceptance is worse than a throw: the row would be marked sent
        // by a node that never delivered it.
        var fenced = cluster.Store.GetService(typeof(IFencedOutboxStore)) as IFencedOutboxStore;
        fenced.ShouldNotBeNull("the shipped in-memory outbox store must answer the fenced capability probe");

        _ = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
            async () => await fenced!
                .MarkSentAsync(available.Id, staleToken, TestCancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);

        // Act — the partitioned node drains, presenting its stale token, with a claimable row in front of it.
        var partitionedResult = await partitioned.Publisher
            .PublishPendingMessagesAsync(TestCancellationToken)
            .ConfigureAwait(false);

        // Assert SAFETY (end-to-end) — the stale claim yields nothing and nothing reaches a transport. This
        // is the assertion that goes red if the drain ever stops presenting its token: an unfenced claim
        // would take this row, publish it, and the live leader would publish it again.
        partitionedResult.SuccessCount.ShouldBe(
            0, "a superseded tenure must claim nothing, even when a claimable row is available");
        cluster.Transport.Published.Count.ShouldBe(
            1, "a superseded node must hand NOTHING to a transport; the second delivery is the duplicate " +
               "fencing exists to prevent");

        // LIVENESS — the refused row is not stranded: the live leader still drains it.
        var recovered = await cluster.NodeB.Publisher
            .PublishPendingMessagesAsync(TestCancellationToken)
            .ConfigureAwait(false);
        recovered.SuccessCount.ShouldBe(1, "the live leader must still be able to drain the refused row");
        cluster.Transport.Published.Count.ShouldBe(2, "both messages are delivered exactly once, by the live leader");
    }
}
