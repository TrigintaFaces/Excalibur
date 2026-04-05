// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Core auditing capabilities for Elasticsearch security: configuration access,
/// security activity auditing, and authentication event recording.
/// </summary>
/// <remarks>
/// <para>
/// This is the core sub-interface of <see cref="IElasticsearchSecurityAuditor"/>.
/// For recording data access, configuration changes, and security incidents,
/// see <see cref="IElasticsearchSecurityAuditorRecording"/>.
/// For event notifications, see <see cref="IElasticsearchSecurityAuditorEvents"/>.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityAuditorCore
{
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
}
