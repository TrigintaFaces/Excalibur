// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ObservabilityEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Observability")]
[Trait("Priority", "0")]
public sealed class ObservabilityEventIdShould : UnitTestBase
{
	#region Context Flow Event ID Tests (80000-80099)

	[Fact]
	public void HaveContextFlowTrackerCreatedInFlowRange()
	{
		ObservabilityEventId.ContextFlowTrackerCreated.ShouldBe(80000);
	}

	[Fact]
	public void HaveContextFlowStartedInFlowRange()
	{
		ObservabilityEventId.ContextFlowStarted.ShouldBe(80001);
	}

	[Fact]
	public void HaveContextFlowCompletedInFlowRange()
	{
		ObservabilityEventId.ContextFlowCompleted.ShouldBe(80002);
	}

	[Fact]
	public void HaveContextPropagatedInFlowRange()
	{
		ObservabilityEventId.ContextPropagated.ShouldBe(80004);
	}

	[Fact]
	public void HaveAllContextFlowEventIdsInExpectedRange()
	{
		ObservabilityEventId.ContextFlowTrackerCreated.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextFlowStarted.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextFlowCompleted.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextFlowDiagnosticsCollected.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextPropagated.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextCorrelationEstablished.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.CorrelateBoundaryInvalidOperation.ShouldBeInRange(80000, 80099);
	}

	#endregion

	#region Context Observability Event ID Tests (80100-80199)

	[Fact]
	public void HaveContextObservabilityExecutingInObservabilityRange()
	{
		ObservabilityEventId.ContextObservabilityExecuting.ShouldBe(80100);
	}

	[Fact]
	public void HaveContextObservedInObservabilityRange()
	{
		ObservabilityEventId.ContextObserved.ShouldBe(80101);
	}

	[Fact]
	public void HaveContextSnapshotCapturedInObservabilityRange()
	{
		ObservabilityEventId.ContextSnapshotCaptured.ShouldBe(80102);
	}

	[Fact]
	public void HaveContextAnomalyDetectedInObservabilityRange()
	{
		ObservabilityEventId.ContextAnomalyDetected.ShouldBe(80103);
	}

	[Fact]
	public void HaveAllObservabilityEventIdsInExpectedRange()
	{
		ObservabilityEventId.ContextObservabilityExecuting.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextObserved.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextSnapshotCaptured.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextAnomalyDetected.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextIntegrityValidationFailed.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextAnomalyLogged.ShouldBeInRange(80100, 80199);
	}

	#endregion

	#region Context Enrichment Event ID Tests (80200-80299)

	[Fact]
	public void HaveContextTraceEnricherCreatedInEnrichmentRange()
	{
		ObservabilityEventId.ContextTraceEnricherCreated.ShouldBe(80200);
	}

	[Fact]
	public void HaveContextEnrichedWithTraceInEnrichmentRange()
	{
		ObservabilityEventId.ContextEnrichedWithTrace.ShouldBe(80201);
	}

	[Fact]
	public void HaveActivityEnrichedInEnrichmentRange()
	{
		ObservabilityEventId.ActivityEnriched.ShouldBe(80204);
	}

	[Fact]
	public void HaveAllEnrichmentEventIdsInExpectedRange()
	{
		ObservabilityEventId.ContextTraceEnricherCreated.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.ContextEnrichedWithTrace.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.ContextEnrichedWithMetadata.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.ContextEnrichmentCompleted.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.ActivityEnriched.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.TraceLinkCreated.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.FailedExtractBaggageArgument.ShouldBeInRange(80200, 80299);
	}

	#endregion

	#region Metrics Collection Event ID Tests (80300-80399)

	[Fact]
	public void HaveLocalMetricsCollectorCreatedInMetricsRange()
	{
		ObservabilityEventId.LocalMetricsCollectorCreated.ShouldBe(80300);
	}

	[Fact]
	public void HaveCloudMetricsAdapterCreatedInMetricsRange()
	{
		ObservabilityEventId.CloudMetricsAdapterCreated.ShouldBe(80301);
	}

	[Fact]
	public void HaveMetricsRecordedInMetricsRange()
	{
		ObservabilityEventId.MetricsRecorded.ShouldBe(80302);
	}

	[Fact]
	public void HaveAllMetricsEventIdsInExpectedRange()
	{
		ObservabilityEventId.LocalMetricsCollectorCreated.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.CloudMetricsAdapterCreated.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.MetricsRecorded.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.MetricsFlushed.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.CounterIncremented.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.GaugeSet.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.HistogramRecorded.ShouldBeInRange(80300, 80399);
		ObservabilityEventId.MetricsExportCompleted.ShouldBeInRange(80300, 80399);
	}

	#endregion

	#region Tracing Event ID Tests (80400-80499)

	[Fact]
	public void HaveTraceStartedInTracingRange()
	{
		ObservabilityEventId.TraceStarted.ShouldBe(80400);
	}

	[Fact]
	public void HaveTraceCompletedInTracingRange()
	{
		ObservabilityEventId.TraceCompleted.ShouldBe(80401);
	}

	[Fact]
	public void HaveSpanStartedInTracingRange()
	{
		ObservabilityEventId.SpanStarted.ShouldBe(80402);
	}

	[Fact]
	public void HaveAllTracingEventIdsInExpectedRange()
	{
		ObservabilityEventId.TraceStarted.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.TraceCompleted.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.SpanStarted.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.SpanCompleted.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.TraceExported.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.TraceSampled.ShouldBeInRange(80400, 80499);
		ObservabilityEventId.TraceDropped.ShouldBeInRange(80400, 80499);
	}

	#endregion

	#region Health Checks Event ID Tests (80500-80599)

	[Fact]
	public void HaveHealthCheckExecutedInHealthCheckRange()
	{
		ObservabilityEventId.HealthCheckExecuted.ShouldBe(80500);
	}

	[Fact]
	public void HaveHealthCheckPassedInHealthCheckRange()
	{
		ObservabilityEventId.HealthCheckPassed.ShouldBe(80501);
	}

	[Fact]
	public void HaveHealthCheckFailedInHealthCheckRange()
	{
		ObservabilityEventId.HealthCheckFailed.ShouldBe(80502);
	}

	[Fact]
	public void HaveAllHealthCheckEventIdsInExpectedRange()
	{
		ObservabilityEventId.HealthCheckExecuted.ShouldBeInRange(80500, 80599);
		ObservabilityEventId.HealthCheckPassed.ShouldBeInRange(80500, 80599);
		ObservabilityEventId.HealthCheckFailed.ShouldBeInRange(80500, 80599);
		ObservabilityEventId.HealthStatusChanged.ShouldBeInRange(80500, 80599);
		ObservabilityEventId.ReadinessCheckExecuted.ShouldBeInRange(80500, 80599);
		ObservabilityEventId.LivenessCheckExecuted.ShouldBeInRange(80500, 80599);
	}

	#endregion

	#region Observability Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInObservabilityReservedRange()
	{
		// Observability reserved range is 80000-80999
		var allEventIds = GetAllObservabilityEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(80000, 80999,
				$"Event ID {eventId} is outside Observability reserved range (80000-80999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllObservabilityEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllObservabilityEventIds();
		allEventIds.Length.ShouldBeGreaterThan(50);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllObservabilityEventIds()
	{
		return
		[
			// Context Flow (80000-80099)
			ObservabilityEventId.ContextFlowTrackerCreated,
			ObservabilityEventId.ContextFlowStarted,
			ObservabilityEventId.ContextFlowCompleted,
			ObservabilityEventId.ContextFlowDiagnosticsCollected,
			ObservabilityEventId.ContextPropagated,
			ObservabilityEventId.ContextCorrelationEstablished,
			ObservabilityEventId.NullContextAttempted,
			ObservabilityEventId.ContextStateRecorded,
			ObservabilityEventId.ContextChangesDetected,
			ObservabilityEventId.ContextCorrelated,
			ObservabilityEventId.CorrelateBoundaryArgumentError,
			ObservabilityEventId.ContextIntegrityCheckFailed,
			ObservabilityEventId.ContextFieldRemovedWarning,
			ObservabilityEventId.ContextFieldChangedDebug,
			ObservabilityEventId.SnapshotCleanup,
			ObservabilityEventId.CleanupInvalidOperationError,
			ObservabilityEventId.CleanupArgumentError,
			ObservabilityEventId.RecordStateInvalidOperation,
			ObservabilityEventId.RecordStateArgument,
			ObservabilityEventId.DetectChangesInvalidOperation,
			ObservabilityEventId.DetectChangesArgument,
			ObservabilityEventId.CorrelateBoundaryInvalidOperation,

			// Context Observability (80100-80199)
			ObservabilityEventId.ContextObservabilityExecuting,
			ObservabilityEventId.ContextObserved,
			ObservabilityEventId.ContextSnapshotCaptured,
			ObservabilityEventId.ContextAnomalyDetected,
			ObservabilityEventId.ContextIntegrityValidationFailed,
			ObservabilityEventId.PipelineStageException,
			ObservabilityEventId.PropertyCaptureInvalidOperation,
			ObservabilityEventId.PropertyCaptureInvalidArgument,
			ObservabilityEventId.PropertyCaptureParameterMismatch,
			ObservabilityEventId.CriticalFieldRemoved,
			ObservabilityEventId.TrackedFieldModified,
			ObservabilityEventId.ContextSizeExceeded,
			ObservabilityEventId.ContextFlowDiagnostic,
			ObservabilityEventId.HistoryEvent,
			ObservabilityEventId.ContextAnomalyLogged,

			// Context Enrichment (80200-80299)
			ObservabilityEventId.ContextTraceEnricherCreated,
			ObservabilityEventId.ContextEnrichedWithTrace,
			ObservabilityEventId.ContextEnrichedWithMetadata,
			ObservabilityEventId.ContextEnrichmentCompleted,
			ObservabilityEventId.ActivityEnriched,
			ObservabilityEventId.TraceLinkCreated,
			ObservabilityEventId.BaggagePropagated,
			ObservabilityEventId.CorrelationIdExtracted,
			ObservabilityEventId.CausationIdExtracted,
			ObservabilityEventId.TenantIdExtracted,
			ObservabilityEventId.ContextExtracted,
			ObservabilityEventId.ContextEventAdded,
			ObservabilityEventId.TraceParentParseFormatError,
			ObservabilityEventId.TraceParentParseArgumentError,
			ObservabilityEventId.FailedEnrichOperationInvalid,
			ObservabilityEventId.FailedEnrichArgument,
			ObservabilityEventId.FailedLinkRelatedTrace,
			ObservabilityEventId.FailedLinkRelatedTraceArgument,
			ObservabilityEventId.FailedExtractBaggageInvalid,
			ObservabilityEventId.FailedExtractBaggageArgument,

			// Metrics Collection (80300-80399)
			ObservabilityEventId.LocalMetricsCollectorCreated,
			ObservabilityEventId.CloudMetricsAdapterCreated,
			ObservabilityEventId.MetricsRecorded,
			ObservabilityEventId.MetricsFlushed,
			ObservabilityEventId.CounterIncremented,
			ObservabilityEventId.GaugeSet,
			ObservabilityEventId.HistogramRecorded,
			ObservabilityEventId.MetricsExportCompleted,

			// Tracing (80400-80499)
			ObservabilityEventId.TraceStarted,
			ObservabilityEventId.TraceCompleted,
			ObservabilityEventId.SpanStarted,
			ObservabilityEventId.SpanCompleted,
			ObservabilityEventId.TraceExported,
			ObservabilityEventId.TraceSampled,
			ObservabilityEventId.TraceDropped,

			// Health Checks (80500-80599)
			ObservabilityEventId.HealthCheckExecuted,
			ObservabilityEventId.HealthCheckPassed,
			ObservabilityEventId.HealthCheckFailed,
			ObservabilityEventId.HealthStatusChanged,
			ObservabilityEventId.ReadinessCheckExecuted,
			ObservabilityEventId.LivenessCheckExecuted
		];
	}

	#endregion
}
