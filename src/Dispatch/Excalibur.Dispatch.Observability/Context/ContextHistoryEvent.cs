// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents an event in context history.
/// </summary>
public sealed class ContextHistoryEvent
{
	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the event type.
	/// </summary>
	public required string EventType { get; set; }

	/// <summary>
	/// Gets or sets event details.
	/// </summary>
	public string? Details { get; set; }

	/// <summary>
	/// Gets or sets the pipeline stage.
	/// </summary>
	public string? Stage { get; set; }

	/// <summary>
	/// Gets or sets the thread ID.
	/// </summary>
	public int ThreadId { get; set; }

	/// <summary>
	/// Gets or sets the field count at this point.
	/// </summary>
	public int FieldCount { get; set; }

	/// <summary>
	/// Gets or sets the context size in bytes.
	/// </summary>
	public int SizeBytes { get; set; }
}
