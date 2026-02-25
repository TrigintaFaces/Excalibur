// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Details about an error that occurred.
/// </summary>
public sealed class ErrorDetail
{
	/// <summary>
	/// Gets or sets the error timestamp.
	/// </summary>
	/// <value>
	/// The error timestamp.
	/// </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>
	/// The error message.
	/// </value>
	public required string Message { get; set; }

	/// <summary>
	/// Gets or sets the error type.
	/// </summary>
	/// <value>
	/// The error type.
	/// </value>
	public string? ErrorType { get; set; }

	/// <summary>
	/// Gets or sets the stack trace.
	/// </summary>
	/// <value>
	/// The stack trace.
	/// </value>
	public string? StackTrace { get; set; }

	/// <summary>
	/// Gets additional error context.
	/// </summary>
	/// <value>
	/// Additional error context.
	/// </value>
	public Dictionary<string, object> Context { get; } = [];
}
