// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Shared telemetry constants for streaming handler instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// Provides a shared <see cref="System.Diagnostics.Metrics.Meter"/> and
/// <see cref="System.Diagnostics.ActivitySource"/> for streaming document handlers.
/// Metric and tag names follow OpenTelemetry semantic conventions.
/// </para>
/// </remarks>
public static class StreamingHandlerTelemetryConstants
{
	/// <summary>
	/// The meter name for streaming handler metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Streaming";

	/// <summary>
	/// The activity source name for streaming handler distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Streaming";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for streaming handler tracing.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

	/// <summary>
	/// Shared <see cref="Meter"/> for streaming handler metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>Total documents processed by streaming handlers.</summary>
		public const string DocumentsProcessed = "dispatch.streaming.documents.processed";

		/// <summary>Total streaming document processing failures.</summary>
		public const string DocumentsFailed = "dispatch.streaming.documents.failed";

		/// <summary>Duration of document processing in milliseconds.</summary>
		public const string ProcessingDuration = "dispatch.streaming.processing.duration";

		/// <summary>Number of chunks produced per document.</summary>
		public const string ChunksProduced = "dispatch.streaming.chunks.produced";
	}

	/// <summary>
	/// Tag names for streaming handler metrics.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>The handler type (fully qualified class name).</summary>
		public const string HandlerType = "streaming.handler.type";

		/// <summary>The document type being processed.</summary>
		public const string DocumentType = "streaming.document.type";

		/// <summary>The error type for failed operations.</summary>
		public const string ErrorType = "error.type";
	}
}
