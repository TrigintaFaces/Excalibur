// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class CircuitBreakerMetricsShould : UnitTestBase
{
	private readonly CircuitBreakerMetrics _metrics;

	public CircuitBreakerMetricsShould()
	{
		_metrics = new CircuitBreakerMetrics();
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
		_metrics.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		// Assert
		CircuitBreakerMetrics.MeterName.ShouldBe("Excalibur.Dispatch.CircuitBreaker");
	}

	#endregion

	#region RecordStateChange Tests

	[Fact]
	public void RecordStateChange_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordStateChange("order-circuit", "Closed", "Open"));
	}

	[Fact]
	public void RecordStateChange_ClosedToOpen()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordStateChange("payment-circuit", "Closed", "Open"));
	}

	[Fact]
	public void RecordStateChange_OpenToHalfOpen()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordStateChange("payment-circuit", "Open", "HalfOpen"));
	}

	[Fact]
	public void RecordStateChange_HalfOpenToClosed()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordStateChange("payment-circuit", "HalfOpen", "Closed"));
	}

	[Fact]
	public void RecordStateChange_HalfOpenToOpen()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordStateChange("payment-circuit", "HalfOpen", "Open"));
	}

	#endregion

	#region RecordRejection Tests

	[Fact]
	public void RecordRejection_WithCircuitName()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordRejection("order-circuit"));
	}

	[Fact]
	public void RecordRejection_MultipleTimes()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordRejection("order-circuit");
			_metrics.RecordRejection("order-circuit");
			_metrics.RecordRejection("payment-circuit");
		});
	}

	#endregion

	#region UpdateState Tests

	[Fact]
	public void UpdateState_ToClosed()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateState("order-circuit", 0));
	}

	[Fact]
	public void UpdateState_ToOpen()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateState("order-circuit", 1));
	}

	[Fact]
	public void UpdateState_ToHalfOpen()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.UpdateState("order-circuit", 2));
	}

	[Fact]
	public void UpdateState_MultipleCircuits()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.UpdateState("order-circuit", 0);
			_metrics.UpdateState("payment-circuit", 1);
			_metrics.UpdateState("notification-circuit", 2);
		});
	}

	[Fact]
	public void UpdateState_OverwritePreviousValue()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.UpdateState("order-circuit", 0);
			_metrics.UpdateState("order-circuit", 1);
			_metrics.UpdateState("order-circuit", 2);
		});
	}

	#endregion

	#region RecordFailure Tests

	[Fact]
	public void RecordFailure_WithRequiredParameters()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordFailure("order-circuit", "TimeoutException"));
	}

	[Fact]
	public void RecordFailure_WithDifferentExceptionTypes()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordFailure("order-circuit", "TimeoutException");
			_metrics.RecordFailure("order-circuit", "InvalidOperationException");
			_metrics.RecordFailure("order-circuit", "HttpRequestException");
		});
	}

	[Fact]
	public void RecordFailure_MultipleCircuits()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordFailure("order-circuit", "TimeoutException");
			_metrics.RecordFailure("payment-circuit", "InsufficientFundsException");
		});
	}

	#endregion

	#region RecordSuccess Tests

	[Fact]
	public void RecordSuccess_WithCircuitName()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => _metrics.RecordSuccess("order-circuit"));
	}

	[Fact]
	public void RecordSuccess_MultipleTimes()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordSuccess("order-circuit");
			_metrics.RecordSuccess("order-circuit");
			_metrics.RecordSuccess("order-circuit");
		});
	}

	[Fact]
	public void RecordSuccess_MultipleCircuits()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_metrics.RecordSuccess("order-circuit");
			_metrics.RecordSuccess("payment-circuit");
			_metrics.RecordSuccess("notification-circuit");
		});
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.Dispose());
	}

	[Fact]
	public void AllowMultipleDisposeCalls()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

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
	public void WorkTogether_RecordFullCircuitBreakerCycle()
	{
		// Act & Assert - Complete circuit breaker lifecycle should not throw
		Should.NotThrow(() =>
		{
			// Start with closed circuit
			_metrics.UpdateState("test-circuit", 0);

			// Record some successes
			_metrics.RecordSuccess("test-circuit");
			_metrics.RecordSuccess("test-circuit");

			// Then a failure occurs
			_metrics.RecordFailure("test-circuit", "TimeoutException");
			_metrics.RecordFailure("test-circuit", "TimeoutException");
			_metrics.RecordFailure("test-circuit", "TimeoutException");

			// Circuit opens
			_metrics.RecordStateChange("test-circuit", "Closed", "Open");
			_metrics.UpdateState("test-circuit", 1);

			// Requests get rejected
			_metrics.RecordRejection("test-circuit");
			_metrics.RecordRejection("test-circuit");

			// Circuit goes to half-open
			_metrics.RecordStateChange("test-circuit", "Open", "HalfOpen");
			_metrics.UpdateState("test-circuit", 2);

			// Trial request succeeds
			_metrics.RecordSuccess("test-circuit");

			// Circuit closes
			_metrics.RecordStateChange("test-circuit", "HalfOpen", "Closed");
			_metrics.UpdateState("test-circuit", 0);
		});
	}

	#endregion
}
