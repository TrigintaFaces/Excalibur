// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents an event that has not been processed within expected time.
/// </summary>
public sealed class LaggingEvent
{
	/// <summary>
	/// Gets the event identifier.
	/// </summary>
	/// <value>
	/// The event identifier.
	/// </value>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the aggregate identifier.
	/// </summary>
	/// <value>
	/// The aggregate identifier.
	/// </value>
	public required string AggregateId { get; init; }

	/// <summary>
	/// Gets the event type.
	/// </summary>
	/// <value>
	/// The event type.
	/// </value>
	public required string EventType { get; init; }

	/// <summary>
	/// Gets when the event was written to the write model.
	/// </summary>
	/// <value>
	/// When the event was written to the write model.
	/// </value>
	public required DateTimeOffset WriteModelTimestamp { get; init; }

	/// <summary>
	/// Gets the age of the unprocessed event.
	/// </summary>
	/// <value>
	/// The age of the unprocessed event.
	/// </value>
	public required TimeSpan Age { get; init; }

	/// <summary>
	/// Gets the projection types that haven't processed this event.
	/// </summary>
	/// <value>
	/// The projection types that haven't processed this event.
	/// </value>
	public required IReadOnlyList<string> PendingProjections { get; init; }

	/// <summary>
	/// Gets any error messages associated with processing attempts.
	/// </summary>
	/// <value>
	/// Any error messages associated with processing attempts.
	/// </value>
	public IReadOnlyList<string>? ErrorMessages { get; init; }
}
