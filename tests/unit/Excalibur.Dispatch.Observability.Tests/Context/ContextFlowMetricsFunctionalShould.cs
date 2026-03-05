// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

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
	private readonly ConcurrentQueue<(string Name, object Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];
	private readonly ContextFlowMetrics _metrics;
	private readonly object _metricsMeterIdentity;

	public ContextFlowMetricsFunctionalShould()
	{
		_metrics = new ContextFlowMetrics(MsOptions.Create(new ContextObservabilityOptions()));
		_metricsMeterIdentity = GetMetricsMeterIdentity(_metrics);

		_listener = new MeterListener();
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _metricsMeterIdentity))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
		{
			_measurements.Enqueue((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
		{
			_measurements.Enqueue((instrument.Name, measurement, tags.ToArray()));
		});
		_listener.Start();
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
		var stage = $"PreProcessing-{Guid.NewGuid():N}";
		_metrics.RecordContextSnapshot(stage, 15, 2048);

		_listener.RecordObservableInstruments();

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.snapshots" &&
			(long)m.Value == 1 &&
			HasTag(m.Tags, "stage", stage));

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.field_count" &&
			(double)m.Value == 15 &&
			HasTag(m.Tags, "stage", stage));

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.size_bytes" &&
			(double)m.Value == 2048 &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void RecordContextMutation()
	{
		var field = $"CorrelationId-{Guid.NewGuid():N}";
		var stage = $"PreProcessing-{Guid.NewGuid():N}";
		_metrics.RecordContextMutation(ContextChangeType.Modified, field, stage);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.mutations" &&
			(long)m.Value == 1 &&
			HasTag(m.Tags, "change_type", "Modified") &&
			HasTag(m.Tags, "field", field) &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void RecordFieldLoss_OnRemoval()
	{
		var field = $"TenantId-{Guid.NewGuid():N}";
		var stage = $"Middleware-{Guid.NewGuid():N}";
		_metrics.RecordContextMutation(ContextChangeType.Removed, field, stage);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.field_loss" &&
			(long)m.Value == 1 &&
			HasTag(m.Tags, "change_type", "Removed") &&
			HasTag(m.Tags, "field", field) &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void NotRecordFieldLoss_OnAdded()
	{
		_measurements.Clear();
		var field = $"NewField-{Guid.NewGuid():N}";
		var stage = $"Middleware-{Guid.NewGuid():N}";
		_metrics.RecordContextMutation(ContextChangeType.Added, field, stage);

		var addedFieldLoss = _measurements.Any(m =>
			m.Name == "dispatch.context.flow.field_loss" &&
			m.Tags.Any(t => t.Key == "change_type" && string.Equals((string?)t.Value, "Added", StringComparison.Ordinal)) &&
			m.Tags.Any(t => t.Key == "field" && string.Equals((string?)t.Value, field, StringComparison.Ordinal)) &&
			m.Tags.Any(t => t.Key == "stage" && string.Equals((string?)t.Value, stage, StringComparison.Ordinal)));
		addedFieldLoss.ShouldBeFalse();
	}

	[Fact]
	public void RecordContextError()
	{
		var errorType = $"serialization_failure_{Guid.NewGuid():N}";
		var stage = $"PostProcessing-{Guid.NewGuid():N}";
		_metrics.RecordContextError(errorType, stage);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.errors" &&
			HasTag(m.Tags, "error_type", errorType) &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public async Task RecordContextValidationFailure()
	{
		var expectedReason = $"missing_corr_{Guid.NewGuid():N}"[..21];
		_metrics.RecordContextValidationFailure(expectedReason);

		var observed = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => _measurements.Any(m =>
				m.Name == "dispatch.context.flow.validation_failures" &&
				m.Tags.Any(t => t.Key == "reason" && string.Equals((string?)t.Value, expectedReason, StringComparison.Ordinal))),
			TimeSpan.FromSeconds(2),
			TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
		observed.ShouldBeTrue();
	}

	[Fact]
	public void RecordCrossBoundaryTransition_Preserved()
	{
		var service = $"OrderService-{Guid.NewGuid():N}";
		_metrics.RecordCrossBoundaryTransition(service, true);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.cross_boundary_transitions" &&
			HasTag(m.Tags, "service", service) &&
			HasTag(m.Tags, "preserved", "True"));
	}

	[Fact]
	public void RecordContextPreservationSuccess()
	{
		var stage = $"Handler-{Guid.NewGuid():N}";
		_metrics.RecordContextPreservationSuccess(stage);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.preservation_success" &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void RecordContextSizeThresholdExceeded()
	{
		var stage = $"PostProcessing-{Guid.NewGuid():N}";
		_metrics.RecordContextSizeThresholdExceeded(stage, 150_000);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.size_threshold_exceeded" &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void RecordPipelineStageLatency()
	{
		var stage = $"Validation-{Guid.NewGuid():N}";
		_metrics.RecordPipelineStageLatency(stage, 42);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.stage_latency_ms" &&
			(double)m.Value == 42 &&
			HasTag(m.Tags, "stage", stage));
	}

	[Fact]
	public void RecordSerializationLatency()
	{
		var operation = $"json-{Guid.NewGuid():N}";
		_metrics.RecordSerializationLatency(operation, 5);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.serialization_latency_ms" &&
			(double)m.Value == 5 &&
			HasTag(m.Tags, "operation", operation));
	}

	[Fact]
	public void RecordDeserializationLatency()
	{
		var operation = $"json-{Guid.NewGuid():N}";
		_metrics.RecordDeserializationLatency(operation, 3);

		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.deserialization_latency_ms" &&
			(double)m.Value == 3 &&
			HasTag(m.Tags, "operation", operation));
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
		var stage = $"Stage-{Guid.NewGuid():N}";
		_metrics.RecordContextMutation(ContextChangeType.Modified, longFieldName, stage);

		// Should not throw - field name should be truncated
		_measurements.ShouldContain(m =>
			m.Name == "dispatch.context.flow.mutations" &&
			HasTag(m.Tags, "change_type", "Modified") &&
			HasTag(m.Tags, "stage", stage));
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

	private static bool HasTag(KeyValuePair<string, object?>[] tags, string key, string value) =>
		tags.Any(t =>
			string.Equals(t.Key, key, StringComparison.Ordinal) &&
			string.Equals(t.Value as string, value, StringComparison.Ordinal));

	private static object GetMetricsMeterIdentity(ContextFlowMetrics metrics)
	{
		var meterField = typeof(ContextFlowMetrics).GetField("_meter",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		meterField.ShouldNotBeNull();

		var meterIdentity = meterField.GetValue(metrics);
		meterIdentity.ShouldNotBeNull();
		return meterIdentity;
	}
}
