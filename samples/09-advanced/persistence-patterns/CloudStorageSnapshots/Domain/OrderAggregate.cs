// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace CloudStorageSnapshots.Domain;

/// <summary>
/// Minimal aggregate used to illustrate the hot-to-cold tiered-storage flow.
/// The aggregate accumulates a running note log so an archive cycle has enough
/// events to move to cold storage.
/// </summary>
public sealed class OrderAggregate : AggregateRoot<Guid>
{
	private readonly List<string> _notes = [];

	/// <summary>Initializes a new instance for rehydration.</summary>
	public OrderAggregate()
	{
	}

	/// <summary>Initializes a new instance with an identifier.</summary>
	public OrderAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the notes appended to this order.</summary>
	public IReadOnlyList<string> Notes => _notes.AsReadOnly();

	/// <summary>Creates a new order and raises <see cref="OrderCreated"/>.</summary>
	public static OrderAggregate Create(Guid id)
	{
		var order = new OrderAggregate(id);
		order.RaiseEvent(new OrderCreated(id));
		return order;
	}

	/// <summary>Appends a note, raising <see cref="OrderNoteAppended"/>.</summary>
	public void AppendNote(string note)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(note);
		RaiseEvent(new OrderNoteAppended(Id, note));
	}

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case OrderCreated c: Id = c.OrderId; break;
			case OrderNoteAppended n: _notes.Add(n.Note); break;
		}
	}
}

/// <summary>Raised when an <see cref="OrderAggregate"/> is created.</summary>
public sealed record OrderCreated(Guid OrderId) : DomainEvent;

/// <summary>Raised when a note is appended to an <see cref="OrderAggregate"/>.</summary>
public sealed record OrderNoteAppended(Guid OrderId, string Note) : DomainEvent;
