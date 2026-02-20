// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuthenticationProtectionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new AuthenticationProtectionOptions();

		sut.Enabled.ShouldBeTrue();
		sut.MaxConsecutiveFailures.ShouldBe(5);
		sut.LockoutDuration.ShouldBe(TimeSpan.FromMinutes(15));
		sut.FailureWindow.ShouldBe(TimeSpan.FromHours(1));
		sut.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new AuthenticationProtectionOptions
		{
			Enabled = false,
			MaxConsecutiveFailures = 10,
			LockoutDuration = TimeSpan.FromMinutes(30),
			FailureWindow = TimeSpan.FromMinutes(30),
			UseExponentialBackoff = false,
		};

		sut.Enabled.ShouldBeFalse();
		sut.MaxConsecutiveFailures.ShouldBe(10);
		sut.LockoutDuration.ShouldBe(TimeSpan.FromMinutes(30));
		sut.FailureWindow.ShouldBe(TimeSpan.FromMinutes(30));
		sut.UseExponentialBackoff.ShouldBeFalse();
	}
}
