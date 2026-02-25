// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Options;

using Tests.Shared.Helpers;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Functional tests for <see cref="ContextFlowMetrics"/> verifying metric instrument emissions via MeterListener.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "ContextFlow")]
public sealed class ContextFlowMetricsFunctionalShould : IDisposable
{
	private readonly MeterListener _listener;
	private readonly List<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
	private readonly ContextFlowMetrics _metrics;

	public ContextFlowMetricsFunctionalShould()
	{
		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == "Excalibur.Dispatch.Observability.Context")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			_measurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
		{
			_measurements.Add((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.Start();

		_metrics = new ContextFlowMetrics(MsOptions.Create(new ContextObservabilityOptions()));
	}

	public void Dispose()
	{
		_listener.Dispose();
		_metrics.Dispose();
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowMetrics((IOptions<ContextObservabilityOptions>)null!));
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowMetrics(
			(IMeterFactory)null!,
			MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void RecordContextSnapshot()
	{
		_metrics.RecordContextSnapshot("PreProcessing", 15, 2048);

		_listener.RecordObservableInstruments();

		var snapshot = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.snapshots");
		snapshot.Name.ShouldNotBeNull();
		((long)snapshot.Value).ShouldBe(1);

		var fieldCount = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.field_count");
		fieldCount.Name.ShouldNotBeNull();
		((double)fieldCount.Value).ShouldBe(15);

		var size = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.size_bytes");
		size.Name.ShouldNotBeNull();
		((double)size.Value).ShouldBe(2048);
	}

	[Fact]
	public void RecordContextMutation()
	{
		_metrics.RecordContextMutation(ContextChangeType.Modified, "CorrelationId", "PreProcessing");

		var mutation = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.mutations");
		mutation.Name.ShouldNotBeNull();
		((long)mutation.Value).ShouldBe(1);

		mutation.Tags.ShouldContain(t => t.Key == "change_type" && (string)t.Value! == "Modified");
	}

	[Fact]
	public void RecordFieldLoss_OnRemoval()
	{
		_metrics.RecordContextMutation(ContextChangeType.Removed, "TenantId", "Middleware");

		var fieldLoss = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.field_loss");
		fieldLoss.Name.ShouldNotBeNull();
		((long)fieldLoss.Value).ShouldBe(1);
	}

	[Fact]
	public void NotRecordFieldLoss_OnAdded()
	{
		_measurements.Clear();
		_metrics.RecordContextMutation(ContextChangeType.Added, "NewField", "Middleware");

		var addedFieldLoss = _measurements.Any(m =>
			m.Name == "dispatch.context.flow.field_loss" &&
			m.Tags.Any(t => t.Key == "change_type" && string.Equals((string?)t.Value, "Added", StringComparison.Ordinal)) &&
			m.Tags.Any(t => t.Key == "field" && string.Equals((string?)t.Value, "NewField", StringComparison.Ordinal)) &&
			m.Tags.Any(t => t.Key == "stage" && string.Equals((string?)t.Value, "Middleware", StringComparison.Ordinal)));
		addedFieldLoss.ShouldBeFalse();
	}

	[Fact]
	public void RecordContextError()
	{
		_metrics.RecordContextError("serialization_failure", "PostProcessing");

		var error = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.errors");
		error.Name.ShouldNotBeNull();
		error.Tags.ShouldContain(t => t.Key == "error_type" && (string)t.Value! == "serialization_failure");
	}

	[Fact]
	public void RecordContextValidationFailure()
	{
		_metrics.RecordContextValidationFailure("missing_correlation_id");

		var failure = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.validation_failures");
		failure.Name.ShouldNotBeNull();
		failure.Tags.ShouldContain(t => t.Key == "reason" && (string)t.Value! == "missing_correlation_id");
	}

	[Fact]
	public void RecordCrossBoundaryTransition_Preserved()
	{
		_metrics.RecordCrossBoundaryTransition("OrderService", true);

		var transition = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.cross_boundary_transitions");
		transition.Name.ShouldNotBeNull();
		transition.Tags.ShouldContain(t => t.Key == "service" && (string)t.Value! == "OrderService");
		transition.Tags.ShouldContain(t => t.Key == "preserved" && (string)t.Value! == "True");
	}

	[Fact]
	public void RecordContextPreservationSuccess()
	{
		_metrics.RecordContextPreservationSuccess("Handler");

		var success = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.preservation_success");
		success.Name.ShouldNotBeNull();
		success.Tags.ShouldContain(t => t.Key == "stage" && (string)t.Value! == "Handler");
	}

	[Fact]
	public void RecordContextSizeThresholdExceeded()
	{
		_metrics.RecordContextSizeThresholdExceeded("PostProcessing", 150_000);

		var exceeded = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.size_threshold_exceeded");
		exceeded.Name.ShouldNotBeNull();
	}

	[Fact]
	public void RecordPipelineStageLatency()
	{
		_metrics.RecordPipelineStageLatency("Validation", 42);

		var latency = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.stage_latency_ms");
		latency.Name.ShouldNotBeNull();
		((double)latency.Value).ShouldBe(42);
	}

	[Fact]
	public void RecordSerializationLatency()
	{
		_metrics.RecordSerializationLatency("json", 5);

		var latency = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.serialization_latency_ms");
		latency.Name.ShouldNotBeNull();
		((double)latency.Value).ShouldBe(5);
	}

	[Fact]
	public void RecordDeserializationLatency()
	{
		_metrics.RecordDeserializationLatency("json", 3);

		var latency = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.deserialization_latency_ms");
		latency.Name.ShouldNotBeNull();
		((double)latency.Value).ShouldBe(3);
	}

	[Fact]
	public void TrackActiveContextCount()
	{
		_metrics.UpdateActiveContextCount(3);
		_metrics.UpdateActiveContextCount(-1);

		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBe(2);
	}

	[Fact]
	public void FloorActiveContextCountAtZero()
	{
		_metrics.UpdateActiveContextCount(-5);

		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void TrackMaxLineageDepth()
	{
		_metrics.UpdateLineageDepth(3);
		_metrics.UpdateLineageDepth(5);
		_metrics.UpdateLineageDepth(2); // Should not decrease

		var summary = _metrics.GetMetricsSummary();
		summary.MaxLineageDepth.ShouldBe(5);
	}

	[Fact]
	public void CalculatePreservationRate()
	{
		// Record some snapshots and preservation successes
		_metrics.RecordContextSnapshot("Stage1", 10, 100);
		_metrics.RecordContextSnapshot("Stage2", 10, 100);
		_metrics.RecordContextPreservationSuccess("Stage1");

		var summary = _metrics.GetMetricsSummary();
		summary.TotalContextsProcessed.ShouldBe(2);
		summary.ContextsPreservedSuccessfully.ShouldBe(1);
		summary.PreservationRate.ShouldBe(0.5);
	}

	[Fact]
	public void ReturnPerfectPreservationRate_WhenNoContextsProcessed()
	{
		var summary = _metrics.GetMetricsSummary();
		summary.PreservationRate.ShouldBe(1.0);
	}

	[Fact]
	public void TruncateLongFieldNames()
	{
		var longFieldName = new string('x', 100);
		_metrics.RecordContextMutation(ContextChangeType.Modified, longFieldName, "Stage");

		// Should not throw - field name should be truncated
		var mutation = _measurements.FirstOrDefault(m => m.Name == "dispatch.context.flow.mutations");
		mutation.Name.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullFieldName()
	{
		Should.Throw<ArgumentNullException>(() => _metrics.RecordContextMutation(ContextChangeType.Added, null!, "Stage"));
	}

	[Fact]
	public void ThrowOnNullStage_ForMutation()
	{
		Should.Throw<ArgumentNullException>(() => _metrics.RecordContextMutation(ContextChangeType.Added, "Field", null!));
	}

	[Fact]
	public void ThrowOnNullFailureReason()
	{
		Should.Throw<ArgumentNullException>(() => _metrics.RecordContextValidationFailure(null!));
	}

	[Fact]
	public void WorkWithMeterFactory()
	{
		using var meterFactory = new TestMeterFactory();
		using var metrics = new ContextFlowMetrics(meterFactory, MsOptions.Create(new ContextObservabilityOptions()));

		metrics.RecordContextSnapshot("Test", 5, 512);

		var summary = metrics.GetMetricsSummary();
		summary.TotalContextsProcessed.ShouldBe(1);
	}
}
