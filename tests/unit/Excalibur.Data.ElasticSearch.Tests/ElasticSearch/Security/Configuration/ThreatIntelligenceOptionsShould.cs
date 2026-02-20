// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ThreatIntelligenceOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ThreatIntelligenceOptions();

		sut.Enabled.ShouldBeFalse();
		sut.FeedUrls.ShouldNotBeNull();
		sut.FeedUrls.ShouldBeEmpty();
		sut.UpdateInterval.ShouldBe(TimeSpan.FromHours(1));
		sut.AutoBlockMaliciousIps.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ThreatIntelligenceOptions
		{
			Enabled = true,
			FeedUrls = ["https://feed1.example.com", "https://feed2.example.com"],
			UpdateInterval = TimeSpan.FromMinutes(30),
			AutoBlockMaliciousIps = false,
		};

		sut.Enabled.ShouldBeTrue();
		sut.FeedUrls.Count.ShouldBe(2);
		sut.UpdateInterval.ShouldBe(TimeSpan.FromMinutes(30));
		sut.AutoBlockMaliciousIps.ShouldBeFalse();
	}
}
