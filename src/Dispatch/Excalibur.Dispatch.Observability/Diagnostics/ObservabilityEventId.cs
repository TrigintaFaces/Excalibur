// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Diagnostics;

/// <summary>
/// Event IDs for observability components (80000-80999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>80000-80099: Context Flow</item>
/// <item>80100-80199: Context Observability</item>
/// <item>80200-80299: Context Enrichment</item>
/// <item>80300-80399: Metrics Collection</item>
/// <item>80400-80499: Tracing</item>
/// <item>80500-80599: Health Checks</item>
/// </list>
/// </remarks>
public static class ObservabilityEventId
{
	// ========================================
	// 80000-80099: Context Flow
	// ========================================

	/// <summary>Context flow tracker created.</summary>
	public const int ContextFlowTrackerCreated = 80000;

	/// <summary>Context flow started.</summary>
	public const int ContextFlowStarted = 80001;

	/// <summary>Context flow completed.</summary>
	public const int ContextFlowCompleted = 80002;

	/// <summary>Context flow diagnostics collected.</summary>
	public const int ContextFlowDiagnosticsCollected = 80003;

	/// <summary>Context propagated.</summary>
	public const int ContextPropagated = 80004;

	/// <summary>Context correlation established.</summary>
	public const int ContextCorrelationEstablished = 80005;

	/// <summary>Null context attempted at stage.</summary>
	public const int NullContextAttempted = 80006;

	/// <summary>Context state recorded.</summary>
	public const int ContextStateRecorded = 80007;

	/// <summary>Context changes detected.</summary>
	public const int ContextChangesDetected = 80008;

	/// <summary>Context correlated across boundary.</summary>
	public const int ContextCorrelated = 80009;

	/// <summary>Correlate boundary argument error.</summary>
	public const int CorrelateBoundaryArgumentError = 80010;

	/// <summary>Context integrity check failed.</summary>
	public const int ContextIntegrityCheckFailed = 80011;

	/// <summary>Context field removed warning.</summary>
	public const int ContextFieldRemovedWarning = 80012;

	/// <summary>Context field changed debug.</summary>
	public const int ContextFieldChangedDebug = 80013;

	/// <summary>Snapshot cleanup completed.</summary>
	public const int SnapshotCleanup = 80014;

	/// <summary>Cleanup invalid operation error.</summary>
	public const int CleanupInvalidOperationError = 80015;

	/// <summary>Cleanup argument error.</summary>
	public const int CleanupArgumentError = 80016;

	/// <summary>Record state invalid operation.</summary>
	public const int RecordStateInvalidOperation = 80017;

	/// <summary>Record state argument error.</summary>
	public const int RecordStateArgument = 80018;

	/// <summary>Detect changes invalid operation.</summary>
	public const int DetectChangesInvalidOperation = 80019;

	/// <summary>Detect changes argument error.</summary>
	public const int DetectChangesArgument = 80020;

	/// <summary>Correlate boundary invalid operation.</summary>
	public const int CorrelateBoundaryInvalidOperation = 80021;

	// ========================================
	// 80100-80199: Context Observability
	// ========================================

	/// <summary>Context observability middleware executing.</summary>
	public const int ContextObservabilityExecuting = 80100;

	/// <summary>Context observed.</summary>
	public const int ContextObserved = 80101;

	/// <summary>Context snapshot captured.</summary>
	public const int ContextSnapshotCaptured = 80102;

	/// <summary>Context anomaly detected.</summary>
	public const int ContextAnomalyDetected = 80103;

	/// <summary>Context integrity validation failed.</summary>
	public const int ContextIntegrityValidationFailed = 80104;

	/// <summary>Pipeline stage exception.</summary>
	public const int PipelineStageException = 80105;

	/// <summary>Property capture invalid operation.</summary>
	public const int PropertyCaptureInvalidOperation = 80106;

	/// <summary>Property capture invalid argument.</summary>
	public const int PropertyCaptureInvalidArgument = 80107;

	/// <summary>Property capture parameter mismatch.</summary>
	public const int PropertyCaptureParameterMismatch = 80108;

	/// <summary>Critical field removed.</summary>
	public const int CriticalFieldRemoved = 80109;

	/// <summary>Tracked field modified.</summary>
	public const int TrackedFieldModified = 80110;

	/// <summary>Context size exceeded.</summary>
	public const int ContextSizeExceeded = 80111;

	/// <summary>Context flow diagnostic.</summary>
	public const int ContextFlowDiagnostic = 80112;

	/// <summary>History event logged.</summary>
	public const int HistoryEvent = 80113;

	/// <summary>Context anomaly logged.</summary>
	public const int ContextAnomalyLogged = 80114;

	// ========================================
	// 80200-80299: Context Enrichment
	// ========================================

	/// <summary>Context trace enricher created.</summary>
	public const int ContextTraceEnricherCreated = 80200;

	/// <summary>Context enriched with trace data.</summary>
	public const int ContextEnrichedWithTrace = 80201;

	/// <summary>Context enriched with metadata.</summary>
	public const int ContextEnrichedWithMetadata = 80202;

	/// <summary>Context enrichment completed.</summary>
	public const int ContextEnrichmentCompleted = 80203;

	/// <summary>Activity enriched with context.</summary>
	public const int ActivityEnriched = 80204;

	/// <summary>Trace link created.</summary>
	public const int TraceLinkCreated = 80205;

	/// <summary>Baggage propagated.</summary>
	public const int BaggagePropagated = 80206;

	/// <summary>Correlation ID extracted from baggage.</summary>
	public const int CorrelationIdExtracted = 80207;

	/// <summary>Causation ID extracted from baggage.</summary>
	public const int CausationIdExtracted = 80208;

	/// <summary>Tenant ID extracted from baggage.</summary>
	public const int TenantIdExtracted = 80209;

	/// <summary>Context extracted from baggage.</summary>
	public const int ContextExtracted = 80210;

	/// <summary>Context event added to span.</summary>
	public const int ContextEventAdded = 80211;

	/// <summary>Trace parent parse format error.</summary>
	public const int TraceParentParseFormatError = 80212;

	/// <summary>Trace parent parse argument error.</summary>
	public const int TraceParentParseArgumentError = 80213;

	/// <summary>Failed to enrich activity - invalid operation.</summary>
	public const int FailedEnrichOperationInvalid = 80214;

	/// <summary>Failed to enrich activity - invalid argument.</summary>
	public const int FailedEnrichArgument = 80215;

	/// <summary>Failed to link related trace.</summary>
	public const int FailedLinkRelatedTrace = 80216;

	/// <summary>Failed to link related trace - invalid argument.</summary>
	public const int FailedLinkRelatedTraceArgument = 80217;

	/// <summary>Failed to extract baggage - invalid operation.</summary>
	public const int FailedExtractBaggageInvalid = 80218;

	/// <summary>Failed to extract baggage - invalid argument.</summary>
	public const int FailedExtractBaggageArgument = 80219;

	// ========================================
	// 80300-80399: Metrics Collection
	// ========================================

	/// <summary>Local metrics collector created.</summary>
	public const int LocalMetricsCollectorCreated = 80300;

	/// <summary>Cloud metrics adapter created.</summary>
	public const int CloudMetricsAdapterCreated = 80301;

	/// <summary>Metrics recorded.</summary>
	public const int MetricsRecorded = 80302;

	/// <summary>Metrics flushed.</summary>
	public const int MetricsFlushed = 80303;

	/// <summary>Counter incremented.</summary>
	public const int CounterIncremented = 80304;

	/// <summary>Gauge set.</summary>
	public const int GaugeSet = 80305;

	/// <summary>Histogram recorded.</summary>
	public const int HistogramRecorded = 80306;

	/// <summary>Metrics export completed.</summary>
	public const int MetricsExportCompleted = 80307;

	// ========================================
	// 80400-80499: Tracing
	// ========================================

	/// <summary>Trace started.</summary>
	public const int TraceStarted = 80400;

	/// <summary>Trace completed.</summary>
	public const int TraceCompleted = 80401;

	/// <summary>Span started.</summary>
	public const int SpanStarted = 80402;

	/// <summary>Span completed.</summary>
	public const int SpanCompleted = 80403;

	/// <summary>Trace exported.</summary>
	public const int TraceExported = 80404;

	/// <summary>Trace sampled.</summary>
	public const int TraceSampled = 80405;

	/// <summary>Trace dropped.</summary>
	public const int TraceDropped = 80406;

	// ========================================
	// 80500-80599: Health Checks
	// ========================================

	/// <summary>Health check executed.</summary>
	public const int HealthCheckExecuted = 80500;

	/// <summary>Health check passed.</summary>
	public const int HealthCheckPassed = 80501;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 80502;

	/// <summary>Health status changed.</summary>
	public const int HealthStatusChanged = 80503;

	/// <summary>Readiness check executed.</summary>
	public const int ReadinessCheckExecuted = 80504;

	/// <summary>Liveness check executed.</summary>
	public const int LivenessCheckExecuted = 80505;

	// ========================================
	// 80600-80699: Sanitization
	// ========================================

	/// <summary>PII sanitization bypassed (IncludeRawPii=true in non-Development environment).</summary>
	public const int PiiSanitizationBypassed = 80600;

	/// <summary>Compliance telemetry sanitizer registered.</summary>
	public const int ComplianceSanitizerRegistered = 80601;

	/// <summary>Compliance sanitizer detected PII pattern in telemetry data.</summary>
	public const int CompliancePiiPatternDetected = 80602;
}
