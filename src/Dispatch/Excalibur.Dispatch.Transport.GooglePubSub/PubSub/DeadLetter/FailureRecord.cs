// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Record of a single failure.
/// </summary>
public sealed class FailureRecord
{
	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	/// <value>
	/// The timestamp.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the exception.
	/// </summary>
	/// <value>
	/// The exception.
	/// </value>
	public Exception Exception { get; set; } = null!;

	/// <summary>
	/// Gets or sets the exception type.
	/// </summary>
	/// <value>
	/// The exception type.
	/// </value>
	public string ExceptionType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception message.
	/// </summary>
	/// <value>
	/// The exception message.
	/// </value>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stack trace hash.
	/// </summary>
	/// <value>
	/// The stack trace hash.
	/// </value>
	public string StackTraceHash { get; set; } = string.Empty;
}
