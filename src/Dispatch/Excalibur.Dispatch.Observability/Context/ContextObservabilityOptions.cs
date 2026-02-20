// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Configuration options for context flow observability.
/// </summary>
/// <remarks>
/// Uses sub-option objects for tracing, limits, fields, and export settings.
/// Root keeps core enable/disable flags; sub-options group related settings.
/// Follows the <c>ChannelConsumeOptions</c> composition pattern.
/// </remarks>
public sealed class ContextObservabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether context observability is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate context integrity at each stage.
	/// </summary>
	public bool ValidateContextIntegrity { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to fail processing when integrity validation fails.
	/// </summary>
	public bool FailOnIntegrityViolation { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to emit diagnostic events.
	/// </summary>
	public bool EmitDiagnosticEvents { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to capture error states.
	/// </summary>
	public bool CaptureErrorStates { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to capture custom items from the context.
	/// </summary>
	public bool CaptureCustomItems { get; set; } = true;

	/// <summary>
	/// Gets the tracing configuration options.
	/// </summary>
	/// <value>Tracing options including trace enrichment, baggage, and sensitive field patterns.</value>
	public ContextTracingOptions Tracing { get; } = new();

	/// <summary>
	/// Gets the limits configuration options.
	/// </summary>
	/// <value>Limits options including size thresholds, retention, and cardinality caps.</value>
	public ContextLimitsOptions Limits { get; } = new();

	/// <summary>
	/// Gets the field configuration options.
	/// </summary>
	/// <value>Field options including required, critical, and tracked field lists.</value>
	public ContextFieldOptions Fields { get; } = new();

	/// <summary>
	/// Gets the export configuration options.
	/// </summary>
	/// <value>Export options including OTLP, Prometheus, and Application Insights settings.</value>
	public ContextExportOptions Export { get; } = new();
}

/// <summary>
/// Tracing configuration options for context observability.
/// </summary>
/// <remarks>
/// Groups tracing-related settings: trace enrichment, baggage propagation,
/// mutation storage, and sensitive field filtering.
/// </remarks>
public sealed class ContextTracingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to include custom items in trace spans.
	/// </summary>
	public bool IncludeCustomItemsInTraces { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of custom items to include in traces.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxCustomItemsInTraces { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to include stack traces in error capture.
	/// </summary>
	public bool IncludeStackTraceInErrors { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to store mutations in the context.
	/// </summary>
	public bool StoreMutationsInContext { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include null fields in snapshots.
	/// </summary>
	public bool IncludeNullFields { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to preserve unknown baggage items.
	/// </summary>
	public bool PreserveUnknownBaggageItems { get; set; } = true;

	/// <summary>
	/// Gets or sets patterns for sensitive fields to exclude from traces.
	/// </summary>
	public string[]? SensitiveFieldPatterns { get; set; } =
	[
		"(?i)password",
		"(?i)secret",
		"(?i)token",
		"(?i)credential",
		"(?i)ssn",
		"(?i)credit.?card",
	];
}

/// <summary>
/// Limits configuration options for context observability.
/// </summary>
/// <remarks>
/// Groups size thresholds, retention periods, and cardinality caps.
/// </remarks>
public sealed class ContextLimitsOptions
{
	/// <summary>
	/// Gets or sets the maximum number of custom items to capture.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxCustomItemsToCapture { get; set; } = 20;

	/// <summary>
	/// Gets or sets the maximum context size in bytes.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxContextSizeBytes { get; set; } = 100_000; // 100KB

	/// <summary>
	/// Gets or sets a value indicating whether to fail when size threshold is exceeded.
	/// </summary>
	public bool FailOnSizeThresholdExceeded { get; set; }

	/// <summary>
	/// Gets or sets the snapshot retention period.
	/// </summary>
	public TimeSpan SnapshotRetentionPeriod { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum number of snapshots per lineage.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxSnapshotsPerLineage { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum history events per context.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxHistoryEventsPerContext { get; set; } = 50;

	/// <summary>
	/// Gets or sets the maximum anomaly queue size.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxAnomalyQueueSize { get; set; } = 1000;
}

/// <summary>
/// Field configuration options for context observability.
/// </summary>
/// <remarks>
/// Groups field-related settings: required, critical, and tracked field lists.
/// </remarks>
public sealed class ContextFieldOptions
{
	/// <summary>
	/// Gets or sets the required context fields for integrity validation.
	/// </summary>
	public string[]? RequiredContextFields { get; set; }

	/// <summary>
	/// Gets or sets critical fields that should never be removed.
	/// </summary>
	public string[]? CriticalFields { get; set; }

	/// <summary>
	/// Gets or sets fields to track for modifications.
	/// </summary>
	public string[]? TrackedFields { get; set; }
}

/// <summary>
/// Export configuration options for context observability.
/// </summary>
/// <remarks>
/// Groups telemetry export settings: OTLP, Prometheus, and Application Insights.
/// </remarks>
public sealed class ContextExportOptions
{
	/// <summary>
	/// Gets or sets the OpenTelemetry export endpoint.
	/// </summary>
	public string? OtlpEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the service name for telemetry.
	/// </summary>
	[Required]
	public string ServiceName { get; set; } = "Excalibur.Dispatch";

	/// <summary>
	/// Gets or sets the service version for telemetry.
	/// </summary>
	[Required]
	public string ServiceVersion { get; set; } = "1.0.0";

	/// <summary>
	/// Gets or sets a value indicating whether to export metrics to Prometheus.
	/// </summary>
	public bool ExportToPrometheus { get; set; } = true;

	/// <summary>
	/// Gets or sets the Prometheus scrape endpoint path.
	/// </summary>
	[Required]
	public string PrometheusScrapePath { get; set; } = "/metrics";

	/// <summary>
	/// Gets or sets a value indicating whether to export to Application Insights.
	/// </summary>
	public bool ExportToApplicationInsights { get; set; }

	/// <summary>
	/// Gets or sets the Application Insights connection string.
	/// </summary>
	public string? ApplicationInsightsConnectionString { get; set; }

	/// <summary>
	/// Gets custom resource attributes for telemetry.
	/// </summary>
	public IDictionary<string, object> ResourceAttributes { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
