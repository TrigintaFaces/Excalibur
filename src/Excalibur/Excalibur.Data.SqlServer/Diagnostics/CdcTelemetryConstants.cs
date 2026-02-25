// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Data.SqlServer.Diagnostics;

/// <summary>
/// Shared telemetry constants for CDC (Change Data Capture) instrumentation.
/// </summary>
public static class CdcTelemetryConstants
{
	/// <summary>
	/// The meter name for CDC metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Data.Cdc";

	/// <summary>
	/// The activity source name for CDC distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Data.Cdc";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for CDC tracing.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

	/// <summary>
	/// Shared <see cref="Meter"/> for CDC metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>Total CDC events processed successfully.</summary>
		public const string EventsProcessed = "excalibur.cdc.events.processed";

		/// <summary>Total CDC event processing failures.</summary>
		public const string EventsFailed = "excalibur.cdc.events.failed";

		/// <summary>Duration of batch processing in milliseconds.</summary>
		public const string BatchDuration = "excalibur.cdc.batch.duration";

		/// <summary>Number of events in a processed batch.</summary>
		public const string BatchSize = "excalibur.cdc.batch.size";
	}

	/// <summary>
	/// Tag names for CDC metrics.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>The capture instance (table) name.</summary>
		public const string CaptureInstance = "cdc.capture_instance";

		/// <summary>The CDC operation type (insert, update, delete).</summary>
		public const string Operation = "cdc.operation";

		/// <summary>The error type for failed events.</summary>
		public const string ErrorType = "error.type";
	}
}
