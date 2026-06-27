// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the event-sourcing metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/>. Carries aggregate, stream and event positioning
/// fields. Holds at most ten properties to satisfy the Microsoft-first focused-value-type design
/// guideline.
/// </remarks>
public readonly record struct MessageEventSourcing
{
	/// <summary>
	/// Gets the identifier of the aggregate for event sourcing.
	/// </summary>
	/// <value> The aggregate identifier or <see langword="null"/>. </value>
	public string? AggregateId { get; init; }

	/// <summary>
	/// Gets the type of the aggregate for event sourcing.
	/// </summary>
	/// <value> The aggregate type or <see langword="null"/>. </value>
	public string? AggregateType { get; init; }

	/// <summary>
	/// Gets the version of the aggregate for event sourcing.
	/// </summary>
	/// <value> The aggregate version or <see langword="null"/>. </value>
	public long? AggregateVersion { get; init; }

	/// <summary>
	/// Gets the name of the event stream.
	/// </summary>
	/// <value> The stream name or <see langword="null"/>. </value>
	public string? StreamName { get; init; }

	/// <summary>
	/// Gets the position within the event stream.
	/// </summary>
	/// <value> The stream position or <see langword="null"/>. </value>
	public long? StreamPosition { get; init; }

	/// <summary>
	/// Gets the global position in the event store.
	/// </summary>
	/// <value> The global position or <see langword="null"/>. </value>
	public long? GlobalPosition { get; init; }

	/// <summary>
	/// Gets the type of the event for event sourcing.
	/// </summary>
	/// <value> The event type or <see langword="null"/>. </value>
	public string? EventType { get; init; }

	/// <summary>
	/// Gets the version of the event for event sourcing.
	/// </summary>
	/// <value> The event version or <see langword="null"/>. </value>
	public int? EventVersion { get; init; }
}
