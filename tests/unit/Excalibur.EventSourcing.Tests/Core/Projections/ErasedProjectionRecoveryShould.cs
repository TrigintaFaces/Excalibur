// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Projections;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Recovering an erased subject actually rewrites the projection row, in both erasure shapes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why position comparisons cannot decide this.</b> The positioned-write contract assumes projection
/// state is a function of position. Erasure mutates events IN PLACE, so <c>fold()</c> changes while
/// every position stays fixed — and across an erasure no position comparison carries any information
/// about whether a row's state is correct. Both arms below are that sentence, made executable.
/// </para>
/// <para>
/// <b>The fixture enforces BOTH conjuncts of the real contract</b> — <c>stored == expected</c> AND
/// <c>new &gt; stored</c> — because the defect is produced by the second one holding the caller to a
/// rule that erasure makes meaningless. A fake that admitted every write could not exhibit either arm.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedProjectionRecoveryShould
{
    private const string AggregateId = "agg-subject";
    private const string AggregateType = "Agg";

    /// <summary>
    /// A PARTIALLY erased aggregate whose projection is caught up must still be rewritten.
    /// </summary>
    /// <remarks>
    /// The common case, and the one that reports success while doing nothing. The projection is caught
    /// up, so the stored position already equals the highest surviving event's position; the write is
    /// refused as non-advancing, the refusal reads as settled, and recovery logs success. The erased
    /// subject's data is still in the row.
    /// </remarks>
    [Fact]
    public async Task Rewrite_a_caught_up_row_after_a_partial_erasure()
    {
        var store = new ConjunctEnforcingStore();

        // The row as the live apply path left it: both events folded, position at the last one.
        store.Seed(AggregateId, new Tally { Total = 2, Trace = "e1+e2" }, position: 2);

        // e1 is now a tombstone; e2 survives at position 2. The replay folds only e2.
        var sut = BuildService(store, Tombstone(1), Live(2));

        await sut.ReapplyAsync<Tally>(AggregateId, AggregateType, TestContext.Current.CancellationToken);

        store.Get(AggregateId)!.Total.ShouldBe(
            1,
            "only the surviving event may remain folded. A stored Total of 2 means the erased event's "
            + "contribution is still in the read model and the recovery did nothing");
        store.Get(AggregateId)!.Trace.Contains("e1", StringComparison.Ordinal).ShouldBeFalse(
            "the erased event must leave no trace in the persisted projection");
    }


    /// <summary>
    /// SAFETY and COST. A recovery against an unplaceable row fails on the FIRST attempt, not after
    /// MaxRecoveryAttempts full stream replays, and says the true thing about why.
    /// </summary>
    /// <returns>A task representing the arm.</returns>
    /// <remarks>
    /// <para>
    /// The framework's own documented remedy used to be a dead end that lied about the reason.
    /// InlineProjectionProcessor tells a consumer whose inline projection failed to call
    /// IProjectionRecovery.ReapplyAsync. Inline mode on a positioned store writes unconditionally, so the
    /// row is unplaceable; every attempt replayed the entire stream, was refused, and looped; and after
    /// the last one the thrown message blamed "another writer" that did not exist and advised retrying
    /// when the processor is quiet, which can never succeed.
    /// </para>
    /// <para>
    /// <b>The ATTEMPT COUNT is the assertion that matters.</b> An arm checking only the message would pass
    /// a version that still burned five replays before wording the failure correctly, so the cost is bound
    /// directly: exactly one advancing write, therefore exactly one replay.
    /// </para>
    /// <para>
    /// RED when the terminal check is removed: AdvanceAttempts reaches MaxRecoveryAttempts (5) and the
    /// message reverts to blaming a competing writer.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Fail_on_the_first_attempt_when_the_row_carries_no_placeable_position()
    {
        var store = new ConjunctEnforcingStore();

        // The state inline mode leaves behind: written unconditionally, so the row records that it is not
        // a fold over any prefix.
        store.Seed(AggregateId, new Tally { Total = 7, Trace = "inline" }, ProjectionPosition.UnplaceableSentinel);

        var sut = BuildService(store, Live(1), Live(2));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ReapplyAsync<Tally>(AggregateId, AggregateType, TestContext.Current.CancellationToken));

        store.AdvanceAttempts.ShouldBe(
            1,
            "recovery must fail on the FIRST refusal. Each attempt replays the whole stream to reach an "
            + "answer that cannot change, so looping costs MaxRecoveryAttempts full replays and reaches "
            + "the same terminal refusal");

        thrown.Message.ShouldContain(
            "RebuildAtPositionAsync",
            Case.Sensitive,
            "the message must name the remedy that can actually succeed");
        thrown.Message.ShouldNotContain(
            "another writer",
            Case.Sensitive,
            "nothing was competing for this row, and blaming a writer that does not exist sends an "
            + "operator looking for a bug in their own code while the framework produced the state");

        store.Read(AggregateId).State!.Total.ShouldBe(
            7,
            "a reported refusal must not have written. Recovery REPORTS here rather than repairing: the "
            + "fold it holds is this aggregate's, and the row may legitimately hold another aggregate's "
            + "contribution through the override hatch, so rebuilding with this state could lose it");
    }

    /// <summary>
    /// A FULLY erased aggregate keeps its position and loses its state.
    /// </summary>
    /// <remarks>
    /// The position is honest — the row has folded everything up to it, and everything up to it is now
    /// nothing. Clearing the position instead leaves the row with no number, which no positioned write
    /// can advance from: the next batch is refused and the projection stalls until it is rebuilt.
    /// </remarks>
    [Fact]
    public async Task Keep_the_position_and_empty_the_state_after_a_full_erasure()
    {
        var store = new ConjunctEnforcingStore();
        store.Seed(AggregateId, new Tally { Total = 2, Trace = "e1+e2" }, position: 2);

        var sut = BuildService(store, Tombstone(1), Tombstone(2));

        await sut.ReapplyAsync<Tally>(AggregateId, AggregateType, TestContext.Current.CancellationToken);

        var (state, position) = store.Read(AggregateId);

        state.ShouldNotBeNull("the row is not deleted -- deleting removes the self-heal a concurrent writer relies on");
        state!.Total.ShouldBe(0, "every event is a tombstone, so the true fold is empty");
        position.ShouldBe(
            2,
            "the position is HONEST and must survive: the row has folded everything up to 2, and "
            + "everything up to 2 is now nothing. Clearing it leaves the row unadvanceable");
    }

    /// <summary>
    /// SAFETY. A fully erased aggregate is numbered from its tombstones, not left numberless.
    /// </summary>
    /// <returns>A task representing the arm.</returns>
    /// <remarks>
    /// <para>
    /// This closes the largest producer of the unsound shape, and erasure was that producer. A tombstone
    /// loses its PAYLOAD, not its position -- it is a stored event with a real global position. Taking the
    /// highest position over FOLDED events only meant a fully erased aggregate, where every event is a
    /// tombstone and nothing folds, produced NO position at all; recovery then wrote a row carrying no
    /// number, which no positioned write can advance from -- so the live apply path is refused on every
    /// subsequent batch and the projection stalls until someone rebuilds it.
    /// </para>
    /// <para>
    /// The number is taken over the events THIS replay examined, never from a second observation such as the
    /// stream head. A head read and a replay are two unordered observations: read the head first and a
    /// concurrent append makes the state a superset of the claim, read it after and a missed append makes it
    /// a subset -- both false. A maximum over the set actually read is true either way, because the set and
    /// the number are one observation.
    /// </para>
    /// <para>
    /// RED if the maximum is taken after the tombstone skip rather than before it: the row stays at the
    /// unnumbered sentinel.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Number_the_row_from_the_tombstones_when_every_event_is_erased()
    {
        // NO row seeded, deliberately. This arm is about the NUMBER recovery writes, not about what a
        // store does with an existing numberless row -- every provider now refuses that, and the
        // conformance kit is where real stores answer for it.
        var store = new ConjunctEnforcingStore();

        var sut = BuildService(store, Tombstone(4), Tombstone(7));

        await sut.ReapplyAsync<Tally>(AggregateId, AggregateType, TestContext.Current.CancellationToken);

        var (state, position) = store.Read(AggregateId);

        state.ShouldNotBeNull("the row is not deleted; deleting removes the self-heal a concurrent writer relies on");
        state!.Total.ShouldBe(0, "every event is a tombstone, so the true fold is empty");

        position.ShouldBe(
            7,
            "the row must end NUMBERED at the highest position this replay examined. Its state is the fold "
            + "over every event feeding this projection at or below 7, and those events fold to nothing, "
            + "which is exactly what an erased state is. Leaving it at the unnumbered sentinel is what "
            + "makes the next batch refusable rather than applicable");

        position.ShouldNotBe(
            ProjectionPosition.UnnumberedSentinel,
            "a row carrying no number cannot be advanced from, so leaving one here stalls the projection "
            + "until someone rebuilds it");
        position.ShouldNotBeNull("a numberless row is exactly what this change exists to stop producing");
    }

    /// <summary>
    /// SAFETY. An aggregate with no events is not recovered by overwriting the row with the initial state.
    /// </summary>
    /// <returns>A task representing the arm.</returns>
    /// <remarks>
    /// <para>
    /// The projection id is the aggregate id, but that does not make this aggregate the row's only
    /// contributor: a handler can redirect into this id at runtime through the override hatch, which
    /// recovery cannot detect before the handler runs — the service says so itself. So a row keyed by an
    /// aggregate with no events can legitimately hold ANOTHER aggregate's fold.
    /// </para>
    /// <para>
    /// Both paths corrupted it. With the row still present, recovery took the re-fold path and rewrote it
    /// to the initial state at the position it already held, so the row then asserted a fold it did not
    /// contain — reachable with no erasure and no crash. With the row deleted, recovery wrote a state
    /// carrying no position, which no later writer can advance from: the projection stalls until it is
    /// rebuilt, having silently lost the other aggregate's fold.
    /// </para>
    /// <para>
    /// RED if the zero-event guard is removed: the seeded fold is replaced by the initial state while the
    /// position stays at 4, which is precisely the false claim.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Leave_the_row_untouched_when_the_aggregate_has_no_events()
    {
        var store = new ConjunctEnforcingStore();

        // A fold contributed by a DIFFERENT aggregate, redirected into this key by the override hatch.
        // Recovery of this aggregate has no business replacing it.
        store.Seed(AggregateId, new Tally { Total = 9, Trace = "folded-from-another-aggregate" }, position: 4);

        var sut = BuildService(store);   // no events at all for this aggregate

        await sut.ReapplyAsync<Tally>(AggregateId, AggregateType, TestContext.Current.CancellationToken);

        var (state, position) = store.Read(AggregateId);

        state.ShouldNotBeNull("the row must survive: recovery had nothing to contribute");
        state!.Total.ShouldBe(
            9,
            "an aggregate with no events has no history to fold, so replacing the row with the initial "
            + "state is not a repair -- it discards a contribution this call never owned");
        state.Trace.ShouldBe(
            "folded-from-another-aggregate",
            "the surviving state must be the one that was there, not a fresh seed wearing its position");
        position.ShouldBe(
            4,
            "and the position is untouched, so the row does not end up asserting a fold it no longer holds");
    }

    private static StoredEvent Tombstone(long position) =>
        new($"t{position}", AggregateId, AggregateType, ErasedEventMarker.EventType,
            null, null, position, DateTimeOffset.UnixEpoch)
        { GlobalPosition = position };

    private static StoredEvent Live(long position) =>
        new($"e{position}", AggregateId, AggregateType, "Counted",
            [(byte)position], null, position, DateTimeOffset.UnixEpoch)
        { GlobalPosition = position };

    private static ProjectionRecoveryService BuildService(ConjunctEnforcingStore store, params StoredEvent[] events)
    {
        var projection = new MultiStreamProjection<Tally>();
        projection.AddHandler<Counted>((p, e) => { p.Total++; p.Trace = p.Trace.Length == 0 ? e.Tag : p.Trace + "+" + e.Tag; });

        var registry = new StubRegistry(
            new ProjectionRegistration(typeof(Tally), ProjectionMode.Async, projection, inlineApply: null));

        var eventStore = A.Fake<IEventStore>();
        A.CallTo(() => eventStore.LoadAsync(AggregateId, AggregateType, A<CancellationToken>._))
            .Returns(new ValueTask<IReadOnlyList<StoredEvent>>(events.ToList()));

        var serializer = A.Fake<IEventSerializer>();
        A.CallTo(() => serializer.ResolveType("Counted")).Returns(typeof(Counted));
        A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(Counted)))
            .ReturnsLazily(call => (IDomainEvent)new Counted { Tag = "e" + call.Arguments.Get<byte[]>(0)![0] });

        var services = new ServiceCollection();
        _ = services.AddSingleton<IProjectionStore<Tally>>(store);

        return new ProjectionRecoveryService(
            registry, eventStore, serializer, services.BuildServiceProvider(),
            NullLogger<ProjectionRecoveryService>.Instance);
    }

    private sealed class Tally
    {
        public int Total { get; set; }

        public string Trace { get; set; } = string.Empty;
    }

    [MessageName("Test.ErasedProjectionRecovery.Counted")]
    private sealed record Counted : DomainEvent
    {
        public string Tag { get; init; } = string.Empty;
    }

    private sealed class StubRegistry(ProjectionRegistration registration) : IProjectionRegistry
    {
        private readonly List<ProjectionRegistration> _registrations = [registration];

        public ProjectionRegistration? GetRegistration(Type projectionType) => _registrations[0];

        public IReadOnlyList<ProjectionRegistration> GetAll() => _registrations;

        public IReadOnlyList<ProjectionRegistration> GetByMode(ProjectionMode mode) => _registrations;

        public void Register(ProjectionRegistration r) => _registrations.Add(r);
    }

    /// <summary>
    /// Enforces BOTH conjuncts of the positioned-write contract, which is what makes these arms real.
    /// </summary>
    private sealed class ConjunctEnforcingStore : IPositionedProjectionStore<Tally>
    {
        private readonly Dictionary<string, (Tally State, long? Position)> _rows = new(StringComparer.Ordinal);

        public void Seed(string id, Tally state, long? position) => _rows[id] = (state, position);

        public Tally? Get(string id) => _rows.TryGetValue(id, out var r) ? r.State : null;

        public (Tally? State, long? Position) Read(string id) =>
            _rows.TryGetValue(id, out var r) ? (r.State, r.Position) : (null, null);

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IPositionedProjectionStore<Tally>) ? this : null;

                // The unnumbered write: a complete fold whose prefix has no global position number.
        // Distinct from the blind UpsertAsync, which records that the state is not a fold at all.
        public Task UpsertUnnumberedAsync(
        	string id, Tally projection, CancellationToken cancellationToken)
        {
        	UnnumberedWrites++;
        	return UpsertAsync(id, projection, cancellationToken);
        }

        /// <summary>Gets how many unnumbered writes this double received.</summary>
        public int UnnumberedWrites { get; private set; }

public Task<(Tally? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id, CancellationToken ct) =>
            Task.FromResult(_rows.TryGetValue(id, out var r)
                ? (r.State, ProjectionPosition.FromStored(r.Position))
                : ((Tally?)null, ProjectionPosition.Unnumbered));

        /// <summary>Gets how many advancing writes this double received.</summary>
        /// <remarks>
        /// The ATTEMPT COUNT, and the reason it is here rather than only a message assertion: recovery
        /// performs exactly one advancing write per attempt, and each attempt replays the whole stream. An
        /// arm that checked only the wording of a terminal failure would pass a version that still burned
        /// MaxRecoveryAttempts replays before saying the right thing.
        /// </remarks>
        public int AdvanceAttempts { get; private set; }

        public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
            string id, Tally projection, long? expectedPosition, long newPosition, CancellationToken ct)
        {
            AdvanceAttempts++;

            var present = _rows.TryGetValue(id, out var existing);
            var stored = present ? existing.Position : null;

            // A PRESENT ROW CARRYING NO NUMBER IS TERMINAL, exactly as every real provider now reports it.
            // Modelled here because the equality below cannot express it: the caller passes null for a row
            // it read as no-number, and `stored != expectedPosition` would answer Superseded -- which tells
            // the caller to retry against a row that never changes.
            if (present && existing.Position is { } held && held < 0)
            {
                return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null));
            }

            // Both conjuncts, exactly as a real store enforces them.
            if (stored != expectedPosition || (present && newPosition <= existing.Position))
            {
                return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, stored));
            }

            _rows[id] = (projection, newPosition);

            return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
        }

        // Rewrites the state at the position the row already holds. Never creates: an absent row was
        // deleted, and deletion is how erasure removes personal data.
        public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
            string id, Tally projection, long atPosition, CancellationToken ct)
        {
            if (!_rows.TryGetValue(id, out var row))
            {
                return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null));
            }

            if (row.Position is not { } stored)
            {
                return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null));
            }

            if (stored != atPosition)
            {
                return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, stored));
            }

            _rows[id] = (projection, atPosition);

            return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));
        }

        // Overwrites BOTH state and position of an EXISTING row, for a caller that folded the whole
        // stream from an empty seed. Unconditional on POSITION but conditional on EXISTENCE: an absent
        // row was deleted, deletion is how erasure removes personal data, and a replay must not restore it.
        public Task<ProjectionRebuildResult> RebuildAtPositionAsync(string id, Tally projection, long newPosition, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

            if (!_rows.ContainsKey(id))
            {
                return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished));
            }

            _rows[id] = (projection, newPosition);

            return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
        }

        public Task<Tally?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Get(id));

        public Task UpsertAsync(string id, Tally projection, CancellationToken ct)
        {
            // Matches the relational providers: an unconditional write INVALIDATES the position.
            _rows[id] = (projection, null);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct)
        {
            _ = _rows.Remove(id);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Tally>> QueryAsync(
            IDictionary<string, object>? filters, QueryOptions? options, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Tally>>([]);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken ct) =>
            Task.FromResult(0L);
    }
}
