// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- fakes store ValueTask in setup
#pragma warning disable CA1506 // Avoid excessive class coupling -- engage-tests require many DI types

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

// ONE FAULTING PROJECTION MUST NOT HALT A WRITER THAT DOES NOT DEPEND ON IT.
//
// Every projection on a subscription used to share ONE cursor, refused as a unit the moment any
// registration's apply faulted. Under Inline that stall sits in the WRITE path: a save waits for the
// processor to report caught-up, so a single broken handler made every save -- for every aggregate,
// every tenant -- burn its whole post-commit budget and then report its read model behind, with the
// events already durably committed.
//
// Cursors are now per registration, and the completion predicate is a quantifier over the projections
// a save actually FEEDS rather than a minimum over all of them. These two arms are the pair:
//
//   SAFETY   a save that FEEDS the broken projection still reports the fault. Without this arm the
//            "fix" could be a blanket success that hides every failure.
//   LIVENESS a save that feeds only healthy projections succeeds DESPITE the broken sibling. Without
//            this arm the safety arm is satisfied by the old shared-fate behaviour.
//
// Neither substitutes for the other, and no single mutation fails both.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PerProjectionCursorShould
{
    [Fact]
    public async Task PreservePrefixRetryForAHealthyProjectionDespiteAnUnrelatedFault()
    {
        var fixture = new TwoProjections();
        using var host = fixture.CreateHost();
        await host.ProcessPendingAsync(CancellationToken.None);
        // The committed target is ahead of the currently visible global prefix.
        var result = await host.ProcessThroughAsync(3, [typeof(HealthyProjection)], CancellationToken.None);
        result.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
        result.HaltReason.ShouldBe(ProjectionHaltReason.PrefixUnavailable);
    }

    [Fact]
    public async Task KeepTheFailedPrefixPendingAcrossAnUnrelatedLaterPage()
    {
        var fixture = new TwoProjections();
        using var host = fixture.CreateHost();
        for (var i = 0; i < 3; i++) { await host.ProcessPendingAsync(CancellationToken.None); }
        fixture.Append(healthy: true);
        await host.ProcessPendingAsync(CancellationToken.None);
        (await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<BrokenProjection>(), CancellationToken.None)).ShouldBeNull();
        (await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<HealthyProjection>(), CancellationToken.None)).ShouldBe(3);
    }

    [Fact]
    public async Task ReplayTheWholeFailedPrefixAfterRepair()
    {
        var fixture = new TwoProjections();
        using var host = fixture.CreateHost();
        for (var i = 0; i < 3; i++) { await host.ProcessPendingAsync(CancellationToken.None); }
        fixture.Append(healthy: false);
        fixture.BrokenFails = false;
        (await host.ProcessPendingAsync(CancellationToken.None)).Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
        fixture.BrokenPositions.ShouldBe(new long[] { 1, 3 });
    }

    [Fact]
    public async Task RecoverTheOnlyProjectionAfterThreeTransientFailuresWithoutRestart()
    {
        var fixture = new TwoProjections();
        using var host = fixture.CreateHost(includeHealthy: false);
        for (var i = 0; i < 3; i++) { await host.ProcessPendingAsync(CancellationToken.None); }
        fixture.BrokenFails = false;
        (await host.ProcessPendingAsync(CancellationToken.None)).Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
        fixture.BrokenPositions.ShouldBe(new long[] { 1 });
    }

    [Fact]
    public async Task NeverReportTheWholeSubscriptionCaughtUpWhileASiblingStillFails()
    {
        var fixture = new TwoProjections();
        using var host = fixture.CreateHost();
        for (var i = 0; i < 5; i++)
        {
            (await host.ProcessPendingAsync(CancellationToken.None)).Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
        }
    }

    [Fact]
    public async Task AdvanceARequiredHealthyProjectionAcrossMultiplePagesDespiteAFailedSibling()
    {
        var fixture = new TwoProjections();
        for (var i = 0; i < 6; i++) { fixture.Append(healthy: true); }
        using var host = fixture.CreateHost(batchSize: 2);
        var result = await host.ProcessThroughAsync(8, [typeof(HealthyProjection)], CancellationToken.None);
        result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
        (await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<HealthyProjection>(), CancellationToken.None)).ShouldBe(8);
        (await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<BrokenProjection>(), CancellationToken.None)).ShouldBeNull();
    }

	// LIVENESS: this is the defect. The save feeds Healthy only; Broken is behind and faulting.
	[Fact]
	public async Task CatchUpASaveThatDoesNotFeedTheBrokenProjection()
	{
		var fixture = new TwoProjections();
		var host = fixture.CreateHost();

		var result = await host.ProcessThroughAsync(
			committedPosition: 2,
			required: [typeof(HealthyProjection)],
			CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp,
			"a save feeding only healthy projections must not be held open by an unrelated broken one");
		result.HaltReason.ShouldBe(ProjectionHaltReason.None);

		// The healthy cursor advanced on its own while the broken one did not: that independence IS the fix.
		(await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<HealthyProjection>(), CancellationToken.None))
			.ShouldBe(2);
		(await fixture.Checkpoints.GetCheckpointAsync(fixture.KeyFor<BrokenProjection>(), CancellationToken.None))
			.ShouldBeNull("the faulting projection must not advance over an event it never folded");
	}

	// SAFETY: a save that DOES feed the broken projection must still be told.
	[Fact]
	public async Task StillReportTheFaultForASaveThatFeedsTheBrokenProjection()
	{
		var fixture = new TwoProjections();
		var host = fixture.CreateHost();

		var result = await host.ProcessThroughAsync(
			committedPosition: 2,
			required: [typeof(BrokenProjection)],
			CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted,
			"a save whose own projection faulted must never be reported as caught up");
		result.HaltReason.ShouldBe(ProjectionHaltReason.ProjectionFaulted);
	}

	private sealed class TwoProjections
	{
		private readonly IGlobalStreamQuery _query = A.Fake<IGlobalStreamQuery>();
		private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();

		internal InMemorySubscriptionCheckpointStore Checkpoints { get; } = new();
        internal bool BrokenFails { get; set; } = true;
        internal List<long> BrokenPositions { get; } = [];
        private readonly List<StoredEvent> _stored = [];

        internal void Append(bool healthy)
        {
            var position = _stored.Count + 1;
            _stored.Add(new StoredEvent($"e{position}", "row", "Order", healthy ? nameof(HealthyEvent) : nameof(BrokenEvent),
                new byte[] { 1 }, null, position, DateTimeOffset.UtcNow) { GlobalPosition = position });
        }

		internal TwoProjections()
		{
			// Position 1 is consumed ONLY by the broken projection, position 2 ONLY by the healthy one.
			// That separation is what makes the required set discriminate.
			var stored = new[]
			{
				new StoredEvent("e1", "row", "Order", nameof(BrokenEvent), "1"u8.ToArray(), null, 1, DateTimeOffset.UtcNow)
				{
					GlobalPosition = 1,
				},
				new StoredEvent("e2", "row", "Order", nameof(HealthyEvent), "2"u8.ToArray(), null, 2, DateTimeOffset.UtcNow)
				{
					GlobalPosition = 2,
				},
			};

            _stored.AddRange(stored);
			A.CallTo(() => _query.GetHeadPositionAsync(A<CancellationToken>._)).ReturnsLazily(() => (long)_stored.Count);
			A.CallTo(() => _query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
				.ReturnsLazily((GlobalStreamPosition p, int n, CancellationToken _) =>
					ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
						_stored.Where(e => e.GlobalPosition > p.Position).Take(n).ToArray()));

			A.CallTo(() => _serializer.ResolveType(nameof(BrokenEvent))).Returns(typeof(BrokenEvent));
			A.CallTo(() => _serializer.ResolveType(nameof(HealthyEvent))).Returns(typeof(HealthyEvent));
			A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, A<Type>._))
				.ReturnsLazily((byte[] payload, Type type) =>
					type == typeof(BrokenEvent) ? new BrokenEvent() : (IDomainEvent)new HealthyEvent());
		}

		internal string KeyFor<TProjection>() =>
			$"AsyncProjectionProcessingHost:{typeof(TProjection).FullName}";

		internal AsyncProjectionProcessingHost CreateHost(bool includeHealthy = true, int batchSize = 100)
		{
			var registry = new InMemoryProjectionRegistry();

			if (includeHealthy) registry.Register(new ProjectionRegistration(
				typeof(HealthyProjection), ProjectionMode.Async, projection: new object(),
				inlineApply: static (_, _, _, _) => Task.CompletedTask)
			{
				HandlesEvent = type => type == typeof(HealthyEvent),
			});

			registry.Register(new ProjectionRegistration(
				typeof(BrokenProjection), ProjectionMode.Async, projection: new object(),
				inlineApply: (events, _, _, _) =>
                {
                    if (BrokenFails) { throw new InvalidOperationException("this projection is broken"); }
                    foreach (var item in events.Where(e => e.Domain is BrokenEvent))
                    {
                        if (!BrokenPositions.Contains(item.GlobalPosition!.Value)) { BrokenPositions.Add(item.GlobalPosition.Value); }
                    }
                    return Task.CompletedTask;
                })
			{
				HandlesEvent = type => type == typeof(BrokenEvent),
			});

			var services = new ServiceCollection().AddSingleton(_query).BuildServiceProvider();

			return new AsyncProjectionProcessingHost(
				registry,
				_serializer,
				Checkpoints,
				Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
				{
					SubscriptionScope = "unit-test",
                    BatchSize = batchSize,
					IdlePollingInterval = TimeSpan.FromMilliseconds(10),
					CheckpointInterval = 1,
				}),
				services,
				NullLogger<AsyncProjectionProcessingHost>.Instance);
		}
	}

	// DECLARED, not derived. These implement IDomainEvent, so the assembly-wide event-type scan finds
	// them and refuses an undeclared stored identity -- which failed five tests in unrelated classes
	// until these attributes were added. A test event is still a stored event as far as that scan knows.
	[MessageName("PerProjectionCursorShould.HealthyEvent")]
	private sealed record HealthyEvent : DomainEvent;

	[MessageName("PerProjectionCursorShould.BrokenEvent")]
	private sealed record BrokenEvent : DomainEvent;

	private sealed class HealthyProjection
	{
		public int Count { get; set; }
	}

	private sealed class BrokenProjection
	{
		public int Count { get; set; }
	}
}
