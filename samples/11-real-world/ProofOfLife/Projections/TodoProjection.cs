// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using ProofOfLife.Messages;

namespace ProofOfLife.Projections;

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

	/// <summary>Gets or sets the aggregate version at last update.</summary>
	public long Version { get; set; }

	/// <summary>
	/// Converts this projection to a DTO for API responses.
	/// </summary>
	public TodoDto ToDto() => new(Id, Title, IsCompleted, CompletedAt, Version);
}
