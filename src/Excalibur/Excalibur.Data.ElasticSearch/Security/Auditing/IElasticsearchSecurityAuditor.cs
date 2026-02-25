// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for recording security audit events in Elasticsearch,
/// including authentication events, data access patterns, configuration changes,
/// and security incidents.
/// </summary>
/// <remarks>
/// <para>
/// This is the core auditing interface (5 methods). For reporting and compliance
/// queries, see <see cref="IElasticsearchSecurityAuditorReporting"/>. For maintenance
/// operations (integrity validation, archival), see <see cref="IElasticsearchSecurityAuditorMaintenance"/>.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityAuditor
{
	/// <summary>
	/// Occurs when a security event is successfully recorded.
	/// </summary>
	event EventHandler<SecurityEventRecordedEventArgs>? SecurityEventRecorded;

	/// <summary>
	/// Occurs when audit log integrity validation fails.
	/// </summary>
	event EventHandler<AuditIntegrityViolationEventArgs>? IntegrityViolationDetected;

	/// <summary>
	/// Occurs when audit log archiving is completed.
	/// </summary>
	event EventHandler<AuditArchiveCompletedEventArgs>? AuditArchiveCompleted;

	/// <summary>
	/// Gets the audit configuration settings currently in use.
	/// </summary>
	/// <value> The current audit configuration settings. </value>
	AuditOptions Configuration { get; }

	/// <summary>
	/// Gets a value indicating whether audit log integrity protection is enabled.
	/// </summary>
	/// <value> True if audit logs are cryptographically protected, false otherwise. </value>
	bool IntegrityProtectionEnabled { get; }

	/// <summary>
	/// Gets the supported compliance frameworks for reporting.
	/// </summary>
	/// <value> A collection of compliance frameworks supported by this auditor. </value>
	IReadOnlyCollection<ComplianceFramework> SupportedComplianceFrameworks { get; }

	/// <summary>
	/// Audits a security activity event for compliance and monitoring purposes.
	/// </summary>
	/// <param name="activityEvent"> The security activity event to audit. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous audit operation. </returns>
	Task AuditSecurityActivityAsync(SecurityActivityEvent activityEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Records an authentication event for security monitoring and compliance purposes.
	/// </summary>
	/// <param name="authenticationEvent"> The authentication event details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the event was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the authentication event is null. </exception>
	Task<bool> RecordAuthenticationEventAsync(
		AuthenticationEvent authenticationEvent,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a data access event for monitoring data usage patterns and detecting anomalies.
	/// </summary>
	/// <param name="dataAccessEvent"> The data access event details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the event was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the data access event is null. </exception>
	Task<bool> RecordDataAccessEventAsync(DataAccessEvent dataAccessEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Records a security configuration change for compliance and change tracking purposes.
	/// </summary>
	/// <param name="configurationEvent"> The configuration change event details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the event was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the configuration event is null. </exception>
	Task<bool> RecordConfigurationChangeAsync(ConfigurationChangeEvent configurationEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Records a security incident or violation for immediate attention and investigation.
	/// </summary>
	/// <param name="securityIncident"> The security incident details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the incident was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the security incident is null. </exception>
	Task<bool> RecordSecurityIncidentAsync(SecurityIncident securityIncident, CancellationToken cancellationToken);
}

/// <summary>
/// Extends <see cref="IElasticsearchSecurityAuditor"/> with reporting and compliance query capabilities.
/// </summary>
/// <remarks>
/// Consumers check for this capability via pattern matching:
/// <code>if (auditor is IElasticsearchSecurityAuditorReporting reporting) { ... }</code>
/// </remarks>
public interface IElasticsearchSecurityAuditorReporting
{
	/// <summary>
	/// Generates a comprehensive security audit report for the specified time period.
	/// </summary>
	/// <param name="reportRequest"> The audit report generation request with filters and parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the generated audit report with security events and
	/// compliance information.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when report generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the report request is null. </exception>
	Task<SecurityAuditReport> GenerateAuditReportAsync(AuditReportRequest reportRequest, CancellationToken cancellationToken);

	/// <summary>
	/// Generates a compliance report for specific regulatory frameworks.
	/// </summary>
	/// <param name="complianceRequest"> The compliance report generation request with framework specifications. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the generated compliance report formatted according to
	/// the specified regulatory requirements.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when compliance report generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the compliance request is null. </exception>
	Task<ComplianceReport> GenerateComplianceReportAsync(
		ComplianceReportRequest complianceRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Searches audit events based on specified criteria for investigation and analysis.
	/// </summary>
	/// <param name="searchRequest"> The audit search request with filters and criteria. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the matching audit events and search result metadata.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit search fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the search request is null. </exception>
	Task<AuditSearchResult> SearchAuditEventsAsync(AuditSearchRequest searchRequest, CancellationToken cancellationToken);
}

/// <summary>
/// Extends <see cref="IElasticsearchSecurityAuditor"/> with audit log maintenance capabilities
/// including integrity validation and event archival.
/// </summary>
/// <remarks>
/// Consumers check for this capability via pattern matching:
/// <code>if (auditor is IElasticsearchSecurityAuditorMaintenance maintenance) { ... }</code>
/// </remarks>
public interface IElasticsearchSecurityAuditorMaintenance
{
	/// <summary>
	/// Validates the integrity of audit logs to detect tampering or corruption.
	/// </summary>
	/// <param name="validationRequest"> The integrity validation request with parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the integrity validation result indicating whether audit
	/// logs are intact and trustworthy.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when integrity validation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the validation request is null. </exception>
	Task<AuditIntegrityResult> ValidateAuditIntegrityAsync(
		AuditIntegrityRequest validationRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Archives old audit events according to retention policies and compliance requirements.
	/// </summary>
	/// <param name="archiveRequest"> The archive request with retention criteria. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the archive operation result including the number of
	/// events archived and any errors encountered.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit archiving fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the archive request is null. </exception>
	Task<AuditArchiveResult> ArchiveAuditEventsAsync(AuditArchiveRequest archiveRequest, CancellationToken cancellationToken);
}
