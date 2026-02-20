// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="DeadLetterQueueMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DeadLetterQueueMetricsShould : UnitTestBase
{
	private readonly DeadLetterQueueMetrics _metrics;

	public DeadLetterQueueMetricsShould()
	{
		_metrics = new DeadLetterQueueMetrics();
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
	public void CreateMeterWithCorrectName()
	{
		// Assert
		_metrics.Meter.ShouldNotBeNull();
		_metrics.Meter.Name.ShouldBe(DeadLetterQueueMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		// Assert
		DeadLetterQueueMetrics.MeterName.ShouldBe("Excalibur.Dispatch.DeadLetterQueue");
	}

	#endregion

	#region RecordEnqueued Tests

	[Fact]
	public void RecordEnqueued_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded"));
	}

	[Fact]
	public void RecordEnqueued_WithSourceQueue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded", "orders-queue"));
	}

	[Fact]
	public void RecordEnqueued_WithDifferentReasons()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded");
			_metrics.RecordEnqueued("PaymentFailed", "ValidationError");
			_metrics.RecordEnqueued("NotificationSent", "TimeoutExceeded");
			_metrics.RecordEnqueued("UserRegistered", "DeserializationError");
		});
	}

	[Fact]
	public void RecordEnqueued_WithNullSourceQueue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded", null));
	}

	[Fact]
	public void RecordEnqueued_WithEmptySourceQueue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded", ""));
	}

	#endregion

	#region RecordReplayed Tests

	[Fact]
	public void RecordReplayed_WithSuccessTrue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordReplayed("OrderCreated", success: true));
	}

	[Fact]
	public void RecordReplayed_WithSuccessFalse()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordReplayed("OrderCreated", success: false));
	}

	[Fact]
	public void RecordReplayed_MultipleMessages()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordReplayed("OrderCreated", success: true);
			_metrics.RecordReplayed("PaymentFailed", success: false);
			_metrics.RecordReplayed("NotificationSent", success: true);
		});
	}

	#endregion

	#region RecordPurged Tests

	[Fact]
	public void RecordPurged_WithSingleMessage()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordPurged(1, "Expired"));
	}

	[Fact]
	public void RecordPurged_WithMultipleMessages()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordPurged(100, "Expired"));
	}

	[Fact]
	public void RecordPurged_WithZeroMessages()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordPurged(0, "CleanupJob"));
	}

	[Fact]
	public void RecordPurged_WithDifferentReasons()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordPurged(50, "Expired");
			_metrics.RecordPurged(25, "ManualPurge");
			_metrics.RecordPurged(10, "StorageLimit");
		});
	}

	#endregion

	#region UpdateDepth Tests

	[Fact]
	public void UpdateDepth_WithDefaultQueue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateDepth(100));
	}

	[Fact]
	public void UpdateDepth_WithNullQueueName()
	{
		// Act & Assert - Should not throw (uses default queue)
		Should.NotThrow(() => _metrics.UpdateDepth(100, null));
	}

	[Fact]
	public void UpdateDepth_WithSpecificQueue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateDepth(50, "orders-dlq"));
	}

	[Fact]
	public void UpdateDepth_MultipleQueues()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.UpdateDepth(100, "orders-dlq");
			_metrics.UpdateDepth(50, "payments-dlq");
			_metrics.UpdateDepth(25, "notifications-dlq");
		});
	}

	[Fact]
	public void UpdateDepth_OverwritePreviousValue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.UpdateDepth(100, "orders-dlq");
			_metrics.UpdateDepth(150, "orders-dlq");
			_metrics.UpdateDepth(75, "orders-dlq");
		});
	}

	[Fact]
	public void UpdateDepth_WithZeroDepth()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateDepth(0, "orders-dlq"));
	}

	[Fact]
	public void UpdateDepth_WithLargeDepth()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateDepth(1_000_000, "high-volume-dlq"));
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var metrics = new DeadLetterQueueMetrics();

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.Dispose());
	}

	[Fact]
	public void AllowMultipleDisposeCalls()
	{
		// Arrange
		var metrics = new DeadLetterQueueMetrics();

		// Act & Assert - Multiple dispose calls should not throw
		Should.NotThrow(() =>
		{
			metrics.Dispose();
			metrics.Dispose();
		});
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void WorkTogether_RecordFullDlqCycle()
	{
		// Act & Assert - Complete DLQ lifecycle should not throw
		Should.NotThrow(() =>
		{
			// Messages get enqueued to DLQ after failures
			_metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded", "orders-queue");
			_metrics.RecordEnqueued("OrderCreated", "MaxRetriesExceeded", "orders-queue");
			_metrics.RecordEnqueued("PaymentFailed", "ValidationError", "payments-queue");

			// Update depth to reflect current state
			_metrics.UpdateDepth(3, "default");

			// Attempt to replay some messages
			_metrics.RecordReplayed("OrderCreated", success: true);
			_metrics.UpdateDepth(2, "default");

			_metrics.RecordReplayed("OrderCreated", success: false);
			// Failed replay, stays in DLQ

			// Purge expired messages
			_metrics.RecordPurged(1, "Expired");
			_metrics.UpdateDepth(1, "default");
		});
	}

	#endregion
}
