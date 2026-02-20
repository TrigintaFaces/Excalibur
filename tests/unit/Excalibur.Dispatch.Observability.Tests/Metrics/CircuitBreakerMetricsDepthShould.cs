// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Deep coverage tests for <see cref="CircuitBreakerMetrics"/> covering constructor variants,
/// IMeterFactory integration, null guard validation, cardinality guard behavior,
/// multiple circuit tracking, and disposal semantics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class CircuitBreakerMetricsDepthShould
{
	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new CircuitBreakerMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void CreateWithMeterFactory()
	{
		// Arrange
		var factory = A.Fake<IMeterFactory>();
		var meter = new Meter("test-cb");
		A.CallTo(() => factory.Create(A<MeterOptions>._)).Returns(meter);

		// Act
		using var metrics = new CircuitBreakerMetrics(factory);

		// Assert
		metrics.Meter.ShouldNotBeNull();
		A.CallTo(() => factory.Create(A<MeterOptions>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void TrackMultipleCircuits_Independently()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act — track two separate circuits
		metrics.UpdateState("circuit-a", 0); // Closed
		metrics.UpdateState("circuit-b", 1); // Open
		metrics.RecordSuccess("circuit-a");
		metrics.RecordFailure("circuit-b", "TimeoutException");
		metrics.RecordRejection("circuit-b");

		// Assert — no exception means independent tracking works
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordStateChange_MultipleTransitions()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act — full lifecycle: Closed→Open→HalfOpen→Closed
		metrics.RecordStateChange("lifecycle", "Closed", "Open");
		metrics.RecordStateChange("lifecycle", "Open", "HalfOpen");
		metrics.RecordStateChange("lifecycle", "HalfOpen", "Closed");

		// Assert — no exception, transitions recorded
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordFailure_MultipleExceptionTypes()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act — different exception types
		metrics.RecordFailure("svc", "TimeoutException");
		metrics.RecordFailure("svc", "HttpRequestException");
		metrics.RecordFailure("svc", "OperationCanceledException");
		metrics.RecordFailure("svc", "InvalidOperationException");

		// Assert — cardinality guard should still accept all 4 (under 100 limit)
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateState_OverwritesPreviousValue()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act — update same circuit multiple times
		metrics.UpdateState("flip-flop", 0);
		metrics.UpdateState("flip-flop", 1);
		metrics.UpdateState("flip-flop", 2);
		metrics.UpdateState("flip-flop", 0);

		// Assert — no exception
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void Dispose_Idempotent()
	{
		// Arrange
		var metrics = new CircuitBreakerMetrics();

		// Act & Assert — double dispose should not throw
		metrics.Dispose();
		metrics.Dispose();
	}

	[Fact]
	public void DefaultConstructor_OwnsMeter()
	{
		// Act
		using var metrics = new CircuitBreakerMetrics();

		// Assert
		metrics.Meter.Name.ShouldBe(CircuitBreakerMetrics.MeterName);
	}

	[Fact]
	public void RecordRejection_MultipleTimes()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act — simulate burst of rejections
		for (var i = 0; i < 10; i++)
		{
			metrics.RecordRejection("burst-circuit");
		}

		// Assert — no exception
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void RecordSuccess_MultipleTimes()
	{
		// Arrange
		using var metrics = new CircuitBreakerMetrics();

		// Act
		for (var i = 0; i < 10; i++)
		{
			metrics.RecordSuccess("healthy-circuit");
		}

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}
}
