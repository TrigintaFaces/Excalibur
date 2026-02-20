// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DispatchMetricsShould : UnitTestBase
{
	private readonly DispatchMetrics _metrics;

	public DispatchMetricsShould()
	{
		_metrics = new DispatchMetrics();
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
		_metrics.Meter.Name.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		// Assert
		DispatchMetrics.MeterName.ShouldBe("Excalibur.Dispatch.Core");
	}

	#endregion

	#region RecordMessageProcessed Tests

	[Fact]
	public void RecordMessageProcessed_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordMessageProcessed("OrderCreated", "OrderHandler"));
	}

	[Fact]
	public void RecordMessageProcessed_WithAdditionalTags()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordMessageProcessed(
			"OrderCreated",
			"OrderHandler",
			("tenant", "tenant-123"),
			("priority", 1)));
	}

	[Fact]
	public void RecordMessageProcessed_WithEmptyTags()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordMessageProcessed("OrderCreated", "OrderHandler"));
	}

	[Fact]
	public void RecordMessageProcessed_ThrowOnNullTags()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_metrics.RecordMessageProcessed("OrderCreated", "OrderHandler", null!));
	}

	#endregion

	#region RecordProcessingDuration Tests

	[Fact]
	public void RecordProcessingDuration_WithSuccessTrue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordProcessingDuration(150.5, "OrderCreated", success: true));
	}

	[Fact]
	public void RecordProcessingDuration_WithSuccessFalse()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordProcessingDuration(250.0, "OrderFailed", success: false));
	}

	[Fact]
	public void RecordProcessingDuration_WithZeroDuration()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordProcessingDuration(0.0, "FastMessage", success: true));
	}

	[Fact]
	public void RecordProcessingDuration_WithLargeDuration()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordProcessingDuration(30000.0, "SlowMessage", success: true));
	}

	#endregion

	#region RecordMessagePublished Tests

	[Fact]
	public void RecordMessagePublished_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordMessagePublished("OrderCreated", "orders-topic"));
	}

	[Fact]
	public void RecordMessagePublished_WithDifferentDestinations()
	{
		// Act & Assert - Should not throw for various destinations
		Should.NotThrow(() => _metrics.RecordMessagePublished("OrderCreated", "rabbitmq://orders"));
		Should.NotThrow(() => _metrics.RecordMessagePublished("PaymentProcessed", "kafka://payments"));
		Should.NotThrow(() => _metrics.RecordMessagePublished("NotificationSent", "azure-servicebus://notifications"));
	}

	#endregion

	#region RecordMessageFailed Tests

	[Fact]
	public void RecordMessageFailed_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordMessageFailed("OrderCreated", "ValidationException", 0));
	}

	[Fact]
	public void RecordMessageFailed_WithRetryAttempts()
	{
		// Act & Assert - Should not throw for various retry attempts
		Should.NotThrow(() => _metrics.RecordMessageFailed("OrderCreated", "TimeoutException", 1));
		Should.NotThrow(() => _metrics.RecordMessageFailed("OrderCreated", "TimeoutException", 2));
		Should.NotThrow(() => _metrics.RecordMessageFailed("OrderCreated", "TimeoutException", 3));
	}

	[Fact]
	public void RecordMessageFailed_WithDifferentErrorTypes()
	{
		// Act & Assert - Should not throw for various error types
		Should.NotThrow(() => _metrics.RecordMessageFailed("OrderCreated", "ValidationException", 0));
		Should.NotThrow(() => _metrics.RecordMessageFailed("PaymentProcessed", "InsufficientFundsException", 0));
		Should.NotThrow(() => _metrics.RecordMessageFailed("NotificationSent", "NetworkException", 1));
	}

	#endregion

	#region UpdateActiveSessions Tests

	[Fact]
	public void UpdateActiveSessions_WithZero()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateActiveSessions(0));
	}

	[Fact]
	public void UpdateActiveSessions_WithPositiveCount()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateActiveSessions(10));
		Should.NotThrow(() => _metrics.UpdateActiveSessions(100));
		Should.NotThrow(() => _metrics.UpdateActiveSessions(1000));
	}

	[Fact]
	public void UpdateActiveSessions_AllowsNegativeCount()
	{
		// Note: Gauge allows negative values - this verifies behavior
		// Act & Assert - Should not throw (gauges accept any int)
		Should.NotThrow(() => _metrics.UpdateActiveSessions(-1));
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.Dispose());
	}

	[Fact]
	public void AllowMultipleDisposeCalls()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Act & Assert - Multiple dispose calls should not throw
		Should.NotThrow(() =>
		{
			metrics.Dispose();
			metrics.Dispose();
		});
	}

	#endregion
}
