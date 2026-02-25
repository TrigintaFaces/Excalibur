// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Diagnostic event for context flow.
/// </summary>
internal sealed class ContextFlowDiagnosticEvent
{
	/// <summary>
	/// Gets or sets the message identifier.
	/// </summary>
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier.
	/// </summary>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the pipeline stage.
	/// </summary>
	public required string Stage { get; set; }

	/// <summary>
	/// Gets or sets the event timestamp.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the elapsed time in milliseconds.
	/// </summary>
	public long ElapsedMilliseconds { get; set; }

	/// <summary>
	/// Gets or sets the field count before processing.
	/// </summary>
	public int FieldCountBefore { get; set; }

	/// <summary>
	/// Gets or sets the field count after processing.
	/// </summary>
	public int FieldCountAfter { get; set; }

	/// <summary>
	/// Gets or sets the size in bytes before processing.
	/// </summary>
	public int SizeBytesBefore { get; set; }

	/// <summary>
	/// Gets or sets the size in bytes after processing.
	/// </summary>
	public int SizeBytesAfter { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether integrity validation passed.
	/// </summary>
	public bool IntegrityValid { get; set; }
}
