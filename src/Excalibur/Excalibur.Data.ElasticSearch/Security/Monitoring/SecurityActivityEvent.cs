// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security activity event.
/// </summary>
public sealed class SecurityActivityEvent
{
	/// <summary>
	/// Gets or sets the type of security activity that occurred.
	/// </summary>
	/// <value> A string describing the nature of the security activity. </value>
	public string ActivityType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the identifier of the user who performed the security activity.
	/// </summary>
	/// <value> The user ID or username associated with the activity. </value>
	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the security activity occurred.
	/// </summary>
	/// <value> The date and time when the activity took place. </value>
	public DateTimeOffset Timestamp { get; set; }
}
