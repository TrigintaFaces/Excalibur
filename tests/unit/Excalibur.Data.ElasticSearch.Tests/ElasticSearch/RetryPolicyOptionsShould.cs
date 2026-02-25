// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class RetryPolicyOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new RetryPolicyOptions();

		sut.Enabled.ShouldBeTrue();
		sut.MaxAttempts.ShouldBe(3);
		sut.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		sut.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		sut.JitterFactor.ShouldBe(0.1);
		sut.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new RetryPolicyOptions
		{
			Enabled = false,
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromMinutes(1),
			JitterFactor = 0.3,
			UseExponentialBackoff = false,
		};

		sut.Enabled.ShouldBeFalse();
		sut.MaxAttempts.ShouldBe(5);
		sut.BaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		sut.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		sut.JitterFactor.ShouldBe(0.3);
		sut.UseExponentialBackoff.ShouldBeFalse();
	}
}
