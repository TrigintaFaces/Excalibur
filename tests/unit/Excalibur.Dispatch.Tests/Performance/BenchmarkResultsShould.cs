// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="BenchmarkResults" /> record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BenchmarkResultsShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		var testDate = DateTimeOffset.UtcNow;
		var sut = new BenchmarkResults
		{
			TestDate = testDate,
			Iterations = 1000,
		};

		sut.TestDate.ShouldBe(testDate);
		sut.Iterations.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new BenchmarkResults
		{
			TestDate = DateTimeOffset.UtcNow,
			Iterations = 100,
		};

		sut.TotalDuration.ShouldBe(TimeSpan.Zero);
		sut.MessagesPerSecond.ShouldBe(0.0);
		sut.AverageLatencyMs.ShouldBe(0.0);
		sut.PerformanceSnapshot.ShouldBeNull();
	}

	[Fact]
	public void SetTotalDuration()
	{
		var sut = new BenchmarkResults
		{
			TestDate = DateTimeOffset.UtcNow,
			Iterations = 500,
			TotalDuration = TimeSpan.FromSeconds(5),
		};

		sut.TotalDuration.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void SetMessagesPerSecond()
	{
		var sut = new BenchmarkResults
		{
			TestDate = DateTimeOffset.UtcNow,
			Iterations = 1000,
			MessagesPerSecond = 10_000.5,
		};

		sut.MessagesPerSecond.ShouldBe(10_000.5);
	}

	[Fact]
	public void SetAverageLatencyMs()
	{
		var sut = new BenchmarkResults
		{
			TestDate = DateTimeOffset.UtcNow,
			Iterations = 1000,
			AverageLatencyMs = 0.1,
		};

		sut.AverageLatencyMs.ShouldBe(0.1);
	}

	[Fact]
	public void SupportValueEquality()
	{
		var date = DateTimeOffset.UtcNow;
		var a = new BenchmarkResults { TestDate = date, Iterations = 100 };
		var b = new BenchmarkResults { TestDate = date, Iterations = 100 };

		a.ShouldBe(b);
	}
}
