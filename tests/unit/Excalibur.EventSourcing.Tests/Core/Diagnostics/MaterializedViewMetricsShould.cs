// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Core.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MaterializedViewMetricsShould : IDisposable
{
	private readonly MaterializedViewMetrics _sut;

	public MaterializedViewMetricsShould()
	{
		// Use parameterless constructor (creates its own Meter internally)
		_sut = new MaterializedViewMetrics();
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	[Fact]
	public void ReturnZeroStaleness_WhenNoViewsRecorded()
	{
		_sut.GetMaxStalenessSeconds().ShouldBe(0);
	}

	[Fact]
	public void ReturnZeroFailureRate_WhenNoAttemptsRecorded()
	{
		_sut.GetFailureRatePercent().ShouldBe(0);
	}

	[Fact]
	public void TrackStaleness_AfterSuccessfulRefresh()
	{
		// Act
		_sut.RecordRefreshSuccess("OrderView", TimeSpan.FromMilliseconds(50), 10);

		// Assert - staleness should be very small (just recorded)
		_sut.GetMaxStalenessSeconds().ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void TrackFailureRate_AfterMixedResults()
	{
		// Arrange
		_sut.RecordRefreshSuccess("OrderView", TimeSpan.FromMilliseconds(50), 10);
		_sut.RecordRefreshFailure("OrderView", TimeSpan.FromMilliseconds(100));

		// Act
		var rate = _sut.GetFailureRatePercent();

		// Assert — 1 failure out of 2 attempts = 50%
		rate.ShouldBe(50.0);
	}

	[Fact]
	public void ReturnMaxValue_WhenViewNeverRefreshed()
	{
		// Arrange — record a failure (never successfully refreshed)
		_sut.RecordRefreshFailure("NeverRefreshedView", TimeSpan.FromMilliseconds(10));

		// Act
		var staleness = _sut.GetMaxStalenessSeconds();

		// Assert — should return MaxValue for never-refreshed views
		staleness.ShouldBe(double.MaxValue);
	}

	[Fact]
	public void ResetFailureTracking_ClearCounters()
	{
		// Arrange
		_sut.RecordRefreshFailure("OrderView", TimeSpan.FromMilliseconds(10));
		_sut.RecordRefreshFailure("OrderView", TimeSpan.FromMilliseconds(10));
		_sut.GetFailureRatePercent().ShouldBe(100.0);

		// Act
		_sut.ResetFailureTracking();

		// Assert
		_sut.GetFailureRatePercent().ShouldBe(0);
	}

	[Fact]
	public void RecordRefreshSuccess_ThrowOnNullViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.RecordRefreshSuccess(null!, TimeSpan.Zero, 0));
	}

	[Fact]
	public void RecordRefreshSuccess_ThrowOnEmptyViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.RecordRefreshSuccess("", TimeSpan.Zero, 0));
	}

	[Fact]
	public void RecordRefreshFailure_ThrowOnNullViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.RecordRefreshFailure(null!, TimeSpan.Zero));
	}

	[Fact]
	public void RecordRefreshFailure_ThrowOnEmptyViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.RecordRefreshFailure("", TimeSpan.Zero));
	}

	[Fact]
	public void RecordRefreshFailure_AcceptOptionalErrorType()
	{
		// Act & Assert — should not throw
		_sut.RecordRefreshFailure("OrderView", TimeSpan.FromMilliseconds(10), "TimeoutException");
		_sut.GetFailureRatePercent().ShouldBe(100.0);
	}

	[Fact]
	public void StartRefresh_ReturnStopwatch()
	{
		// Act
		var sw = _sut.StartRefresh("OrderView");

		// Assert
		sw.ShouldNotBeNull();
		sw.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public void StartRefresh_ThrowOnNullViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.StartRefresh(null!));
	}

	[Fact]
	public void StartRefresh_ThrowOnEmptyViewName()
	{
		Should.Throw<ArgumentException>(
			() => _sut.StartRefresh(""));
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(
			() => new MaterializedViewMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void TrackMultipleViews_ReturnMaxStaleness()
	{
		// Arrange — record two different views
		_sut.RecordRefreshSuccess("OrderView", TimeSpan.FromMilliseconds(10), 5);
		_sut.RecordRefreshSuccess("ProductView", TimeSpan.FromMilliseconds(10), 3);

		// Act
		var staleness = _sut.GetMaxStalenessSeconds();

		// Assert — both just refreshed, staleness should be small
		staleness.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void CalculateFailureRate_Correctly()
	{
		// Arrange — 3 successes, 1 failure = 25% failure rate
		_sut.RecordRefreshSuccess("v1", TimeSpan.FromMilliseconds(10), 1);
		_sut.RecordRefreshSuccess("v1", TimeSpan.FromMilliseconds(10), 1);
		_sut.RecordRefreshSuccess("v1", TimeSpan.FromMilliseconds(10), 1);
		_sut.RecordRefreshFailure("v1", TimeSpan.FromMilliseconds(10));

		// Act
		var rate = _sut.GetFailureRatePercent();

		// Assert
		rate.ShouldBe(25.0);
	}

	[Fact]
	public void Dispose_NotThrowOnDoubleDispose()
	{
		// Arrange — use parameterless constructor to create a separate instance
		var metrics = new MaterializedViewMetrics();

		// Act & Assert — should not throw
		metrics.Dispose();
		metrics.Dispose();
	}
}
