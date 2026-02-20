// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ServiceAccountOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ServiceAccountOptions();

		sut.Enabled.ShouldBeFalse();
		sut.AccountId.ShouldBeNull();
		sut.Namespace.ShouldBeNull();
		sut.TokenExpiration.ShouldBe(TimeSpan.FromHours(1));
		sut.RefreshThreshold.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ServiceAccountOptions
		{
			Enabled = true,
			AccountId = "svc-elastic-reader",
			Namespace = "production",
			TokenExpiration = TimeSpan.FromHours(8),
			RefreshThreshold = TimeSpan.FromMinutes(30),
		};

		sut.Enabled.ShouldBeTrue();
		sut.AccountId.ShouldBe("svc-elastic-reader");
		sut.Namespace.ShouldBe("production");
		sut.TokenExpiration.ShouldBe(TimeSpan.FromHours(8));
		sut.RefreshThreshold.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
