// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class CircuitBreakerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new CircuitBreakerOptions();

		sut.Enabled.ShouldBeTrue();
		sut.FailureThreshold.ShouldBe(5);
		sut.MinimumThroughput.ShouldBe(10);
		sut.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
		sut.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(60));
		sut.FailureRateThreshold.ShouldBe(0.5);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new CircuitBreakerOptions
		{
			Enabled = false,
			FailureThreshold = 10,
			MinimumThroughput = 20,
			BreakDuration = TimeSpan.FromMinutes(2),
			SamplingDuration = TimeSpan.FromMinutes(5),
			FailureRateThreshold = 0.8,
		};

		sut.Enabled.ShouldBeFalse();
		sut.FailureThreshold.ShouldBe(10);
		sut.MinimumThroughput.ShouldBe(20);
		sut.BreakDuration.ShouldBe(TimeSpan.FromMinutes(2));
		sut.SamplingDuration.ShouldBe(TimeSpan.FromMinutes(5));
		sut.FailureRateThreshold.ShouldBe(0.8);
	}
}
