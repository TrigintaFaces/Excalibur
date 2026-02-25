// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.AuditLogging.Diagnostics;

/// <summary>
/// Event IDs for audit logging components (93000-93999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>93000-93099: Audit Logging Core</item>
/// <item>93100-93199: Audit Event Writers</item>
/// <item>93200-93299: Audit Storage</item>
/// <item>93300-93399: Audit Query</item>
/// <item>93400-93499: SIEM Integration</item>
/// </list>
/// </remarks>
public static class AuditLoggingEventId
{
	// ========================================
	// 93000-93099: Audit Logging Core
	// ========================================

	/// <summary>Audit logger created.</summary>
	public const int AuditLoggerCreated = 93000;

	/// <summary>Audit event captured.</summary>
	public const int AuditEventCaptured = 93001;

	/// <summary>Audit event enriched.</summary>
	public const int AuditEventEnriched = 93002;

	/// <summary>Audit event filtered.</summary>
	public const int AuditEventFiltered = 93003;

	/// <summary>Audit event validated.</summary>
	public const int AuditEventValidated = 93004;

	/// <summary>Audit middleware executing.</summary>
	public const int AuditMiddlewareExecuting = 93005;

	// ========================================
	// 93100-93199: Audit Event Writers
	// ========================================

	/// <summary>Audit writer created.</summary>
	public const int AuditWriterCreated = 93100;

	/// <summary>Audit event written.</summary>
	public const int AuditEventWritten = 93101;

	/// <summary>Audit batch written.</summary>
	public const int AuditBatchWritten = 93102;

	/// <summary>Audit write failed.</summary>
	public const int AuditWriteFailed = 93103;

	/// <summary>Audit write retried.</summary>
	public const int AuditWriteRetried = 93104;

	// ========================================
	// 93200-93299: Audit Storage
	// ========================================

	/// <summary>SQL Server audit store created.</summary>
	public const int SqlServerAuditStoreCreated = 93200;

	/// <summary>Splunk audit adapter created.</summary>
	public const int SplunkAuditAdapterCreated = 93201;

	/// <summary>Datadog audit adapter created.</summary>
	public const int DatadogAuditAdapterCreated = 93202;

	/// <summary>Sentinel audit adapter created.</summary>
	public const int SentinelAuditAdapterCreated = 93203;

	/// <summary>Audit event stored.</summary>
	public const int AuditEventStored = 93204;

	/// <summary>Audit storage compacted.</summary>
	public const int AuditStorageCompacted = 93205;

	// ========================================
	// 93300-93399: Audit Query
	// ========================================

	/// <summary>Audit query service created.</summary>
	public const int AuditQueryServiceCreated = 93300;

	/// <summary>Audit query executed.</summary>
	public const int AuditQueryExecuted = 93301;

	/// <summary>Audit report generated.</summary>
	public const int AuditReportGenerated = 93302;

	/// <summary>Audit export completed.</summary>
	public const int AuditExportCompleted = 93303;

	/// <summary>Audit search completed.</summary>
	public const int AuditSearchCompleted = 93304;

	/// <summary>Audit integrity verification started.</summary>
	public const int AuditIntegrityVerificationStarted = 93305;

	/// <summary>Audit integrity verification completed.</summary>
	public const int AuditIntegrityVerificationCompleted = 93306;

	/// <summary>Audit integrity verification failed.</summary>
	public const int AuditIntegrityVerificationFailed = 93307;

	/// <summary>Audit log access denied.</summary>
	public const int AuditLogAccessDenied = 93308;

	/// <summary>Audit integrity verification access denied.</summary>
	public const int AuditIntegrityVerificationAccessDenied = 93309;

	/// <summary>Meta-audit logging failed.</summary>
	public const int MetaAuditLoggingFailed = 93310;

	/// <summary>Audit integrity verification error.</summary>
	public const int AuditIntegrityVerificationError = 93311;

	// ========================================
	// 93400-93499: SIEM Integration
	// ========================================

	/// <summary>SIEM connector created.</summary>
	public const int SiemConnectorCreated = 93400;

	/// <summary>SIEM event forwarded.</summary>
	public const int SiemEventForwarded = 93401;

	/// <summary>SIEM batch forwarded.</summary>
	public const int SiemBatchForwarded = 93402;

	/// <summary>SIEM connection established.</summary>
	public const int SiemConnectionEstablished = 93403;

	/// <summary>SIEM connection lost.</summary>
	public const int SiemConnectionLost = 93404;

	/// <summary>SIEM forward failed.</summary>
	public const int SiemForwardFailed = 93405;

	/// <summary>SIEM forward retried.</summary>
	public const int SiemForwardRetried = 93406;

	/// <summary>SIEM health check failed.</summary>
	public const int SiemHealthCheckFailed = 93407;

	// ========================================
	// 93500-93509: Audit Store Health Checks
	// ========================================

	/// <summary>Audit store health check passed.</summary>
	public const int AuditStoreHealthCheckPassed = 93500;

	/// <summary>Audit store health check degraded.</summary>
	public const int AuditStoreHealthCheckDegraded = 93501;

	/// <summary>Audit store health check failed.</summary>
	public const int AuditStoreHealthCheckFailed = 93502;
}
