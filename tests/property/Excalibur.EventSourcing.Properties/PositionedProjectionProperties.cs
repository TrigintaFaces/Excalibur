// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Properties;

/// <summary>
/// R3 for the positioned-projection seam: the fold invariant, checked against GENERATED delivery
/// schedules rather than chosen ones.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this adds over the conformance kit, and why the distinction is not pedantic.</b> The
/// conformance kit states the same contract as nine hand-written arms. Those arms encode the
/// interleavings somebody thought of. This suite generates delivery schedules — overlapping batches,
/// re-deliveries, out-of-order arrival, batches that skip forward, the same batch twice in a row — and
/// checks the invariant against every one of them. It is still SAMPLING and it is still not a proof:
/// it does not search the state space, it draws from it. That is exactly what this rung is and it must
/// not be described as more.
/// </para>
/// <para>
/// <b>The invariant.</b> For a projection with stored position <c>P</c>:
/// <c>state = fold(apply, init, { e : pos(e) &lt;= P })</c>. The projection accumulates, so the folded
/// state is a COUNT and the invariant is checkable by arithmetic: the stored total must equal the
/// number of DISTINCT events at or below the stored position. An assigning projection would satisfy
/// this for any schedule whatsoever, which is why it would be the wrong shape to generate against.
/// </para>
/// <para>
/// <b>A failure reproduces.</b> Every case carries its seed, the failure message prints it and the
/// schedule that produced it, and the suite shrinks — it retries the failing seed with progressively
/// shorter prefixes of the schedule and reports the shortest one that still breaks, because a
/// forty-step counterexample is evidence and a three-step one is a diagnosis.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class PositionedProjectionProperties
{
	/// <summary>How many generated schedules to run. Each is an independent delivery history.</summary>
	private const int Cases = 300;

	/// <summary>
	/// The root seed. Fixed so the suite is deterministic: a generated suite that draws from a random
	/// root fails on somebody else's machine and passes on yours, which makes it unusable as a gate.
	/// Change it deliberately to sample a different region.
	/// </summary>
	private const int RootSeed = 20260926;

	[Fact]
	public async Task Hold_the_fold_invariant_across_generated_delivery_schedules()
	{
		for (var caseIndex = 0; caseIndex < Cases; caseIndex++)
		{
			var seed = RootSeed + caseIndex;
			var schedule = GenerateSchedule(seed);

			var failure = await RunAsync(schedule).ConfigureAwait(false);

			if (failure is null)
			{
				continue;
			}

			var shortest = await ShrinkAsync(schedule).ConfigureAwait(false);

			Assert.Fail(
				$"The fold invariant broke on generated case seed {seed.ToString(CultureInfo.InvariantCulture)}."
				+ Environment.NewLine + failure
				+ Environment.NewLine + Environment.NewLine
				+ "Shortest schedule that still breaks it:" + Environment.NewLine
				+ Describe(shortest));
		}
	}

	/// <summary>
	/// The same generator, asserting the property the whole contract exists for: no event is ever
	/// folded twice.
	/// </summary>
	/// <remarks>
	/// Stated separately from the invariant above because the two can fail independently. A store that
	/// stopped writing satisfies "no double fold" trivially, and the invariant arm is what refuses that;
	/// a store that double-counted but also advanced its position consistently could satisfy neither.
	/// </remarks>
	[Fact]
	public async Task Never_fold_an_event_twice_across_generated_delivery_schedules()
	{
		for (var caseIndex = 0; caseIndex < Cases; caseIndex++)
		{
			var seed = RootSeed + 100_000 + caseIndex;
			var schedule = GenerateSchedule(seed);

			var store = new CountingStore();
			var apply = BuildApply();
			var provider = Provider(store);

			foreach (var batch in schedule)
			{
				await apply(ToEvents(batch), Context(), provider, CancellationToken.None)
					.ConfigureAwait(false);
			}

			var delivered = schedule.SelectMany(static b => b).Distinct().Count();
			var stored = store.Get(ProjectionId)?.Total ?? 0;

			Assert.True(
				stored <= delivered,
				$"seed {seed.ToString(CultureInfo.InvariantCulture)}: the projection folded "
				+ $"{stored.ToString(CultureInfo.InvariantCulture)} events from a history containing only "
				+ $"{delivered.ToString(CultureInfo.InvariantCulture)} distinct ones, so at least one was "
				+ "applied more than once." + Environment.NewLine + Describe(schedule));
		}
	}

	/// <summary>
	/// Runs a schedule and returns a description of the broken invariant, or null when it held.
	/// </summary>
	private static async Task<string?> RunAsync(IReadOnlyList<long[]> schedule)
	{
		var store = new CountingStore();
		var apply = BuildApply();
		var provider = Provider(store);

		foreach (var batch in schedule)
		{
			await apply(ToEvents(batch), Context(), provider, CancellationToken.None).ConfigureAwait(false);
		}

		var storedPosition = store.PositionOf(ProjectionId);
		var storedTotal = store.Get(ProjectionId)?.Total ?? 0;

		// The oracle: everything delivered at or below the stored position, counted once each.
		var expected = schedule
			.SelectMany(static b => b)
			.Distinct()
			.Count(position => storedPosition is { } at && position <= at);

		if (storedTotal == expected)
		{
			return null;
		}

		return $"stored position {Format(storedPosition)} carries a folded total of "
			+ $"{storedTotal.ToString(CultureInfo.InvariantCulture)}, but the distinct events at or below "
			+ $"that position number {expected.ToString(CultureInfo.InvariantCulture)}. The position "
			+ "asserts which prefix is folded in, so those two must be equal.";
	}

	/// <summary>
	/// Returns the shortest prefix of a failing schedule that still fails.
	/// </summary>
	/// <remarks>
	/// Prefix shrinking only, deliberately: a delivery history is ordered, and dropping a batch from the
	/// middle produces a history the generator could not have produced, so a "counterexample" found that
	/// way might not correspond to anything reachable.
	/// </remarks>
	private static async Task<IReadOnlyList<long[]>> ShrinkAsync(IReadOnlyList<long[]> schedule)
	{
		for (var length = 1; length < schedule.Count; length++)
		{
			var prefix = schedule.Take(length).ToArray();

			if (await RunAsync(prefix).ConfigureAwait(false) is not null)
			{
				return prefix;
			}
		}

		return schedule;
	}

	/// <summary>
	/// Builds one delivery schedule: a sequence of batches of global positions.
	/// </summary>
	/// <remarks>
	/// The shapes here are the ones that break naive implementations, and each is deliberate rather than
	/// incidental noise: a fresh batch advancing the stream; an exact RE-DELIVERY of the previous batch
	/// (the restart case); an OVERLAPPING batch that repeats part of the previous one and extends past
	/// it (the resumed-from-an-older-checkpoint case); and a batch that SKIPS forward, leaving a gap the
	/// projection never sees, which is legitimate because a projection observes a subsequence of the
	/// global stream.
	/// </remarks>
	private static IReadOnlyList<long[]> GenerateSchedule(int seed)
	{
		var random = new Random(seed);
		var batches = new List<long[]>();
		var head = 0L;
		long[]? previous = null;

		var batchCount = random.Next(1, 9);

		for (var i = 0; i < batchCount; i++)
		{
			var shape = previous is null ? 0 : random.Next(0, 4);

			switch (shape)
			{
				case 1 when previous is not null:
					// Exact re-delivery: the reader restarted from a mark it had already passed.
					batches.Add(previous);
					continue;

				case 2 when previous is { Length: > 1 }:
					// Overlap: part of the previous batch arrives again, followed by new events.
					var keep = random.Next(1, previous.Length + 1);
					var extend = random.Next(1, 4);
					var overlapped = new long[keep + extend];
					Array.Copy(previous, previous.Length - keep, overlapped, 0, keep);
					for (var j = 0; j < extend; j++)
					{
						head++;
						overlapped[keep + j] = head;
					}

					batches.Add(overlapped);
					previous = overlapped;
					continue;

				case 3:
					// A gap: the projection simply has no event for those positions.
					head += random.Next(2, 6);
					break;

				default:
					break;
			}

			var size = random.Next(1, 5);
			var batch = new long[size];
			for (var j = 0; j < size; j++)
			{
				head++;
				batch[j] = head;
			}

			batches.Add(batch);
			previous = batch;
		}

		return batches;
	}

	private static string Describe(IReadOnlyList<long[]> schedule)
	{
		var text = new StringBuilder();
		for (var i = 0; i < schedule.Count; i++)
		{
			_ = text.Append("  batch ")
				.Append(i.ToString(CultureInfo.InvariantCulture))
				.Append(": [")
				.Append(string.Join(", ", schedule[i].Select(static p => p.ToString(CultureInfo.InvariantCulture))))
				.AppendLine("]");
		}

		return text.ToString();
	}

	private static string Format(long? value) =>
		value?.ToString(CultureInfo.InvariantCulture) ?? "none";

	private const string ProjectionId = "generated-aggregate";

	private static IReadOnlyList<ProjectionEvent> ToEvents(long[] positions) =>
		[.. positions.Select(static p => new ProjectionEvent(new Counted(), ProjectionId, p))];

	private static ProjectionRegistration.InlineApplyDelegate BuildApply()
	{
		var builder = new ProjectionBuilder<Counter>(new ServiceCollection());
		builder.When<Counted>(static (p, _) => p.Total++);

		var registry = new InMemoryProjectionRegistry();
		builder.Build(registry);

		return registry.GetRegistration(typeof(Counter))!.InlineApply!;
	}

	private static IServiceProvider Provider(CountingStore store)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IProjectionStore<Counter>>(store);
		return services.BuildServiceProvider();
	}

	private static EventNotificationContext Context() =>
		new(ProjectionId, "Agg", 1, DateTimeOffset.UnixEpoch);

	private sealed class Counter
	{
		public int Total { get; set; }
	}

	[MessageName("Test.PositionedProjectionInvariant.Counted")]
	private sealed record Counted : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public string EventType { get; init; } = nameof(Counted);

		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// A positioned store enforcing exactly the contract's two conjuncts, and nothing else.
	/// </summary>
	/// <remarks>
	/// Implements the interface directly so the suite binds the CONTRACT rather than re-testing a
	/// first-party base class that would supply the member under test.
	/// </remarks>
	private sealed class CountingStore : IPositionedProjectionStore<Counter>
	{
		private readonly Dictionary<string, (Counter State, long? Position)> _rows =
			new(StringComparer.Ordinal);

		internal Counter? Get(string id) => _rows.TryGetValue(id, out var r) ? r.State : null;

		internal long? PositionOf(string id) => _rows.TryGetValue(id, out var r) ? r.Position : null;

		// A complete fold whose prefix has no global position number -- distinct from the blind
		// UpsertAsync, which records that the state is not a fold over any prefix at all.
		public Task UpsertUnnumberedAsync(
			string id, Counter projection, CancellationToken cancellationToken) =>
			UpsertAsync(id, projection, cancellationToken);

		public Task<(Counter? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id, CancellationToken cancellationToken) =>
			Task.FromResult(_rows.TryGetValue(id, out var r)
				? (Clone(r.State), ProjectionPosition.FromStored(r.Position))
				: ((Counter?)null, ProjectionPosition.Unnumbered));

		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id, Counter projection, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken)
		{
			var present = _rows.TryGetValue(id, out var existing);
			var stored = present ? existing.Position : null;

			if (stored != expectedPosition || (stored is { } at && newPosition <= at))
			{
				return Task.FromResult(new ProjectionAdvanceResult(
					ProjectionAdvanceOutcome.Superseded, stored));
			}

			_rows[id] = (Clone(projection), newPosition);
			return Task.FromResult(new ProjectionAdvanceResult(
				ProjectionAdvanceOutcome.Applied, newPosition));
		}

		// Rewrites the state at the position the row already holds. Never creates: an absent row was
		// deleted, and deletion is how erasure removes personal data.
		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, Counter projection, long atPosition, CancellationToken cancellationToken)
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

			_rows[id] = (Clone(projection), atPosition);

			return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));
		}

		public Task<Counter?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult(Get(id));

		public Task UpsertAsync(string id, Counter projection, CancellationToken cancellationToken)
		{
			_rows[id] = (Clone(projection), PositionOf(id));
			return Task.CompletedTask;
		}

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_ = _rows.Remove(id);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<Counter>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Counter>>([.. _rows.Values.Select(static v => v.State)]);

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult((long)_rows.Count);

		private static Counter Clone(Counter c) => new() { Total = c.Total };
	}
}
