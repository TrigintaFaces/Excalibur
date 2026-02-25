// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a snapshot of context state at a specific point in time.
/// </summary>
public sealed class ContextSnapshot
{
	/// <summary>
	/// Gets or sets the message ID this snapshot belongs to.
	/// </summary>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the pipeline stage where this snapshot was taken.
	/// </summary>
	public required string Stage { get; set; }

	/// <summary>
	/// Gets or sets when this snapshot was captured.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets the context field values at this point.
	/// </summary>
	public required IDictionary<string, object?> Fields { get; init; }

	/// <summary>
	/// Gets or sets the count of fields in the context.
	/// </summary>
	public int FieldCount { get; set; }

	/// <summary>
	/// Gets or sets the serialized size of the context in bytes.
	/// </summary>
	public int SizeBytes { get; set; }

	/// <summary>
	/// Gets additional metadata about this snapshot.
	/// </summary>
	public required IReadOnlyDictionary<string, object> Metadata { get; init; }
}
