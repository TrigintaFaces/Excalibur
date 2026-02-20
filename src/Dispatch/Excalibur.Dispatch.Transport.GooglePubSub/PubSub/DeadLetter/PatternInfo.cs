// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Information about a detected pattern.
/// </summary>
public sealed class PatternInfo
{
	/// <summary>
	/// Gets or sets the pattern.
	/// </summary>
	/// <value>
	/// The pattern.
	/// </value>
	public string Pattern { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the number of occurrences.
	/// </summary>
	/// <value>
	/// The number of occurrences.
	/// </value>
	public int Occurrences { get; set; }

	/// <summary>
	/// Gets or sets when the pattern was last seen.
	/// </summary>
	/// <value>
	/// When the pattern was last seen.
	/// </value>
	public DateTimeOffset LastSeen { get; set; }
}
