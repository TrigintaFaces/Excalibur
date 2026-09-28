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
    /// A FULLY erased aggregate keeps its position and loses its state.
    /// </summary>
    /// <remarks>
    /// The position is honest — the row has folded everything up to it, and everything up to it is now
    /// nothing. Clearing the position instead is what leaves the row adoptable by the next batch, which
    /// then folds onto empty state and stamps a position asserting a prefix it does not hold.
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
            + "everything up to 2 is now nothing. Clearing it leaves the row adoptable");
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

        public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
            string id, Tally projection, long? expectedPosition, long newPosition, CancellationToken ct)
        {
            var present = _rows.TryGetValue(id, out var existing);
            var stored = present ? existing.Position : null;

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
