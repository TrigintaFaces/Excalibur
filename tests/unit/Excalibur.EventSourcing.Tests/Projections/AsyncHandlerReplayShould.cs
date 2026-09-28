// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// A projection registered with an ASYNCHRONOUS handler must be folded by a replay, and the handler
/// must be able to tell that it is a replay.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms bind.</b> <c>MultiStreamProjection.Apply</c> is synchronous, so it can
/// dispatch only the two synchronous handler shapes. For an entry registered by
/// <c>WhenHandledBy&lt;TEvent, THandler&gt;()</c> it fell through to <see langword="false"/> — and both
/// replay callers, the rebuild service and the recovery service, discarded that value. A rebuild or a
/// recovery of such a projection therefore folded NOTHING and reported success.
/// </para>
/// <para>
/// The recovery path is the more serious of the two: <c>ReapplyAsync</c> is the remedy the guarantee
/// document names for clearing an erased subject from a per-aggregate projection, so a GDPR erasure
/// remedy could not fold the events of any projection using this registration.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Point the replay back at the synchronous <c>Apply</c> and
/// <see cref="Fold_an_event_whose_handler_is_asynchronous"/> goes RED, because nothing is folded. The
/// sibling sync-handler arm stays GREEN, which is what distinguishes "the replay is broken" from "this
/// one handler shape is dropped".
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AsyncHandlerReplayShould
{
	/// <summary>The asynchronous handler runs and its fold lands in the state.</summary>
	[Fact]
	public async Task Fold_an_event_whose_handler_is_asynchronous()
	{
		var projection = BuildAsyncHandledProjection();
		var state = new Tally();

		var applied = await projection.ApplyAsync(
			state,
			new Counted(),
			Context(),
			HandlerContext(isReplay: true),
			Provider(),
			CancellationToken.None);

		applied.ShouldBeTrue("a handler IS registered for this event type, so the apply must report that it ran");
		state.Total.ShouldBe(
			1,
			"the asynchronous handler is the only one registered for this event. If it is not invoked, a "
			+ "rebuild folds nothing and still reports success -- which is the defect this arm exists for.");
	}

	/// <summary>LIVENESS: the synchronous shape still works, so the fix did not trade one for the other.</summary>
	[Fact]
	public async Task Still_fold_an_event_whose_handler_is_synchronous()
	{
		var projection = new MultiStreamProjection<Tally>();
		projection.AddHandler<Counted>(static (t, _) => t.Total++);
		var state = new Tally();

		var applied = await projection.ApplyAsync(
			state, new Counted(), Context(), HandlerContext(isReplay: true), Provider(), CancellationToken.None);

		applied.ShouldBeTrue();
		state.Total.ShouldBe(1);
	}

	/// <summary>
	/// An event type with NO registered handler reports that nothing ran, and that is not an error.
	/// </summary>
	/// <remarks>
	/// This is the case the old return value conflated with the async shape. Keeping it distinct is what
	/// lets a caller treat "no handler" as ordinary while a malformed entry throws.
	/// </remarks>
	[Fact]
	public async Task Report_that_nothing_ran_when_no_handler_is_registered()
	{
		var projection = new MultiStreamProjection<Tally>();

		var applied = await projection.ApplyAsync(
			projection: new Tally(),
			new Counted(),
			Context(),
			HandlerContext(isReplay: true),
			Provider(),
			CancellationToken.None);

		applied.ShouldBeFalse("no handler is registered for this type, which is a legitimate outcome");
	}

	/// <summary>The handler can tell a replay from a live delivery.</summary>
	/// <remarks>
	/// Without this the fix for the silent drop becomes a silent RE-EXECUTION: an asynchronous handler
	/// is arbitrary consumer code that may send mail or call a payment provider, and a rebuild re-runs
	/// every one of them over history.
	/// </remarks>
	[Fact]
	public async Task Tell_the_handler_that_it_is_replaying()
	{
		var projection = BuildAsyncHandledProjection();
		var state = new Tally();

		_ = await projection.ApplyAsync(
			state, new Counted(), Context(), HandlerContext(isReplay: true), Provider(), CancellationToken.None);

		state.SawReplay.ShouldBeTrue(
			"a handler with side effects outside the projection must be able to skip them on a replay, "
			+ "and it can only do that if the flag reaches it");
	}

	private static MultiStreamProjection<Tally> BuildAsyncHandledProjection()
	{
		var projection = new MultiStreamProjection<Tally>();

		// Registered as an ASYNC handler, which is the shape WhenHandledBy<TEvent, THandler>() produces
		// and the one the synchronous Apply silently skipped.
		projection.AddAsyncHandler<Counted>((tally, _, handlerContext, _, _) =>
		{
			tally.Total++;
			tally.SawReplay = handlerContext.IsReplay;
			return Task.CompletedTask;
		});

		return projection;
	}

	private static ProjectionContext Context() => new(isReplay: true, globalPosition: 1, aggregateId: "agg-1");

	private static ProjectionHandlerContext HandlerContext(bool isReplay) =>
		new("agg-1", "Agg", 1, DateTimeOffset.UnixEpoch, isReplay);

	private static IServiceProvider Provider() => new ServiceCollection().BuildServiceProvider();

	private sealed class Tally
	{
		public int Total { get; set; }

		public bool SawReplay { get; set; }
	}

	// [MessageName] is REQUIRED, not decorative: a startup validator scans the whole assembly for
	// IDomainEvent implementations that declare no message name and throws. Omitting it here broke two
	// unrelated startup-gate arms in this project, because an event is stored under the name it
	// declares and one without a name would fail inside AppendAsync rather than at startup.
	[MessageName("Test.AsyncHandlerReplay.Counted")]
	private sealed class Counted : IDomainEvent
	{
		public string EventId { get; } = Guid.NewGuid().ToString("N");

		public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UnixEpoch;

		public IDictionary<string, object>? Metadata => null;
	}
}
