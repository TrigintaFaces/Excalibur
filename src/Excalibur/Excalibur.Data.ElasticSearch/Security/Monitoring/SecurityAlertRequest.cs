// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security alert request.
/// </summary>
public sealed class SecurityAlertRequest
{
	/// <summary>
	/// Gets or sets the unique request identifier.
	/// </summary>
	/// <value>
	/// The unique request identifier.
	/// </value>
	public string RequestId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the alert criteria for filtering alerts.
	/// </summary>
	/// <value>
	/// The alert criteria for filtering alerts.
	/// </value>
	public AlertCriteria Criteria { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to automatically distribute alerts.
	/// </summary>
	/// <value>
	/// A value indicating whether to automatically distribute alerts.
	/// </value>
	public bool AutoDistribute { get; set; }

	/// <summary>
	/// Gets or sets the minimum severity level for alerts to be included in the request.
	/// </summary>
	/// <value>
	/// A SecurityRiskLevel enumeration value that determines the lowest severity threshold for security alerts to be processed by this request.
	/// </value>
	public SecurityRiskLevel MinimumSeverity { get; set; }

	/// <summary>
	/// Gets or sets the start time for the alert query time range.
	/// </summary>
	/// <value> A DateTimeOffset representing the earliest timestamp for alerts to be included in the security alert request results. </value>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for the alert query time range.
	/// </summary>
	/// <value> A DateTimeOffset representing the latest timestamp for alerts to be included in the security alert request results. </value>
	public DateTimeOffset EndTime { get; set; }

	/// <summary>
	/// Represents criteria for generating security alerts.
	/// </summary>
	public sealed class AlertCriteria
	{
		/// <summary>
		/// Gets or sets the minimum risk level for filtering security alerts.
		/// </summary>
		/// <value>
		/// A nullable SecurityRiskLevel that specifies the minimum severity threshold for including alerts in the results. If null, no
		/// risk level filtering is applied.
		/// </value>
		public SecurityRiskLevel? MinimumRiskLevel { get; set; }

		/// <summary>
		/// Gets or sets the list of event types to include in the security alert filtering.
		/// </summary>
		/// <value>
		/// A collection of event type names that determines which categories of security events should be included in the alert
		/// generation process.
		/// </value>
		public List<string> EventTypes { get; set; } = [];

		/// <summary>
		/// Gets or sets the list of target systems to monitor for security alerts.
		/// </summary>
		/// <value>
		/// A collection of system identifiers or names that specifies which systems should be monitored for security incidents and
		/// alert generation.
		/// </value>
		public List<string> TargetSystems { get; set; } = [];

		/// <summary>
		/// Gets or sets a value indicating whether anomaly detection events should be included in the security alerts.
		/// </summary>
		/// <value> True if anomaly detection events should be included in the security alert criteria; otherwise, false. </value>
		public bool IncludeAnomalies { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether threat detection events should be included in the security alerts.
		/// </summary>
		/// <value> True if threat detection events should be included in the security alert criteria; otherwise, false. </value>
		public bool IncludeThreats { get; set; }
	}
}
