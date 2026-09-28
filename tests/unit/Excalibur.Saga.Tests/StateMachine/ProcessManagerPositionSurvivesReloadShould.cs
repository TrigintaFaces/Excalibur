// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.StateMachine;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// A process manager's POSITION is saga state, and must survive being reloaded into a new instance.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the arm the subsystem never had, and its absence is why the defect shipped.</b> Every
/// existing position assertion drives ONE long-lived process-manager instance: transition it, then read
/// <c>CurrentState</c> off the same object. That can never observe the defect, because the defect is
/// entirely about what happens when the object goes away.
/// </para>
/// <para>
/// <b>What it observes.</b> The coordinator constructs a saga fresh for every delivered message. The
/// position used to live in a private field initialised to <c>"Initial"</c>, so it was re-established on
/// every delivery: a saga persisted in a later state resumed at <c>"Initial"</c>, its handler lookup
/// missed, and the event was dropped. A multi-state process manager was broken on its second message.
/// </para>
/// <para>
/// <b>Why the documented remedy did not work either.</b> The base exposed a <c>protected virtual
/// CurrentStateName</c> and the documentation told consumers to override it so the position would be
/// persisted. The framework WROTE that property on transition and never READ it back, so a consumer who
/// followed the instruction exactly got a value faithfully stored and never loaded. The position is now
/// a member of <see cref="ProcessManagerState"/>, so there is one representation of it, it lives in the
/// persisted state, and the type system requires it — there is nothing to override and nothing to forget.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Restore the old shape — hold the position in a field on the manager instead of in
/// the state — and <see cref="Resume_at_the_state_it_transitioned_to_after_a_reload"/> goes RED while
/// <see cref="Start_at_Initial_when_the_state_is_new"/> stays GREEN. That split is the point: the second
/// arm is what a same-instance test could already see, and it is exactly what kept the defect invisible.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ProcessManagerPositionSurvivesReloadShould
{
	/// <summary>A saga that has never transitioned starts at the initial position.</summary>
	/// <remarks>
	/// The liveness half. Without it, the arm below is satisfied by a manager that reports some fixed
	/// non-initial string, and "the position survives" would be indistinguishable from "the position is
	/// always the same wrong thing".
	/// </remarks>
	[Fact]
	public void Start_at_Initial_when_the_state_is_new()
	{
		var manager = NewManager(new OrderData());

		manager.CurrentState.ShouldBe("Initial");
	}

	/// <summary>
	/// A saga reloaded into a FRESH instance resumes at the position it transitioned to.
	/// </summary>
	[Fact]
	public void Resume_at_the_state_it_transitioned_to_after_a_reload()
	{
		// The first delivery: transition, then let the instance go, exactly as the coordinator does.
		var data = new OrderData();
		var first = NewManager(data);
		first.MoveTo("Shipping");
		first.CurrentState.ShouldBe("Shipping", "the transition must take effect on the live instance");

		// The store round-trip. A real store serialises and materialises; carrying the same state object
		// is the weaker form of that and is still sufficient here, because the defect was that the
		// position was NOT IN the state object at all -- it lived on the manager and died with it.
		var reloaded = NewManager(data);

		reloaded.CurrentState.ShouldBe(
			"Shipping",
			"a process manager is constructed fresh for every delivered message, so a position that does "
			+ "not travel in the saga state is lost on the next message. Resuming at Initial is how a "
			+ "multi-state saga silently stops handling its own events.");
	}

	/// <summary>The position a reloaded saga resumes at is the one that was persisted.</summary>
	/// <remarks>
	/// Binds the representation, not just the behaviour: the value must be IN the state, so any store
	/// that persists the state persists the position, with no per-provider work and nothing to override.
	/// </remarks>
	[Fact]
	public void Carry_the_position_in_the_persisted_state()
	{
		var data = new OrderData();
		var manager = NewManager(data);

		manager.MoveTo("Shipping");

		data.CurrentStateName.ShouldBe(
			"Shipping",
			"the position is part of the saga state, which is what makes it survive without a consumer "
			+ "having to override anything");
	}

	private static OrderProcess NewManager(OrderData data) =>
		new(data, A.Fake<IDispatcher>(), NullLogger.Instance);

	private sealed class OrderData : ProcessManagerState;

	private sealed class OrderProcess : ProcessManager<OrderData>
	{
		public OrderProcess(OrderData state, IDispatcher dispatcher, Microsoft.Extensions.Logging.ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s.When<Placed>(_ => { }));
			During("Shipping", s => s.When<Shipped>(_ => { }));
		}

		/// <summary>Exposes the protected transition so the arm can drive it.</summary>
		/// <param name="stateName">The state to move to.</param>
		public void MoveTo(string stateName) => TransitionTo(stateName);
	}

	private sealed record Placed(string OrderId);

	private sealed record Shipped(string TrackingNumber);
}
