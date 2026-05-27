// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using ProofOfLife.Domain.Events;

namespace ProofOfLife.Domain;

/// <summary>
/// Todo item aggregate demonstrating event sourcing with the Excalibur framework.
/// </summary>
/// <remarks>
/// <para>
/// This is the simplest possible aggregate for the proof-of-life sample:
/// <list type="bullet">
/// <item><description><see cref="AggregateRoot{TKey}"/> base class with <see cref="Guid"/> key</description></item>
/// <item><description><see cref="RaiseEvent"/> for state transitions</description></item>
/// <item><description><see cref="ApplyEventInternal"/> with pattern matching (no reflection)</description></item>
/// <item><description>Business invariant enforcement (cannot complete a completed todo)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TodoAggregate : AggregateRoot<Guid>
{
	/// <summary>
	/// Parameterless constructor for rehydration from events.
	/// </summary>
	public TodoAggregate()
	{
	}

	/// <summary>
	/// Constructor with identifier for factory usage.
	/// </summary>
	public TodoAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the todo title.</summary>
	public string Title { get; private set; } = string.Empty;

	/// <summary>Gets whether the todo is completed.</summary>
	public bool IsCompleted { get; private set; }

	/// <summary>Gets when the todo was completed, if applicable.</summary>
	public DateTimeOffset? CompletedAt { get; private set; }

	/// <summary>
	/// Creates a new todo item.
	/// </summary>
	public static TodoAggregate Create(Guid id, string title)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);

		var todo = new TodoAggregate(id);
		todo.RaiseEvent(new TodoCreated(id, title));
		return todo;
	}

	/// <summary>
	/// Marks the todo as completed.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if already completed.</exception>
	public void Complete()
	{
		if (IsCompleted)
		{
			throw new InvalidOperationException("Todo is already completed.");
		}

		RaiseEvent(new TodoCompleted(Id));
	}

	/// <summary>
	/// Updates the todo title.
	/// </summary>
	public void UpdateTitle(string newTitle)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(newTitle);

		if (IsCompleted)
		{
			throw new InvalidOperationException("Cannot update a completed todo.");
		}

		RaiseEvent(new TodoTitleUpdated(Id, newTitle));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case TodoCreated e:
				Id = e.TodoId;
				Title = e.Title;
				break;
			case TodoCompleted:
				IsCompleted = true;
				CompletedAt = DateTimeOffset.UtcNow;
				break;
			case TodoTitleUpdated e:
				Title = e.NewTitle;
				break;
		}
	}
}
