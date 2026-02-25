// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security incident.
/// </summary>
public sealed class SecurityIncident
{
	/// <summary>
	/// Gets or sets the unique identifier for this security incident.
	/// </summary>
	/// <value>
	/// The unique identifier for this security incident.
	/// </value>
	public Guid IncidentId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the security incident occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the security incident occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the type of security incident.
	/// </summary>
	/// <value>
	/// The type of security incident.
	/// </value>
	public string IncidentType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the severity level of the security incident.
	/// </summary>
	/// <value>
	/// The severity level of the security incident.
	/// </value>
	public string Severity { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the description of the security incident.
	/// </summary>
	/// <value>
	/// The description of the security incident.
	/// </value>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the user ID affected by this security incident.
	/// </summary>
	/// <value>
	/// The user ID affected by this security incident.
	/// </value>
	public string? AffectedUserId { get; set; }

	/// <summary>
	/// Gets or sets the source IP address from which the security incident originated.
	/// </summary>
	/// <value>
	/// The source IP address from which the security incident originated.
	/// </value>
	public string? SourceIpAddress { get; set; }

	/// <summary>
	/// Gets or sets the list of systems affected by this security incident.
	/// </summary>
	/// <value>
	/// The list of systems affected by this security incident.
	/// </value>
	public List<string> AffectedSystems { get; set; } = [];

	/// <summary>
	/// Gets or sets the response actions taken for this security incident.
	/// </summary>
	/// <value>
	/// The response actions taken for this security incident.
	/// </value>
	public List<string> ResponseActions { get; set; } = [];

	/// <summary>
	/// Gets or sets the resolution details for this security incident.
	/// </summary>
	/// <value>
	/// The resolution details for this security incident.
	/// </value>
	public string? Resolution { get; set; }

	/// <summary>
	/// Gets or sets additional metadata associated with this security incident.
	/// </summary>
	/// <value>
	/// Additional metadata associated with this security incident.
	/// </value>
	public Dictionary<string, object>? AdditionalData { get; set; }
}
