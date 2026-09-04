// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// A domain event together with the name it is stored under.
/// </summary>
/// <param name="Event">The event being written.</param>
/// <param name="EventTypeName">The declared name the event is stored under.</param>
public readonly record struct NamedEvent(IDomainEvent Event, string EventTypeName);

/// <summary>
/// Names events for storage, once, before any of them is written.
/// </summary>
public static class NamedEventExtensions
{
	/// <summary>
	/// Pairs each event with the name it is stored under.
	/// </summary>
	/// <param name="events">The events about to be written.</param>
	/// <returns>The events in the order given, each carrying its stored name.</returns>
	/// <remarks>
	/// <para>
	/// A store persists the name it is handed rather than working one out. Ten stores each deriving
	/// their own agreed by convention rather than by construction -- they matched because they were
	/// changed together, and an inventory of the places that name a message has already missed
	/// several.
	/// </para>
	/// <para>
	/// Every event is named before any is written, so a batch containing one undeclared event fails
	/// before the store has persisted part of it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">An event's type declares no name.</exception>
	public static IReadOnlyList<NamedEvent> AsNamedEvents(this IEnumerable<IDomainEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);

		var named = events is ICollection<IDomainEvent> collection
			? new List<NamedEvent>(collection.Count)
			: [];

		foreach (var @event in events)
		{
			ArgumentNullException.ThrowIfNull(@event);
			named.Add(new NamedEvent(@event, MessageNameHelper.GetName(@event.GetType())));
		}

		return named;
	}
}
