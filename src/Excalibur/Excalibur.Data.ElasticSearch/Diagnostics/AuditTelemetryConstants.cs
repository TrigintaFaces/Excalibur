// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Data.ElasticSearch.Diagnostics;

/// <summary>
/// Shared telemetry constants for security audit instrumentation.
/// </summary>
public static class AuditTelemetryConstants
{
	/// <summary>
	/// The meter name for audit metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Data.Audit";

	/// <summary>
	/// The activity source name for audit distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Data.Audit";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for audit tracing.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

	/// <summary>
	/// Shared <see cref="Meter"/> for audit metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>Total audit events recorded.</summary>
		public const string EventsRecorded = "excalibur.audit.events.recorded";

		/// <summary>Total audit event recording failures.</summary>
		public const string EventsFailed = "excalibur.audit.events.failed";

		/// <summary>Duration of audit report generation in milliseconds.</summary>
		public const string ReportDuration = "excalibur.audit.report.duration";

		/// <summary>Number of events in a bulk store batch.</summary>
		public const string BulkStoreSize = "excalibur.audit.bulk_store.size";
	}

	/// <summary>
	/// Tag names for audit metrics.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>The audit event type (authentication, data_access, etc.).</summary>
		public const string EventType = "audit.event_type";

		/// <summary>The audit event severity (low, medium, high, critical).</summary>
		public const string Severity = "audit.severity";

		/// <summary>The error type for failed operations.</summary>
		public const string ErrorType = "error.type";
	}
}
