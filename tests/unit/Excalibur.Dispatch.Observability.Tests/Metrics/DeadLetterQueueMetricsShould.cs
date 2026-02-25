// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DeadLetterQueueMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DeadLetterQueueMetricsShould : IDisposable
{
	private DeadLetterQueueMetrics? _metrics;

	public void Dispose() => _metrics?.Dispose();

	[Fact]
	public void CreateMeterOnConstruction()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UseCorrectMeterName()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		DeadLetterQueueMetrics.MeterName.ShouldBe("Excalibur.Dispatch.DeadLetterQueue");
	}

	[Fact]
	public void RecordEnqueued_WithoutSourceQueue()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.RecordEnqueued("OrderCommand", "MaxRetriesExceeded");
	}

	[Fact]
	public void RecordEnqueued_WithSourceQueue()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.RecordEnqueued("OrderCommand", "MaxRetriesExceeded", "orders-queue");
	}

	[Fact]
	public void RecordReplayed_WithSuccess()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.RecordReplayed("OrderCommand", success: true);
	}

	[Fact]
	public void RecordReplayed_WithFailure()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.RecordReplayed("OrderCommand", success: false);
	}

	[Fact]
	public void RecordPurged_WithCount()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.RecordPurged(50, "Expired");
	}

	[Fact]
	public void UpdateDepth_WithDefaultQueueName()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.UpdateDepth(100);
	}

	[Fact]
	public void UpdateDepth_WithCustomQueueName()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.UpdateDepth(42, "orders-dlq");
	}

	[Fact]
	public void ImplementIDeadLetterQueueMetrics()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.ShouldBeAssignableTo<IDeadLetterQueueMetrics>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_metrics = new DeadLetterQueueMetrics();
		_metrics.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void SupportCompleteWorkflow()
	{
		_metrics = new DeadLetterQueueMetrics();

		_metrics.RecordEnqueued("FailingCommand", "ProcessingError", "main-queue");
		_metrics.UpdateDepth(1, "main-dlq");
		_metrics.RecordEnqueued("AnotherFailing", "Timeout", "main-queue");
		_metrics.UpdateDepth(2, "main-dlq");
		_metrics.RecordReplayed("FailingCommand", success: true);
		_metrics.UpdateDepth(1, "main-dlq");
		_metrics.RecordPurged(1, "ManualPurge");
		_metrics.UpdateDepth(0, "main-dlq");
	}
}
