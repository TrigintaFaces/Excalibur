// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="MaterializedViewMetrics"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Health Checks &amp; OpenTelemetry Metrics tests.
/// Tests verify metrics recording, failure tracking, and state management.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Metrics")]
public sealed class MaterializedViewMetricsShould : IDisposable
{
	private readonly MaterializedViewMetrics _metrics;

	public MaterializedViewMetricsShould()
	{
		_metrics = new MaterializedViewMetrics();
	}

	public void Dispose()
	{
		_metrics.Dispose();
	}

	#region Meter Configuration Tests

	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		MaterializedViewMetrics.MeterName.ShouldBe("Excalibur.EventSourcing.MaterializedViews");
	}

	#endregion

	#region RecordRefreshSuccess Tests

	[Fact]
	public void RecordRefreshSuccess_UpdateViewState()
	{
		// Act
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		// Assert - staleness should be near zero since we just refreshed
		var staleness = _metrics.GetMaxStalenessSeconds();
		staleness.ShouldBeLessThan(1.0);
	}

	[Fact]
	public void RecordRefreshSuccess_ResetFailureState()
	{
		// Arrange - first fail
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50));

		// Act - then succeed
		_metrics.RecordRefreshSuccess("TestView", TimeSpan.FromMilliseconds(100), 5);

		// Assert - failure rate should not be 100% anymore
		var failureRate = _metrics.GetFailureRatePercent();
		failureRate.ShouldBeLessThan(100.0);
	}

	[Fact]
	public void RecordRefreshSuccess_ThrowOnNullViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_metrics.RecordRefreshSuccess(null!, TimeSpan.FromMilliseconds(100), 5));
	}

	[Fact]
	public void RecordRefreshSuccess_ThrowOnEmptyViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_metrics.RecordRefreshSuccess("", TimeSpan.FromMilliseconds(100), 5));
	}

	#endregion

	#region RecordRefreshFailure Tests

	[Fact]
	public void RecordRefreshFailure_IncrementFailureCount()
	{
		// Act
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50));

		// Assert
		var failureRate = _metrics.GetFailureRatePercent();
		failureRate.ShouldBe(100.0); // 1 failure out of 1 attempt
	}

	[Fact]
	public void RecordRefreshFailure_ThrowOnNullViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_metrics.RecordRefreshFailure(null!, TimeSpan.FromMilliseconds(50)));
	}

	[Fact]
	public void RecordRefreshFailure_ThrowOnEmptyViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_metrics.RecordRefreshFailure("", TimeSpan.FromMilliseconds(50)));
	}

	[Fact]
	public void RecordRefreshFailure_AcceptOptionalErrorType()
	{
		// Act - should not throw
		_metrics.RecordRefreshFailure("TestView", TimeSpan.FromMilliseconds(50), "DatabaseTimeout");

		// Assert
		var failureRate = _metrics.GetFailureRatePercent();
		failureRate.ShouldBe(100.0);
	}

	#endregion

	#region GetMaxStalenessSeconds Tests

	[Fact]
	public void GetMaxStalenessSeconds_ReturnZeroWithNoViews()
	{
		// Act
		var staleness = _metrics.GetMaxStalenessSeconds();

		// Assert
		staleness.ShouldBe(0);
	}

	[Fact]
	public void GetMaxStalenessSeconds_TrackMultipleViews()
	{
		// Arrange - refresh two views at different times
		_metrics.RecordRefreshSuccess("View1", TimeSpan.FromMilliseconds(100), 5);
		Thread.Sleep(100); // Introduce delay
		_metrics.RecordRefreshSuccess("View2", TimeSpan.FromMilliseconds(100), 5);

		// Act
		var staleness = _metrics.GetMaxStalenessSeconds();

		// Assert - View1 should be staler
		staleness.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void GetMaxStalenessSeconds_ReturnMaxValueForNeverRefreshedView()
	{
		// Arrange - mark a view as failed without prior success
		_metrics.RecordRefreshFailure("NeverRefreshedView", TimeSpan.FromMilliseconds(50));

		// Act
		var staleness = _metrics.GetMaxStalenessSeconds();

		// Assert - should be max because view was never successfully refreshed
		staleness.ShouldBe(double.MaxValue);
	}

	#endregion

	#region GetFailureRatePercent Tests

	[Fact]
	public void GetFailureRatePercent_ReturnZeroWithNoAttempts()
	{
		// Act
		var rate = _metrics.GetFailureRatePercent();

		// Assert
		rate.ShouldBe(0);
	}

	[Fact]
	public void GetFailureRatePercent_CalculateCorrectRate()
	{
		// Arrange - 1 success, 1 failure = 50%
		_metrics.RecordRefreshSuccess("View1", TimeSpan.FromMilliseconds(100), 5);
		_metrics.RecordRefreshFailure("View2", TimeSpan.FromMilliseconds(50));

		// Act
		var rate = _metrics.GetFailureRatePercent();

		// Assert
		rate.ShouldBe(50.0);
	}

	[Fact]
	public void GetFailureRatePercent_TrackMultipleAttempts()
	{
		// Arrange - 3 successes, 1 failure = 25%
		_metrics.RecordRefreshSuccess("View1", TimeSpan.FromMilliseconds(100), 5);
		_metrics.RecordRefreshSuccess("View2", TimeSpan.FromMilliseconds(100), 5);
		_metrics.RecordRefreshSuccess("View3", TimeSpan.FromMilliseconds(100), 5);
		_metrics.RecordRefreshFailure("View4", TimeSpan.FromMilliseconds(50));

		// Act
		var rate = _metrics.GetFailureRatePercent();

		// Assert
		rate.ShouldBe(25.0);
	}

	#endregion

	#region ResetFailureTracking Tests

	[Fact]
	public void ResetFailureTracking_ClearCounters()
	{
		// Arrange - record some failures
		_metrics.RecordRefreshFailure("View1", TimeSpan.FromMilliseconds(50));
		_metrics.RecordRefreshFailure("View2", TimeSpan.FromMilliseconds(50));

		// Act
		_metrics.ResetFailureTracking();

		// Assert
		var rate = _metrics.GetFailureRatePercent();
		rate.ShouldBe(0);
	}

	#endregion

	#region StartRefresh Tests

	[Fact]
	public void StartRefresh_ReturnRunningStopwatch()
	{
		// Act
		var stopwatch = _metrics.StartRefresh("TestView");

		// Assert
		stopwatch.ShouldNotBeNull();
		stopwatch.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public void StartRefresh_ThrowOnNullViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _metrics.StartRefresh(null!));
	}

	[Fact]
	public void StartRefresh_ThrowOnEmptyViewName()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _metrics.StartRefresh(""));
	}

	#endregion

	#region Disposal Tests

	[Fact]
	public void Dispose_BeIdempotent()
	{
		// Arrange
		using var metrics = new MaterializedViewMetrics();

		// Act - dispose twice should not throw
		metrics.Dispose();
		metrics.Dispose();
	}

	#endregion
}
