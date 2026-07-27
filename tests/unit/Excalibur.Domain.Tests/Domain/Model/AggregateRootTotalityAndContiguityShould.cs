// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Domain.Exceptions;
using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Regression locks for the structural totality + contiguity invariants enforced by
/// <see cref="AggregateRoot{TKey}.LoadFromHistory"/> (ev4w90). Non-vacuous: each asserts a throw the
/// pre-fix count-and-ignore implementation did NOT produce.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootTotalityAndContiguityShould
{
	[Fact]
	public void Throw_UnhandledDomainEventException_When_A_Replayed_Event_Has_No_Apply_Arm()
	{
		// A deliberately-missing switch arm: the aggregate handles Known but not Unknown. Replaying an
		// Unknown event (with a contiguous version) MUST throw rather than silently no-op while the version
		// still advances. RED on the pre-fix impl, which counted and ignored the unhandled event.
		var aggregate = new PartialAggregate();

		var ex = Should.Throw<UnhandledDomainEventException>(() =>
			aggregate.LoadFromHistory(new HistoricEvent[] { new(new UnknownEvent(), 0) }));

		ex.EventType.ShouldBe(typeof(UnknownEvent));
	}

	[Fact]
	public void Throw_EventStreamContiguityException_When_A_Version_Gap_Is_Replayed()
	{
		// Versions 0 then 2 (1 missing). The pre-fix impl counted (=> version 1) and applied the v2 payload
		// where v1 belonged with no error; contiguity now refuses the gapped stream. Versions are the
		// durable 0-based stream index.
		var aggregate = new PartialAggregate();

		var ex = Should.Throw<EventStreamContiguityException>(() =>
			aggregate.LoadFromHistory(new HistoricEvent[]
			{
				new(new KnownEvent(), 0),
				new(new KnownEvent(), 2),
			}));

		ex.ExpectedVersion.ShouldBe(1);
		ex.ActualVersion.ShouldBe(2);
	}

	[Fact]
	public void Replay_A_Contiguous_Stream_Of_Handled_Events_And_Set_Version_To_The_Last()
	{
		// Liveness dual of the two throw arms above: a well-formed stream MUST still replay. A guard that
		// refused everything would satisfy both throw arms and be caught only here.
		//
		// The event payload no longer carries a version at all — position lives only on the envelope
		// (the contiguous 0, 1, 2 below). Replay reads the envelope, so this stream is contiguous and
		// Version lands on 3.
		var aggregate = new PartialAggregate();

		aggregate.LoadFromHistory(new HistoricEvent[]
		{
			new(new KnownEvent(), 0),
			new(new KnownEvent(), 1),
			new(new KnownEvent(), 2),
		});

		aggregate.Applied.ShouldBe(3);
		aggregate.Version.ShouldBe(3);
	}

	private sealed class PartialAggregate : AggregateRoot
	{
		public int Applied { get; private set; }

		protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
		{
			KnownEvent => Handle(),
			_ => false,
		};

		private bool Handle()
		{
			Applied++;
			return true;
		}
	}

	private sealed record KnownEvent : DomainEvent;

	private sealed record UnknownEvent : DomainEvent;
}
