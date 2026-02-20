// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents the history of a context.
/// </summary>
public sealed class ContextHistory
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets when tracking started.
	/// </summary>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets the list of history events.
	/// </summary>
	public required IList<ContextHistoryEvent> Events { get; init; }
}
