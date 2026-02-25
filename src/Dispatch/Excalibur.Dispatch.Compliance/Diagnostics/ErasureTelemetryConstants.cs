// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Compliance.Diagnostics;

/// <summary>
/// Shared telemetry constants for GDPR erasure instrumentation.
/// </summary>
public static class ErasureTelemetryConstants
{
	/// <summary>
	/// The meter name for erasure metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Compliance.Erasure";

	/// <summary>
	/// The activity source name for erasure distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Compliance.Erasure";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for erasure tracing.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

	/// <summary>
	/// Shared <see cref="Meter"/> for erasure metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>Total erasure requests submitted.</summary>
		public const string RequestsSubmitted = "dispatch.erasure.requests.submitted";

		/// <summary>Total erasure requests completed.</summary>
		public const string RequestsCompleted = "dispatch.erasure.requests.completed";

		/// <summary>Total erasure request failures.</summary>
		public const string RequestsFailed = "dispatch.erasure.requests.failed";

		/// <summary>Total erasure requests blocked by legal hold.</summary>
		public const string RequestsBlocked = "dispatch.erasure.requests.blocked";

		/// <summary>Total keys deleted via erasure.</summary>
		public const string KeysDeleted = "dispatch.erasure.keys.deleted";

		/// <summary>Duration of erasure execution in milliseconds.</summary>
		public const string ExecutionDuration = "dispatch.erasure.execution.duration";
	}

	/// <summary>
	/// Tag names for erasure metrics.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>The erasure scope (full, tenant, selective).</summary>
		public const string Scope = "erasure.scope";

		/// <summary>The erasure result status (scheduled, blocked, failed).</summary>
		public const string ResultStatus = "erasure.result_status";

		/// <summary>The error type for failed operations.</summary>
		public const string ErrorType = "error.type";
	}
}
