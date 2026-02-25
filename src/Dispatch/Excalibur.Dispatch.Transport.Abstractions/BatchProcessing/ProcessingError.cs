// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a processing error.
/// </summary>
public sealed class ProcessingError
{
	/// <summary>
	/// Gets or sets the error code.
	/// </summary>
	/// <value>The current <see cref="Code"/> value.</value>
	public string Code { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>The current <see cref="Message"/> value.</value>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the error severity.
	/// </summary>
	/// <value>The current <see cref="Severity"/> value.</value>
	public ErrorSeverity Severity { get; set; }

	/// <summary>
	/// Gets or sets when the error occurred.
	/// </summary>
	/// <value>The current <see cref="OccurredAt"/> value.</value>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the message ID if error is message-specific.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the exception.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; set; }
}
