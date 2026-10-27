// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using PublicApiTodo.Messages;

namespace PublicApiTodo.Projections;

/// <summary>
/// Read model (projection) for todo items.
/// Maintained by updating from domain events after each save.
/// </summary>
public sealed class TodoProjection
{
	/// <summary>Gets or sets the todo identifier.</summary>
	public Guid Id { get; set; }

	/// <summary>Gets or sets the todo title.</summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>Gets or sets whether the todo is completed.</summary>
	public bool IsCompleted { get; set; }

	/// <summary>Gets or sets when the todo was completed.</summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>Gets or sets this row's revision count.</summary>
	/// <remarks>
	/// A COUNT OF WRITES, not an aggregate version, and the distinction matters if you copy this
	/// sample. This read model is maintained by hand from the endpoints below rather than folded from
	/// events, so no event version reaches it — the endpoints hold a command result that carries the
	/// id and nothing about the stream. An event-sourced projection does the opposite: it implements
	/// <c>IProjection&lt;TKey&gt;</c> and ASSIGNS the version of the event it is folding, because a
	/// count of writes and the version a row reflects are different numbers. See the projections
	/// sample under <c>samples/09-advanced/persistence-patterns</c> for that shape.
	/// </remarks>
	public long Version { get; set; }

	/// <summary>
	/// Converts this projection to a DTO for API responses.
	/// </summary>
	public TodoDto ToDto() => new(Id, Title, IsCompleted, CompletedAt, Version);
}
