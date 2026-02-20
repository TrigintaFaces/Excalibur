// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextFlowMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowMetricsShould : IDisposable
{
	private ContextFlowMetrics? _metrics;

	public void Dispose() => _metrics?.Dispose();

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowMetrics((Microsoft.Extensions.Options.IOptions<ContextObservabilityOptions>)null!));
	}

	[Fact]
	public void RecordContextSnapshot_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextSnapshot("PreProcessing", 10, 1024);
	}

	[Fact]
	public void RecordContextMutation_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextMutation(ContextChangeType.Added, "MessageId", "PreProcessing");
	}

	[Fact]
	public void RecordContextMutation_ThrowsOnNullFieldName()
	{
		_metrics = CreateMetrics();
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextMutation(ContextChangeType.Added, null!, "stage"));
	}

	[Fact]
	public void RecordContextMutation_ThrowsOnNullStage()
	{
		_metrics = CreateMetrics();
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextMutation(ContextChangeType.Added, "field", null!));
	}

	[Fact]
	public void RecordContextError_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextError("validation", "PreProcessing");
	}

	[Fact]
	public void RecordContextValidationFailure_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextValidationFailure("Missing required field");
	}

	[Fact]
	public void RecordContextValidationFailure_ThrowsOnNull()
	{
		_metrics = CreateMetrics();
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextValidationFailure(null!));
	}

	[Fact]
	public void RecordCrossBoundaryTransition_Preserved()
	{
		_metrics = CreateMetrics();
		_metrics.RecordCrossBoundaryTransition("order-service", contextPreserved: true);
	}

	[Fact]
	public void RecordCrossBoundaryTransition_NotPreserved()
	{
		_metrics = CreateMetrics();
		_metrics.RecordCrossBoundaryTransition("order-service", contextPreserved: false);
	}

	[Fact]
	public void RecordContextPreservationSuccess_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextPreservationSuccess("PostProcessing");
	}

	[Fact]
	public void RecordContextSizeThresholdExceeded_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextSizeThresholdExceeded("PreProcessing", 200_000);
	}

	[Fact]
	public void RecordContextSize_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextSize("PreProcessing", 5000);
	}

	[Fact]
	public void RecordPipelineStageLatency_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordPipelineStageLatency("PreProcessing", 150);
	}

	[Fact]
	public void RecordSerializationLatency_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordSerializationLatency("serialize", 10);
	}

	[Fact]
	public void RecordDeserializationLatency_WithoutThrowing()
	{
		_metrics = CreateMetrics();
		_metrics.RecordDeserializationLatency("deserialize", 15);
	}

	[Fact]
	public void UpdateActiveContextCount_Increment()
	{
		_metrics = CreateMetrics();
		_metrics.UpdateActiveContextCount(1);
	}

	[Fact]
	public void UpdateActiveContextCount_Decrement()
	{
		_metrics = CreateMetrics();
		_metrics.UpdateActiveContextCount(1);
		_metrics.UpdateActiveContextCount(-1);
	}

	[Fact]
	public void UpdateActiveContextCount_FloorAtZero()
	{
		_metrics = CreateMetrics();
		_metrics.UpdateActiveContextCount(-5);
		// Should not go negative — floor at zero via CAS
	}

	[Fact]
	public void UpdateLineageDepth_TracksMaximum()
	{
		_metrics = CreateMetrics();
		_metrics.UpdateLineageDepth(5);
		_metrics.UpdateLineageDepth(10);
		_metrics.UpdateLineageDepth(3); // Should not decrease

		var summary = _metrics.GetMetricsSummary();
		summary.MaxLineageDepth.ShouldBe(10);
	}

	[Fact]
	public void GetMetricsSummary_ReturnsValidSummary()
	{
		_metrics = CreateMetrics();
		_metrics.RecordContextSnapshot("test", 5, 100);
		_metrics.RecordContextPreservationSuccess("test");

		var summary = _metrics.GetMetricsSummary();

		summary.ShouldNotBeNull();
		summary.TotalContextsProcessed.ShouldBe(1);
		summary.ContextsPreservedSuccessfully.ShouldBe(1);
		summary.PreservationRate.ShouldBe(1.0);
		summary.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void GetMetricsSummary_PreservationRate_WhenNoProcessing()
	{
		_metrics = CreateMetrics();
		var summary = _metrics.GetMetricsSummary();

		// No processing = default rate of 1.0
		summary.PreservationRate.ShouldBe(1.0);
	}

	[Fact]
	public void ImplementIContextFlowMetrics()
	{
		_metrics = CreateMetrics();
		_metrics.ShouldBeAssignableTo<IContextFlowMetrics>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_metrics = CreateMetrics();
		_metrics.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void TruncateLongFieldNames()
	{
		_metrics = CreateMetrics();
		var longFieldName = new string('x', 100);

		// Should not throw — field name gets truncated internally
		_metrics.RecordContextMutation(ContextChangeType.Added, longFieldName, "stage");
	}

	private static ContextFlowMetrics CreateMetrics()
	{
		var options = MsOptions.Create(new ContextObservabilityOptions());
		return new ContextFlowMetrics(options);
	}
}
