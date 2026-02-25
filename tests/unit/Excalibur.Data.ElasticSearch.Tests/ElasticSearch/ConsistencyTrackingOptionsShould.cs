// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConsistencyTrackingOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ConsistencyTrackingOptions();

		sut.Enabled.ShouldBeTrue();
		sut.ExpectedMaxLag.ShouldBe(TimeSpan.FromSeconds(5));
		sut.SLAPercentage.ShouldBe(99.0);
		sut.MetricsInterval.ShouldBe(TimeSpan.FromMinutes(1));
		sut.EnableAlerting.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ConsistencyTrackingOptions
		{
			Enabled = false,
			ExpectedMaxLag = TimeSpan.FromSeconds(10),
			SLAPercentage = 99.99,
			MetricsInterval = TimeSpan.FromSeconds(30),
			EnableAlerting = false,
		};

		sut.Enabled.ShouldBeFalse();
		sut.ExpectedMaxLag.ShouldBe(TimeSpan.FromSeconds(10));
		sut.SLAPercentage.ShouldBe(99.99);
		sut.MetricsInterval.ShouldBe(TimeSpan.FromSeconds(30));
		sut.EnableAlerting.ShouldBeFalse();
	}
}
