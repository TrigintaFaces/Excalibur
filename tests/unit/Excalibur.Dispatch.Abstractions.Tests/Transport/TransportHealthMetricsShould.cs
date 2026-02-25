// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportHealthMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportHealthMetricsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsLastCheckTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var metrics = CreateMetrics(lastCheckTimestamp: timestamp);

		// Assert
		metrics.LastCheckTimestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Constructor_SetsLastStatus()
	{
		// Act
		var metrics = CreateMetrics(lastStatus: TransportHealthStatus.Degraded);

		// Assert
		metrics.LastStatus.ShouldBe(TransportHealthStatus.Degraded);
	}

	[Fact]
	public void Constructor_SetsConsecutiveFailures()
	{
		// Act
		var metrics = CreateMetrics(consecutiveFailures: 5);

		// Assert
		metrics.ConsecutiveFailures.ShouldBe(5);
	}

	[Fact]
	public void Constructor_SetsTotalChecks()
	{
		// Act
		var metrics = CreateMetrics(totalChecks: 1000);

		// Assert
		metrics.TotalChecks.ShouldBe(1000);
	}

	[Fact]
	public void Constructor_SetsSuccessRate()
	{
		// Act
		var metrics = CreateMetrics(successRate: 0.95);

		// Assert
		metrics.SuccessRate.ShouldBe(0.95);
	}

	[Fact]
	public void Constructor_SetsAverageCheckDuration()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(150);

		// Act
		var metrics = CreateMetrics(averageCheckDuration: duration);

		// Assert
		metrics.AverageCheckDuration.ShouldBe(duration);
	}

	[Fact]
	public void Constructor_WithNullCustomMetrics_SetsEmptyCustomMetrics()
	{
		// Act
		var metrics = CreateMetrics(customMetrics: null);

		// Assert
		_ = metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithCustomMetrics_SetsCustomMetrics()
	{
		// Arrange
		var customMetrics = new Dictionary<string, object> { ["latency_p99"] = 200 };

		// Act
		var metrics = CreateMetrics(customMetrics: customMetrics);

		// Assert
		metrics.CustomMetrics.ShouldContainKeyAndValue("latency_p99", 200);
	}

	#endregion

	#region IsHealthy Tests

	[Fact]
	public void IsHealthy_WhenLastStatusIsHealthy_ReturnsTrue()
	{
		// Arrange
		var metrics = CreateMetrics(lastStatus: TransportHealthStatus.Healthy);

		// Assert
		metrics.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void IsHealthy_WhenLastStatusIsDegraded_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(lastStatus: TransportHealthStatus.Degraded);

		// Assert
		metrics.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void IsHealthy_WhenLastStatusIsUnhealthy_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(lastStatus: TransportHealthStatus.Unhealthy);

		// Assert
		metrics.IsHealthy.ShouldBeFalse();
	}

	#endregion

	#region IsStable Tests

	[Fact]
	public void IsStable_WhenNoFailuresAndHighSuccessRate_ReturnsTrue()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 0, successRate: 0.98);

		// Assert
		metrics.IsStable.ShouldBeTrue();
	}

	[Fact]
	public void IsStable_WhenNoFailuresAndExactly95PercentSuccessRate_ReturnsTrue()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 0, successRate: 0.95);

		// Assert
		metrics.IsStable.ShouldBeTrue();
	}

	[Fact]
	public void IsStable_WhenNoFailuresAndLowSuccessRate_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 0, successRate: 0.94);

		// Assert
		metrics.IsStable.ShouldBeFalse();
	}

	[Fact]
	public void IsStable_WhenConsecutiveFailuresAndHighSuccessRate_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 1, successRate: 0.99);

		// Assert
		metrics.IsStable.ShouldBeFalse();
	}

	[Fact]
	public void IsStable_WhenConsecutiveFailuresAndLowSuccessRate_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 3, successRate: 0.80);

		// Assert
		metrics.IsStable.ShouldBeFalse();
	}

	[Fact]
	public void IsStable_WhenNoFailuresAndPerfectSuccessRate_ReturnsTrue()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 0, successRate: 1.0);

		// Assert
		metrics.IsStable.ShouldBeTrue();
	}

	[Fact]
	public void IsStable_WhenNoFailuresAndZeroSuccessRate_ReturnsFalse()
	{
		// Arrange
		var metrics = CreateMetrics(consecutiveFailures: 0, successRate: 0.0);

		// Assert
		metrics.IsStable.ShouldBeFalse();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void Constructor_WithZeroTotalChecks_SetsZero()
	{
		// Act
		var metrics = CreateMetrics(totalChecks: 0);

		// Assert
		metrics.TotalChecks.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithNegativeConsecutiveFailures_SetsNegativeValue()
	{
		// Act - edge case, shouldn't happen in practice but testing behavior
		var metrics = CreateMetrics(consecutiveFailures: -1);

		// Assert
		metrics.ConsecutiveFailures.ShouldBe(-1);
	}

	[Fact]
	public void Constructor_WithSuccessRateGreaterThanOne_SetsValue()
	{
		// Act - edge case, shouldn't happen in practice
		var metrics = CreateMetrics(successRate: 1.5);

		// Assert
		metrics.SuccessRate.ShouldBe(1.5);
	}

	[Fact]
	public void Constructor_WithZeroAverageCheckDuration_SetsZero()
	{
		// Act
		var metrics = CreateMetrics(averageCheckDuration: TimeSpan.Zero);

		// Assert
		metrics.AverageCheckDuration.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Helper Methods

	private static TransportHealthMetrics CreateMetrics(
		DateTimeOffset? lastCheckTimestamp = null,
		TransportHealthStatus lastStatus = TransportHealthStatus.Healthy,
		int consecutiveFailures = 0,
		long totalChecks = 100,
		double successRate = 0.99,
		TimeSpan? averageCheckDuration = null,
		IReadOnlyDictionary<string, object>? customMetrics = null)
	{
		return new TransportHealthMetrics(
			lastCheckTimestamp ?? DateTimeOffset.UtcNow,
			lastStatus,
			consecutiveFailures,
			totalChecks,
			successRate,
			averageCheckDuration ?? TimeSpan.FromMilliseconds(100),
			customMetrics);
	}

	#endregion
}
