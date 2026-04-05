// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event notifications for Elasticsearch security auditing operations.
/// </summary>
/// <remarks>
/// <para>
/// This is a sub-interface of <see cref="IElasticsearchSecurityAuditor"/>.
/// For core auditing and authentication, see <see cref="IElasticsearchSecurityAuditorCore"/>.
/// For recording data access, configuration changes, and incidents,
/// see <see cref="IElasticsearchSecurityAuditorRecording"/>.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityAuditorEvents
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
}
