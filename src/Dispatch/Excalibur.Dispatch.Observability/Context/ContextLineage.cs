// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents the complete lineage of a context through its lifecycle.
/// </summary>
public sealed class ContextLineage
{
	/// <summary>
	/// Gets or sets the correlation ID for this lineage.
	/// </summary>
	public required string CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the original message ID that started this lineage.
	/// </summary>
	public string? OriginMessageId { get; set; }

	/// <summary>
	/// Gets or sets when this lineage started.
	/// </summary>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets all snapshots in this lineage.
	/// </summary>
	public required IList<ContextSnapshot> Snapshots { get; init; }

	/// <summary>
	/// Gets service boundary transitions.
	/// </summary>
	public required IList<ServiceBoundaryTransition> ServiceBoundaries { get; init; }
}
