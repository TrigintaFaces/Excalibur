// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event args for compliance violation detection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComplianceViolationEventArgs" /> class.
/// </remarks>
/// <param name="framework"> The compliance framework that was violated. </param>
/// <param name="violationType"> The type of compliance violation that occurred. </param>
/// <param name="description"> The detailed description of the compliance violation. </param>
/// <param name="eventId"> The unique identifier for this compliance violation event. </param>
public sealed class ComplianceViolationEventArgs(
	ComplianceFramework framework,
	string violationType,
	string description,
	string eventId) : EventArgs
{
	/// <summary>
	/// Gets the compliance framework that was violated.
	/// </summary>
	/// <value>
	/// A ComplianceFramework enumeration value indicating which specific compliance standard or regulatory framework was violated
	/// during the auditing process.
	/// </value>
	public ComplianceFramework Framework { get; } = framework;

	/// <summary>
	/// Gets the type of compliance violation that occurred.
	/// </summary>
	/// <value>
	/// A string describing the specific category or type of compliance violation detected, providing classification details for
	/// security analysis and remediation.
	/// </value>
	public string ViolationType { get; } = violationType;

	/// <summary>
	/// Gets the detailed description of the compliance violation.
	/// </summary>
	/// <value>
	/// A string containing comprehensive information about the compliance violation, including context, affected resources, and
	/// relevant details for compliance auditing and remediation.
	/// </value>
	public string Description { get; } = description;

	/// <summary>
	/// Gets the unique identifier for this compliance violation event.
	/// </summary>
	/// <value>
	/// A string representing the unique event identifier used for tracking, correlation, and auditing purposes in compliance violation
	/// monitoring systems.
	/// </value>
	public string EventId { get; } = eventId;
}
