// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Error statistics.
/// </summary>
public sealed class ErrorStatistics
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the error count.
	/// </summary>
	/// <value>
	/// The error count.
	/// </value>
	public int ErrorCount { get; set; }

	/// <summary>
	/// Gets or sets the first error timestamp.
	/// </summary>
	/// <value>
	/// The first error timestamp.
	/// </value>
	public DateTimeOffset FirstError { get; set; }

	/// <summary>
	/// Gets or sets the last error timestamp.
	/// </summary>
	/// <value>
	/// The last error timestamp.
	/// </value>
	public DateTimeOffset LastError { get; set; }

	/// <summary>
	/// Gets the error types.
	/// </summary>
	/// <value>
	/// The error types.
	/// </value>
	public Collection<string> ErrorTypes { get; } = [];
}
