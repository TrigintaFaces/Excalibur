// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="CircuitBreakerMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class CircuitBreakerMetricsShould : IDisposable
{
	private CircuitBreakerMetrics? _metrics;

	public void Dispose() => _metrics?.Dispose();

	[Fact]
	public void CreateMeterOnConstruction()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UseCorrectMeterName()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);
	}

	[Fact]
	public void HaveCorrectMeterNameConstant()
	{
		CircuitBreakerMetrics.MeterName.ShouldBe("Excalibur.Dispatch.CircuitBreaker");
	}

	[Fact]
	public void RecordStateChange_WithoutThrowing()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.RecordStateChange("my-circuit", "Closed", "Open");
	}

	[Fact]
	public void RecordRejection_WithoutThrowing()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.RecordRejection("my-circuit");
	}

	[Fact]
	public void UpdateState_WithoutThrowing()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.UpdateState("my-circuit", 0); // Closed
		_metrics.UpdateState("my-circuit", 1); // Open
		_metrics.UpdateState("my-circuit", 2); // HalfOpen
	}

	[Fact]
	public void RecordFailure_WithoutThrowing()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.RecordFailure("my-circuit", "TimeoutException");
	}

	[Fact]
	public void RecordSuccess_WithoutThrowing()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.RecordSuccess("my-circuit");
	}

	[Fact]
	public void ImplementICircuitBreakerMetrics()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.ShouldBeAssignableTo<ICircuitBreakerMetrics>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_metrics = new CircuitBreakerMetrics();
		_metrics.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void SupportCompleteWorkflow()
	{
		_metrics = new CircuitBreakerMetrics();

		// Simulate circuit breaker lifecycle
		_metrics.RecordSuccess("order-service");
		_metrics.RecordSuccess("order-service");
		_metrics.RecordFailure("order-service", "TimeoutException");
		_metrics.RecordFailure("order-service", "TimeoutException");
		_metrics.RecordStateChange("order-service", "Closed", "Open");
		_metrics.UpdateState("order-service", 1);
		_metrics.RecordRejection("order-service");
		_metrics.RecordStateChange("order-service", "Open", "HalfOpen");
		_metrics.UpdateState("order-service", 2);
		_metrics.RecordSuccess("order-service");
		_metrics.RecordStateChange("order-service", "HalfOpen", "Closed");
		_metrics.UpdateState("order-service", 0);
	}
}
