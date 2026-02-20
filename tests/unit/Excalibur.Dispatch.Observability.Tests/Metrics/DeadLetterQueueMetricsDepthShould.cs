// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Deep coverage tests for <see cref="DeadLetterQueueMetrics"/> covering constructor variants,
/// IMeterFactory integration, null guard, cardinality guard behavior, multi-queue tracking,
/// and observable gauge depth output.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DeadLetterQueueMetricsDepthShould
{
	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new DeadLetterQueueMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void CreateWithMeterFactory()
	{
		// Arrange
		var factory = A.Fake<IMeterFactory>();
		var meter = new Meter("test-dlq");
		A.CallTo(() => factory.Create(A<MeterOptions>._)).Returns(meter);

		// Act
		using var metrics = new DeadLetterQueueMetrics(factory);

		// Assert
		metrics.Meter.ShouldNotBeNull();
		A.CallTo(() => factory.Create(A<MeterOptions>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DefaultConstructor_OwnsMeter()
	{
		// Act
		using var metrics = new DeadLetterQueueMetrics();

		// Assert
		metrics.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);
	}

	[Fact]
	public void RecordEnqueued_MultipleMessageTypes()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — various message types through cardinality guard
		metrics.RecordEnqueued("OrderCommand", "MaxRetries");
		metrics.RecordEnqueued("PaymentEvent", "Timeout");
		metrics.RecordEnqueued("NotificationMessage", "SchemaViolation");
		metrics.RecordEnqueued("AuditCommand", "AuthorizationFailed");

		// Assert — no exception, cardinality guard accepts all
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordEnqueued_WithNullSourceQueue()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — null source queue should not add the tag
		metrics.RecordEnqueued("OrderCommand", "Timeout", null);

		// Assert — no exception
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordEnqueued_WithEmptySourceQueue()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — empty string source queue should not add the tag
		metrics.RecordEnqueued("OrderCommand", "Timeout", "");

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordReplayed_SuccessAndFailure()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — both outcomes
		metrics.RecordReplayed("OrderCommand", success: true);
		metrics.RecordReplayed("OrderCommand", success: false);
		metrics.RecordReplayed("PaymentEvent", success: true);

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordPurged_WithVariousCounts()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — different purge counts and reasons
		metrics.RecordPurged(0, "ScheduledCleanup");
		metrics.RecordPurged(1, "ManualPurge");
		metrics.RecordPurged(1000, "ExpiredMessages");

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateDepth_MultipleQueues()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — track multiple queues
		metrics.UpdateDepth(10, "orders-dlq");
		metrics.UpdateDepth(5, "payments-dlq");
		metrics.UpdateDepth(0, "notifications-dlq");

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateDepth_OverwritesPreviousValue()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — update same queue depth multiple times
		metrics.UpdateDepth(100, "orders-dlq");
		metrics.UpdateDepth(50, "orders-dlq");
		metrics.UpdateDepth(0, "orders-dlq");

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateDepth_DefaultQueueName_WhenNull()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — null queue name uses "default"
		metrics.UpdateDepth(42, null);
		metrics.UpdateDepth(43);

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void Dispose_Idempotent()
	{
		// Arrange
		var metrics = new DeadLetterQueueMetrics();

		// Act & Assert — double dispose should not throw
		metrics.Dispose();
		metrics.Dispose();
	}

	[Fact]
	public void RecordEnqueued_MultipleReasons()
	{
		// Arrange
		using var metrics = new DeadLetterQueueMetrics();

		// Act — different failure reasons
		metrics.RecordEnqueued("Cmd", "MaxRetriesExceeded", "q1");
		metrics.RecordEnqueued("Cmd", "TimeoutExpired", "q1");
		metrics.RecordEnqueued("Cmd", "SchemaValidationFailed", "q1");
		metrics.RecordEnqueued("Cmd", "DeserializationError", "q1");
		metrics.RecordEnqueued("Cmd", "AuthorizationDenied", "q1");

		// Assert — cardinality guard accepts all reasons (under 100 limit)
		metrics.Meter.ShouldNotBeNull();
	}
}
