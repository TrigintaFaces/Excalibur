// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents an audit integrity request.
/// </summary>
public sealed class AuditIntegrityRequest
{
	/// <summary>
	/// Gets or sets the start time for the audit integrity check.
	/// </summary>
	/// <value> The start timestamp for the audit integrity time window. </value>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for the audit integrity check.
	/// </summary>
	/// <value> The end timestamp for the audit integrity time window. </value>
	public DateTimeOffset EndTime { get; set; }
}
