// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures security auditing and compliance reporting.
/// </summary>
public sealed class AuditOptions
{
	/// <summary>
	/// Gets a value indicating whether security auditing is enabled.
	/// </summary>
	/// <value> True to enable comprehensive security auditing, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to audit authentication events.
	/// </summary>
	/// <value> True to log all authentication attempts and results, false otherwise. </value>
	public bool AuditAuthentication { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to audit data access events.
	/// </summary>
	/// <value> True to log data access patterns and queries, false otherwise. </value>
	public bool AuditDataAccess { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to audit configuration changes.
	/// </summary>
	/// <value> True to log security configuration modifications, false otherwise. </value>
	public bool AuditConfigurationChanges { get; init; } = true;

	/// <summary>
	/// Gets the audit log retention period.
	/// </summary>
	/// <value> The time to retain audit logs for compliance purposes. Defaults to 7 years. </value>
	public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(2555); // 7 years

	/// <summary>
	/// Gets the compliance frameworks to support.
	/// </summary>
	/// <value> List of compliance standards to generate reports for. </value>
	public List<ComplianceFramework> ComplianceFrameworks { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether to ensure audit log integrity.
	/// </summary>
	/// <value> True to use cryptographic signatures for audit log integrity, false otherwise. </value>
	public bool EnsureLogIntegrity { get; init; } = true;

	/// <summary>
	/// Gets the maximum number of results to return from Elasticsearch queries used for
	/// audit report generation, integrity validation, and archival operations.
	/// </summary>
	/// <value> The maximum query result size. Defaults to 10,000. </value>
	public int MaxQueryResultSize { get; init; } = 10_000;
}
