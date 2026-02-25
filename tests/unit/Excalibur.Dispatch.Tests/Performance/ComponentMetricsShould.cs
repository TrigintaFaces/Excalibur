// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="ComponentMetrics" /> record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ComponentMetricsShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		var sut = new ComponentMetrics
		{
			ExecutionCount = 100,
			TotalDuration = TimeSpan.FromSeconds(10),
			AverageDuration = TimeSpan.FromMilliseconds(100),
			MinDuration = TimeSpan.FromMilliseconds(5),
			MaxDuration = TimeSpan.FromMilliseconds(500),
			SuccessCount = 95,
			FailureCount = 5,
			SuccessRate = 0.95,
		};

		sut.ExecutionCount.ShouldBe(100);
		sut.TotalDuration.ShouldBe(TimeSpan.FromSeconds(10));
		sut.AverageDuration.ShouldBe(TimeSpan.FromMilliseconds(100));
		sut.MinDuration.ShouldBe(TimeSpan.FromMilliseconds(5));
		sut.MaxDuration.ShouldBe(TimeSpan.FromMilliseconds(500));
		sut.SuccessCount.ShouldBe(95);
		sut.FailureCount.ShouldBe(5);
		sut.SuccessRate.ShouldBe(0.95);
	}

	[Fact]
	public void SupportValueEquality()
	{
		var a = new ComponentMetrics
		{
			ExecutionCount = 10,
			TotalDuration = TimeSpan.FromSeconds(1),
			AverageDuration = TimeSpan.FromMilliseconds(100),
			MinDuration = TimeSpan.FromMilliseconds(50),
			MaxDuration = TimeSpan.FromMilliseconds(200),
			SuccessCount = 10,
			FailureCount = 0,
			SuccessRate = 1.0,
		};

		var b = new ComponentMetrics
		{
			ExecutionCount = 10,
			TotalDuration = TimeSpan.FromSeconds(1),
			AverageDuration = TimeSpan.FromMilliseconds(100),
			MinDuration = TimeSpan.FromMilliseconds(50),
			MaxDuration = TimeSpan.FromMilliseconds(200),
			SuccessCount = 10,
			FailureCount = 0,
			SuccessRate = 1.0,
		};

		a.ShouldBe(b);
	}

	[Fact]
	public void SupportWithExpression()
	{
		var original = new ComponentMetrics
		{
			ExecutionCount = 10,
			TotalDuration = TimeSpan.FromSeconds(1),
			AverageDuration = TimeSpan.FromMilliseconds(100),
			MinDuration = TimeSpan.FromMilliseconds(50),
			MaxDuration = TimeSpan.FromMilliseconds(200),
			SuccessCount = 10,
			FailureCount = 0,
			SuccessRate = 1.0,
		};

		var modified = original with { FailureCount = 2, SuccessRate = 0.8 };

		modified.FailureCount.ShouldBe(2);
		modified.SuccessRate.ShouldBe(0.8);
		modified.ExecutionCount.ShouldBe(10); // unchanged
	}
}
