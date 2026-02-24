// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

namespace Excalibur.Dispatch.Tests.Timing;

/// <summary>
///     Tests for the <see cref="DefaultTimeoutMonitor" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultTimeoutMonitorShould
{
	private readonly DefaultTimeoutMonitor _sut = new();

	[Fact]
	public void StartOperationSuccessfully()
	{
		var token = _sut.StartOperation(TimeoutOperationType.Pipeline);
		token.ShouldNotBeNull();
		token.OperationType.ShouldBe(TimeoutOperationType.Pipeline);
	}

	[Fact]
	public void CompleteOperationSuccessfully()
	{
		var token = _sut.StartOperation(TimeoutOperationType.Pipeline);
		Should.NotThrow(() => _sut.CompleteOperation(token, success: true, timedOut: false));
	}

	[Fact]
	public void ThrowForNullTokenOnComplete() =>
		Should.Throw<ArgumentNullException>(() => _sut.CompleteOperation(null!, true, false));

	[Fact]
	public void TrackStatisticsAfterOperations()
	{
		var token1 = _sut.StartOperation(TimeoutOperationType.Handler);
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);
		_sut.CompleteOperation(token1, success: true, timedOut: false);

		var token2 = _sut.StartOperation(TimeoutOperationType.Handler);
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);
		_sut.CompleteOperation(token2, success: false, timedOut: true);

		var stats = _sut.GetStatistics(TimeoutOperationType.Handler);
		stats.TotalOperations.ShouldBe(2);
		stats.SuccessfulOperations.ShouldBe(1);
		stats.TimedOutOperations.ShouldBe(1);
	}

	[Fact]
	public void ReturnDefaultStatisticsWhenNoData()
	{
		var stats = _sut.GetStatistics(TimeoutOperationType.Middleware);
		stats.TotalOperations.ShouldBe(0);
		stats.AverageDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ReturnDefaultRecommendedTimeoutWhenNoData()
	{
		var timeout = _sut.GetRecommendedTimeout(TimeoutOperationType.Pipeline);
		timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ClearStatisticsForSpecificType()
	{
		var token = _sut.StartOperation(TimeoutOperationType.Handler);
		_sut.CompleteOperation(token, true, false);

		_sut.GetSampleCount(TimeoutOperationType.Handler).ShouldBe(1);

		_sut.ClearStatistics(TimeoutOperationType.Handler);

		_sut.GetSampleCount(TimeoutOperationType.Handler).ShouldBe(0);
	}

	[Fact]
	public void ClearAllStatistics()
	{
		var token1 = _sut.StartOperation(TimeoutOperationType.Handler);
		_sut.CompleteOperation(token1, true, false);

		var token2 = _sut.StartOperation(TimeoutOperationType.Pipeline);
		_sut.CompleteOperation(token2, true, false);

		_sut.ClearStatistics();

		_sut.GetSampleCount(TimeoutOperationType.Handler).ShouldBe(0);
		_sut.GetSampleCount(TimeoutOperationType.Pipeline).ShouldBe(0);
	}

	[Fact]
	public void ReportSufficientSamplesCorrectly()
	{
		_sut.HasSufficientSamples(TimeoutOperationType.Handler, minimumSamples: 5).ShouldBeFalse();

		for (var i = 0; i < 5; i++)
		{
			var token = _sut.StartOperation(TimeoutOperationType.Handler);
			_sut.CompleteOperation(token, true, false);
		}

		_sut.HasSufficientSamples(TimeoutOperationType.Handler, minimumSamples: 5).ShouldBeTrue();
	}

	[Fact]
	public void GetSampleCount()
	{
		_sut.GetSampleCount(TimeoutOperationType.Handler).ShouldBe(0);

		var token = _sut.StartOperation(TimeoutOperationType.Handler);
		_sut.CompleteOperation(token, true, false);

		_sut.GetSampleCount(TimeoutOperationType.Handler).ShouldBe(1);
	}
}
