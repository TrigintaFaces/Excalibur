// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Handlers.Tests;

/// <summary>
/// Author≠impl <strong>augmentation</strong> lock for the aggregate-handler Decider (bead C1 / `0aajc0`),
/// complementing the impl-authored <see cref="AggregateHandlerShould"/> (which locks persist / not-found /
/// concurrency). This independent pass covers the seam behaviors that pass-count could otherwise hide:
/// that <c>resolveId</c> actually selects the target aggregate (not a constant), that <em>every</em> event
/// a multi-event decision raises is durably persisted, that a decision raising <em>no</em> event is a clean
/// no-op (no spurious concurrency conflict), and that sequential dispatches accumulate against the reloaded
/// current state.
/// </summary>
/// <remarks>
/// Drives the REAL committed <c>AddAggregateHandler</c> glue over a REAL production event store
/// (<c>AddInMemoryEventStore</c> — no mocks); emitted behavior is asserted by reloading on a fresh
/// repository over the same store. Non-skipped, in-process (the Decider is Excalibur-side orchestration;
/// the real infrastructure under test is the event-store round-trip). RED on absent glue — without the
/// handler registration the message never reaches the aggregate.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AggregateHandlerDeciderAugmentationShould
{
	private static IEventStore NewRealEventStore()
		=> new ServiceCollection()
			.AddInMemoryEventStore()
			.BuildServiceProvider()
			.GetRequiredKeyedService<IEventStore>("default");

	private static EventSourcedRepository<Counter, string> NewRepository(IEventStore store)
		=> new(store, new JsonEventSerializer(
				// A declared name resolves through the registry, never through the assembly scan --
				// the scan matches CLR type names and a declared name deliberately is not one.
				new ServiceCollection().AddEventTypes(typeof(CounterCreated), typeof(CounterIncremented))
					.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>(),
				options: null,
				allowAssemblyScan: false), id => new Counter(id),
			Options.Create(new EventSourcedRepositoryOptions()));

	private static ServiceProvider BuildHandlerProvider(
		EventSourcedRepository<Counter, string> repository,
		Func<Counter, AdjustCounter, CancellationToken, Task> decide)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcedRepository<Counter, string>>(repository);
		services.AddAggregateHandler<Counter, string, AdjustCounter>(
			resolveId: msg => msg.CounterId,
			decide: decide);
		return services.BuildServiceProvider();
	}

	private static async Task<Counter> SeedAsync(IEventStore store, string id)
	{
		var repo = NewRepository(store);
		var counter = new Counter(id);
		counter.Create(id);
		await repo.SaveAsync(counter, expectedETag: null, CancellationToken.None);
		return counter;
	}

	private static Func<Counter, AdjustCounter, CancellationToken, Task> AdjustDecision()
		=> (counter, msg, _) =>
		{
			counter.Adjust(msg.By);
			return Task.CompletedTask;
		};

	private static async Task DispatchAsync(IEventStore store, AdjustCounter message)
	{
		await using var provider = BuildHandlerProvider(NewRepository(store), AdjustDecision());
		var handler = provider.GetRequiredService<IActionHandler<AdjustCounter>>();
		await handler.HandleAsync(message, CancellationToken.None);
	}

	[Fact]
	public async Task RouteToTheAggregateSelectedByResolveIdAndLeaveOthersUntouched()
	{
		// Two live aggregates over the same store; resolveId must pick exactly the message's target.
		var store = NewRealEventStore();
		var targetId = Guid.NewGuid().ToString();
		var otherId = Guid.NewGuid().ToString();
		await SeedAsync(store, targetId);
		await SeedAsync(store, otherId);

		await DispatchAsync(store, new AdjustCounter(targetId, By: 5));

		var reloadTarget = await NewRepository(store).GetByIdAsync(targetId, CancellationToken.None);
		var reloadOther = await NewRepository(store).GetByIdAsync(otherId, CancellationToken.None);
		reloadTarget.ShouldNotBeNull();
		reloadOther.ShouldNotBeNull();
		reloadTarget!.Count.ShouldBe(5, "resolveId must route to the message's aggregate");
		reloadOther!.Count.ShouldBe(0, "a sibling aggregate must be untouched");
	}

	[Fact]
	public async Task PersistEveryEventWhenTheDecisionRaisesMoreThanOne()
	{
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();
		var seed = await SeedAsync(store, id);

		await DispatchAsync(store, new AdjustCounter(id, By: 3)); // raises 3 increment events

		var reloaded = await NewRepository(store).GetByIdAsync(id, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded!.Count.ShouldBe(3, "all three increment events must be persisted and replayed");
		reloaded.Version.ShouldBe(seed.Version + 3, "version must advance by the full event count, not by one");
	}

	[Fact]
	public async Task PersistNothingAndNotConflictWhenTheDecisionRaisesNoEvent()
	{
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();
		var seed = await SeedAsync(store, id);

		// A decision that raises no event must save cleanly (no-op), never a spurious ConcurrencyException.
		await Should.NotThrowAsync(() => DispatchAsync(store, new AdjustCounter(id, By: 0)));

		var reloaded = await NewRepository(store).GetByIdAsync(id, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded!.Count.ShouldBe(0);
		reloaded.Version.ShouldBe(seed.Version, "an eventless decision must not advance the version");
	}

	[Fact]
	public async Task AccumulateAcrossSequentialDispatchesOverTheSameStore()
	{
		// Each dispatch loads the current state and its load-time ETag; sequential dispatches must compose
		// (the second sees the first's result), proving the optimistic-concurrency ETag is re-read per call.
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();
		await SeedAsync(store, id);

		await DispatchAsync(store, new AdjustCounter(id, By: 2));
		await DispatchAsync(store, new AdjustCounter(id, By: 3));

		var reloaded = await NewRepository(store).GetByIdAsync(id, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded!.Count.ShouldBe(5, "sequential dispatches must accumulate over the reloaded current state");
	}

	#region Real test aggregate + message

	/// <summary>A minimal real event-sourced aggregate whose decision can raise zero, one, or many events.</summary>
	internal sealed class Counter : AggregateRoot, IAggregateSnapshotSupport
	{
		public Counter()
		{
		}

		public Counter(string id) : base(id)
		{
		}

		public int Count { get; private set; }

		public void Create(string id) => RaiseEvent(new CounterCreated(id));

		/// <summary>Raises <paramref name="by"/> increment events (zero when <paramref name="by"/> is 0).</summary>
		public void Adjust(int by)
		{
			for (var i = 0; i < by; i++)
			{
				RaiseEvent(new CounterIncremented(Id));
			}
		}

		protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
		{
			CounterCreated e => Apply(e),
			CounterIncremented e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown event {@event.GetType().Name}"),
		};

		private bool Apply(CounterCreated e)
		{
			Id = e.AggregateId;
			return true;
		}

		private bool Apply(CounterIncremented e)
		{
			_ = e;
			Count++;
			return true;
		}
	}

	[MessageName("Test.CounterCreated")]
	private sealed record CounterCreated(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[MessageName("Test.AggregateHandlerDeciderAugmentation.CounterIncremented")]
	private sealed record CounterIncremented(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>The dispatched command routed to the counter aggregate.</summary>
	internal sealed record AdjustCounter(string CounterId, int By) : IDispatchAction;

	#endregion Real test aggregate + message
}
