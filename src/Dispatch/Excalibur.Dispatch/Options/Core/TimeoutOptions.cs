// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for timeout middleware.
/// </summary>
public sealed class TimeoutOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether timeout handling is enabled.
	/// </summary>
	/// <value> <see langword="true" /> to enable timeout handling; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the default timeout for message processing.
	/// </summary>
	/// <value> The default timeout duration applied to message processing. </value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the timeout for specific message types.
	/// </summary>
	/// <value> The mapping of message types to custom timeout durations. </value>
	public IDictionary<string, TimeSpan> MessageTypeTimeouts { get; } = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets a value indicating whether to throw an exception on timeout.
	/// </summary>
	/// <value> <see langword="true" /> to throw exceptions when timeouts occur; otherwise, <see langword="false" />. </value>
	public bool ThrowOnTimeout { get; set; } = true;
}
