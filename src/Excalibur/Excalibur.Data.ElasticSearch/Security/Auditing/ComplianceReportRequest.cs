// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a compliance report request.
/// </summary>
public sealed class ComplianceReportRequest
{
	/// <summary>
	/// Gets or sets the compliance framework for the report.
	/// </summary>
	/// <value>
	/// The compliance framework for the report.
	/// </value>
	public ComplianceFramework Framework { get; set; }

	/// <summary>
	/// Gets or sets the start time for the compliance report period.
	/// </summary>
	/// <value>
	/// The start time for the compliance report period.
	/// </value>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for the compliance report period.
	/// </summary>
	/// <value>
	/// The end time for the compliance report period.
	/// </value>
	public DateTimeOffset EndTime { get; set; }
}
