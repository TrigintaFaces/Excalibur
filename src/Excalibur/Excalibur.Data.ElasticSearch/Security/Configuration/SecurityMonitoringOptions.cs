// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures security monitoring, threat detection, and alerting.
/// </summary>
public sealed class SecurityMonitoringOptions
{
	/// <summary>
	/// Gets a value indicating whether security monitoring is enabled.
	/// </summary>
	/// <value> True to enable real-time security monitoring, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether automated responses to threats are enabled.
	/// </summary>
	/// <value> True to enable automated threat response, false otherwise. </value>
	public bool AutomatedResponseEnabled { get; init; }

	/// <summary>
	/// Gets the monitoring interval.
	/// </summary>
	/// <value> The time interval between monitoring checks. </value>
	public TimeSpan MonitoringInterval { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets the failed login threshold.
	/// </summary>
	/// <value> The number of failed login attempts before triggering an alert. </value>
	public int FailedLoginThreshold { get; init; } = 5;

	/// <summary>
	/// Gets a value indicating whether to store alerts in Elasticsearch.
	/// </summary>
	/// <value> True to store generated alerts in Elasticsearch, false otherwise. </value>
	public bool StoreAlertsInElasticsearch { get; init; } = true;
}
