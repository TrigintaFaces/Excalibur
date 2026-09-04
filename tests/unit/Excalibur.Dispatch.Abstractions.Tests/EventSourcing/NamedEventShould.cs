// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Locks the single point at which an event acquires the name it is stored under.
/// </summary>
/// <remarks>
/// Ten event stores used to derive this for themselves. They agreed because they were changed
/// together, not because anything made them agree, and an inventory of the places that name a message
/// had already missed several.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class NamedEventShould
{
	[MessageName("Test.NamedEvent.First")]
	private sealed record FirstEvent : IDomainEvent
	{
		public string EventId => "1";
		public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
		public IDictionary<string, object>? Metadata => null;
	}

	[MessageName("Test.NamedEvent.Second")]
	private sealed record SecondEvent : IDomainEvent
	{
		public string EventId => "2";
		public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
		public IDictionary<string, object>? Metadata => null;
	}

	[Fact]
	public void GiveEachEventItsDeclaredName()
	{
		IDomainEvent[] events = [new FirstEvent(), new SecondEvent()];

		var named = events.AsNamedEvents();

		named.Select(static n => n.EventTypeName)
			.ShouldBe(["Test.NamedEvent.First", "Test.NamedEvent.Second"]);
	}

	[Fact]
	public void KeepTheOrderItWasGiven()
	{
		// Stores zip this against a version counter, so a reordering would attach each event to the
		// wrong version -- silently, and only in a batch of more than one.
		IDomainEvent[] events = [new SecondEvent(), new FirstEvent(), new SecondEvent()];

		var named = events.AsNamedEvents();

		named.Select(static n => n.Event).ShouldBe(events);
	}

	[Fact]
	public void NameEveryEventBeforeAnyIsWritten()
	{
		// A store appends as it walks this list. Naming lazily would let it persist the first event
		// and then throw on the second, leaving a partial batch behind.
		IDomainEvent[] events = [new FirstEvent(), new UndeclaredDomainEventFixture("3")];

		_ = Should.Throw<InvalidOperationException>(() => events.AsNamedEvents());
	}

	[Fact]
	public void DeconstructIntoTheEventAndItsName()
	{
		// The shape every store's append loop now uses.
		var (@event, name) = new NamedEvent(new FirstEvent(), "Test.NamedEvent.First");

		@event.ShouldBeOfType<FirstEvent>();
		name.ShouldBe("Test.NamedEvent.First");
	}

	[Fact]
	public void AcceptAnEmptyBatch()
	{
		Array.Empty<IDomainEvent>().AsNamedEvents().ShouldBeEmpty();
	}

	[Fact]
	public void RejectANullBatch()
	{
		IEnumerable<IDomainEvent> events = null!;

		_ = Should.Throw<ArgumentNullException>(() => events.AsNamedEvents());
	}
}
