// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextFlowMetrics" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Observability")]
[Trait("Feature", "Metrics")]
public sealed class ContextFlowMetricsShould : UnitTestBase
{
	private readonly ContextFlowMetrics _metrics;
	private readonly IOptions<ContextObservabilityOptions> _options;

	public ContextFlowMetricsShould()
	{
		_options = Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions());
		_metrics = new ContextFlowMetrics(_options);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_metrics.Dispose();
		}

		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ContextFlowMetrics(null!));
	}

	[Fact]
	public void Create_WithValidOptions_Succeeds()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions());

		// Act
		using var metrics = new ContextFlowMetrics(options);

		// Assert
		metrics.ShouldNotBeNull();
	}

	#endregion

	#region RecordContextSnapshot Tests

	[Fact]
	public void RecordContextSnapshot_IncrementsTotalContextsProcessed()
	{
		// Act
		_metrics.RecordContextSnapshot("handler", 5, 256);
		_metrics.RecordContextSnapshot("middleware", 3, 128);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.TotalContextsProcessed.ShouldBe(2);
	}

	[Fact]
	public void RecordContextSnapshot_AcceptsVariousStages()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextSnapshot("pre-handler", 1, 100);
		_metrics.RecordContextSnapshot("handler", 2, 200);
		_metrics.RecordContextSnapshot("post-handler", 3, 300);
	}

	#endregion

	#region RecordContextMutation Tests

	[Fact]
	public void RecordContextMutation_ThrowsArgumentNullException_WhenFieldNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextMutation(ContextChangeType.Added, null!, "handler"));
	}

	[Fact]
	public void RecordContextMutation_ThrowsArgumentNullException_WhenStageIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextMutation(ContextChangeType.Added, "field", null!));
	}

	[Fact]
	public void RecordContextMutation_AcceptsAllChangeTypes()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextMutation(ContextChangeType.Added, "field1", "stage");
		_metrics.RecordContextMutation(ContextChangeType.Removed, "field2", "stage");
		_metrics.RecordContextMutation(ContextChangeType.Modified, "field3", "stage");
	}

	#endregion

	#region RecordContextError Tests

	[Fact]
	public void RecordContextError_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextError("ValidationFailed", "handler");
		_metrics.RecordContextError("SerializationError", "middleware");
	}

	#endregion

	#region RecordContextValidationFailure Tests

	[Fact]
	public void RecordContextValidationFailure_ThrowsArgumentNullException_WhenReasonIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordContextValidationFailure(null!));
	}

	[Fact]
	public void RecordContextValidationFailure_AcceptsValidReason()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextValidationFailure("Missing required field");
	}

	#endregion

	#region RecordCrossBoundaryTransition Tests

	[Fact]
	public void RecordCrossBoundaryTransition_IncrementsPreservedCount_WhenPreserved()
	{
		// Arrange
		_metrics.RecordContextSnapshot("initial", 1, 100); // Initial context

		// Act
		_metrics.RecordCrossBoundaryTransition("service-a", true);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ContextsPreservedSuccessfully.ShouldBe(1);
	}

	[Fact]
	public void RecordCrossBoundaryTransition_DoesNotIncrementPreserved_WhenNotPreserved()
	{
		// Arrange
		_metrics.RecordContextSnapshot("initial", 1, 100);

		// Act
		_metrics.RecordCrossBoundaryTransition("service-a", false);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ContextsPreservedSuccessfully.ShouldBe(0);
	}

	#endregion

	#region RecordContextPreservationSuccess Tests

	[Fact]
	public void RecordContextPreservationSuccess_IncrementsSuccessCount()
	{
		// Act
		_metrics.RecordContextPreservationSuccess("handler");
		_metrics.RecordContextPreservationSuccess("middleware");

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ContextsPreservedSuccessfully.ShouldBe(2);
	}

	#endregion

	#region RecordContextSizeThresholdExceeded Tests

	[Fact]
	public void RecordContextSizeThresholdExceeded_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextSizeThresholdExceeded("handler", 65536);
	}

	#endregion

	#region RecordContextSize Tests

	[Fact]
	public void RecordContextSize_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordContextSize("handler", 1024);
		_metrics.RecordContextSize("middleware", 2048);
	}

	#endregion

	#region RecordPipelineStageLatency Tests

	[Fact]
	public void RecordPipelineStageLatency_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordPipelineStageLatency("handler", 15);
		_metrics.RecordPipelineStageLatency("validation", 5);
	}

	#endregion

	#region RecordSerializationLatency Tests

	[Fact]
	public void RecordSerializationLatency_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordSerializationLatency("json", 10);
		_metrics.RecordSerializationLatency("protobuf", 2);
	}

	#endregion

	#region RecordDeserializationLatency Tests

	[Fact]
	public void RecordDeserializationLatency_AcceptsValidParameters()
	{
		// Act & Assert - Should not throw
		_metrics.RecordDeserializationLatency("json", 8);
		_metrics.RecordDeserializationLatency("protobuf", 1);
	}

	#endregion

	#region UpdateActiveContextCount Tests

	[Fact]
	public void UpdateActiveContextCount_IncrementsCount()
	{
		// Act
		_metrics.UpdateActiveContextCount(1);
		_metrics.UpdateActiveContextCount(1);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBe(2);
	}

	[Fact]
	public void UpdateActiveContextCount_DecrementsCount()
	{
		// Arrange
		_metrics.UpdateActiveContextCount(5);

		// Act
		_metrics.UpdateActiveContextCount(-2);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBe(3);
	}

	[Fact]
	public void UpdateActiveContextCount_DoesNotGoBelowZero()
	{
		// Arrange
		_metrics.UpdateActiveContextCount(1);

		// Act
		_metrics.UpdateActiveContextCount(-10);

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.ActiveContexts.ShouldBe(0);
	}

	#endregion

	#region UpdateLineageDepth Tests

	[Fact]
	public void UpdateLineageDepth_TracksMaxDepth()
	{
		// Act
		_metrics.UpdateLineageDepth(3);
		_metrics.UpdateLineageDepth(5);
		_metrics.UpdateLineageDepth(2); // Smaller than max

		// Assert
		var summary = _metrics.GetMetricsSummary();
		summary.MaxLineageDepth.ShouldBe(5);
	}

	#endregion

	#region GetMetricsSummary Tests

	[Fact]
	public void GetMetricsSummary_ReturnsValidSummary()
	{
		// Arrange
		_metrics.RecordContextSnapshot("handler", 5, 256);
		_metrics.RecordContextPreservationSuccess("handler");
		_metrics.UpdateActiveContextCount(1);
		_metrics.UpdateLineageDepth(3);

		// Act
		var summary = _metrics.GetMetricsSummary();

		// Assert
		summary.TotalContextsProcessed.ShouldBe(1);
		summary.ContextsPreservedSuccessfully.ShouldBe(1);
		summary.ActiveContexts.ShouldBe(1);
		summary.MaxLineageDepth.ShouldBe(3);
		summary.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void GetMetricsSummary_ReturnsFullPreservationRate_WhenNoContextsProcessed()
	{
		// Act
		var summary = _metrics.GetMetricsSummary();

		// Assert
		summary.PreservationRate.ShouldBe(1.0);
	}

	[Fact]
	public void GetMetricsSummary_CalculatesPreservationRateCorrectly()
	{
		// Arrange - Process 4 contexts, preserve 2
		_metrics.RecordContextSnapshot("stage", 1, 100);
		_metrics.RecordContextSnapshot("stage", 1, 100);
		_metrics.RecordContextSnapshot("stage", 1, 100);
		_metrics.RecordContextSnapshot("stage", 1, 100);
		_metrics.RecordContextPreservationSuccess("stage");
		_metrics.RecordContextPreservationSuccess("stage");

		// Act
		var summary = _metrics.GetMetricsSummary();

		// Assert
		summary.PreservationRate.ShouldBe(0.5);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions());
		var metrics = new ContextFlowMetrics(options);

		// Act & Assert - Should not throw
		metrics.Dispose();
		metrics.Dispose();
		metrics.Dispose();
	}

	#endregion

	#region IContextFlowMetrics Interface Tests

	[Fact]
	public void Implement_IContextFlowMetrics()
	{
		// Assert
		_metrics.ShouldBeAssignableTo<IContextFlowMetrics>();
	}

	[Fact]
	public void Implement_IDisposable()
	{
		// Assert
		_metrics.ShouldBeAssignableTo<IDisposable>();
	}

	#endregion
}
