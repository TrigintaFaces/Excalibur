// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a change detected in context fields.
/// </summary>
public sealed class ContextChange
{
	/// <summary>
	/// Gets or sets the name of the field that changed.
	/// </summary>
	public required string FieldName { get; set; }

	/// <summary>
	/// Gets or sets the type of change.
	/// </summary>
	public ContextChangeType ChangeType { get; set; }

	/// <summary>
	/// Gets or sets the previous value.
	/// </summary>
	public object? FromValue { get; set; }

	/// <summary>
	/// Gets or sets the new value.
	/// </summary>
	public object? ToValue { get; set; }

	/// <summary>
	/// Gets or sets the stage where the change was detected.
	/// </summary>
	public required string Stage { get; set; }

	/// <summary>
	/// Gets or sets when the change was detected.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }
}
