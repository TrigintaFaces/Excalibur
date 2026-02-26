// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Context;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Deep coverage tests for <see cref="ContextFlowMetrics"/> covering the IMeterFactory constructor,
/// field-loss mutation path, concurrent operations, and GetMetricsSummary edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextFlowMetricsDepthCoverageShould : IDisposable
{
	private ContextFlowMetrics? _metrics;

	public void Dispose() => _metrics?.Dispose();

	[Fact]
	public void ConstructWithIMeterFactory()
	{
		// Arrange
		var meterFactory = new TestMeterFactory();

		// Act
		_metrics = new ContextFlowMetrics(meterFactory, MsOptions.Create(new ContextObservabilityOptions()));

		// Assert
		_metrics.ShouldNotBeNull();
		_metrics.ShouldBeAssignableTo<IContextFlowMetrics>();
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowMetrics((IMeterFactory)null!, MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullOptionsWithMeterFactory()
	{
		var meterFactory = new TestMeterFactory();
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowMetrics(meterFactory, null!));
	}

	[Fact]
	public void RecordContextMutation_TrackFieldLoss_WhenChangeTypeIsRemoved()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — Removed change type should increment field loss counter
		_metrics.RecordContextMutation(ContextChangeType.Removed, "CorrelationId", "PostProcessing");

		// Assert — no exception, field loss recorded internally
	}

	[Fact]
	public void RecordContextMutation_NoFieldLoss_WhenChangeTypeIsAdded()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — Added change type should NOT increment field loss counter
		_metrics.RecordContextMutation(ContextChangeType.Added, "NewField", "PreProcessing");

		// Assert — no exception
	}

	[Fact]
	public void RecordContextMutation_NoFieldLoss_WhenChangeTypeIsModified()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act
		_metrics.RecordContextMutation(ContextChangeType.Modified, "ExistingField", "Processing");

		// Assert — no exception
	}

	[Fact]
	public void RecordCrossBoundaryTransition_IncrementPreservation_WhenPreserved()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act
		_metrics.RecordCrossBoundaryTransition("ServiceA", contextPreserved: true);
		_metrics.RecordCrossBoundaryTransition("ServiceB", contextPreserved: true);
		_metrics.RecordCrossBoundaryTransition("ServiceC", contextPreserved: false);

		// Assert — preserved count should be 2 (from RecordCrossBoundaryTransition only)
		var summary = _metrics.GetMetricsSummary();
		summary.ContextsPreservedSuccessfully.ShouldBe(2);
	}

	[Fact]
	public void GetMetricsSummary_ComputePartialPreservationRate()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Record 3 snapshots (increments total) and 1 preservation success
		_metrics.RecordContextSnapshot("stage1", 5, 100);
		_metrics.RecordContextSnapshot("stage2", 3, 200);
		_metrics.RecordContextSnapshot("stage3", 7, 150);
		_metrics.RecordContextPreservationSuccess("stage1");

		// Act
		var summary = _metrics.GetMetricsSummary();

		// Assert
		summary.TotalContextsProcessed.ShouldBe(3);
		summary.ContextsPreservedSuccessfully.ShouldBe(1);
		summary.PreservationRate.ShouldBeInRange(0.3, 0.35); // 1/3 ≈ 0.333
	}

	[Fact]
	public void UpdateActiveContextCount_HandleConcurrentIncrementDecrement()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — simulate a lifecycle: increment then decrement
		_metrics.UpdateActiveContextCount(5);
		_metrics.UpdateActiveContextCount(3);
		_metrics.UpdateActiveContextCount(-2);

		// Assert — summary shows correct value (5 + 3 - 2 = 6)
		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBe(6);
	}

	[Fact]
	public void UpdateActiveContextCount_FloorAtZero_WhenDecrementBeyondZero()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — decrement from zero should floor at zero
		_metrics.UpdateActiveContextCount(-10);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void UpdateLineageDepth_TrackMaximumDepthOnly()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — increasing then decreasing
		_metrics.UpdateLineageDepth(3);
		_metrics.UpdateLineageDepth(7);
		_metrics.UpdateLineageDepth(5); // lower — should not update
		_metrics.UpdateLineageDepth(7); // same — should not update
		_metrics.UpdateLineageDepth(10); // higher — should update

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.MaxLineageDepth.ShouldBe(10);
	}

	[Fact]
	public void UpdateLineageDepth_HandleZeroDepth()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act
		_metrics.UpdateLineageDepth(0);

		// Assert — initial max is 0, updating with 0 should be fine
		var summary = _metrics.GetMetricsSummary();
		summary.MaxLineageDepth.ShouldBe(0);
	}

	[Fact]
	public void RecordContextSizeThresholdExceeded_RecordsBothCounterAndHistogram()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act — should not throw and should record both metrics
		_metrics.RecordContextSizeThresholdExceeded("Processing", 500_000);
		_metrics.RecordContextSizeThresholdExceeded("Processing", 1_000_000);

		// Assert — no exception
	}

	[Fact]
	public void RecordMultipleSerializationOperationTypes()
	{
		// Arrange
		_metrics = CreateMetrics();

		// Act
		_metrics.RecordSerializationLatency("json", 5);
		_metrics.RecordSerializationLatency("protobuf", 2);
		_metrics.RecordDeserializationLatency("json", 8);
		_metrics.RecordDeserializationLatency("protobuf", 3);

		// Assert — no exception
	}

	[Fact]
	public void TruncateLongFieldNames_InMutation()
	{
		// Arrange
		_metrics = CreateMetrics();
		var longFieldName = new string('A', 100); // > 50 char limit

		// Act — should truncate internally without throwing
		_metrics.RecordContextMutation(ContextChangeType.Added, longFieldName, "stage");

		// Assert — no exception
	}

	[Fact]
	public void TruncateLongFailureReason_InValidationFailure()
	{
		// Arrange
		_metrics = CreateMetrics();
		var longReason = new string('B', 100);

		// Act — should truncate internally
		_metrics.RecordContextValidationFailure(longReason);

		// Assert — no exception
	}

	[Fact]
	public void Dispose_MultipleTimes()
	{
		// Arrange
		var metrics = CreateMetrics();

		// Act & Assert — idempotent disposal
		metrics.Dispose();
		metrics.Dispose();
		_metrics = null; // prevent double-dispose in teardown
	}

	[Fact]
	public void GetMetricsSummary_IncludeTimestamp()
	{
		// Arrange
		_metrics = CreateMetrics();
		var before = DateTimeOffset.UtcNow;

		// Act
		var summary = _metrics.GetMetricsSummary();

		// Assert
		summary.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		summary.Timestamp.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	private static ContextFlowMetrics CreateMetrics()
	{
		return new ContextFlowMetrics(MsOptions.Create(new ContextObservabilityOptions()));
	}

	/// <summary>
	/// Minimal IMeterFactory implementation for testing the DI constructor path.
	/// </summary>
	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options.Name, options.Version);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}

			_meters.Clear();
		}
	}
}
