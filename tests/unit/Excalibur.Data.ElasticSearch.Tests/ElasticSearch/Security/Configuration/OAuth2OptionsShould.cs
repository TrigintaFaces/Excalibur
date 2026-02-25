// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class OAuth2OptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new OAuth2Options();

		sut.Enabled.ShouldBeFalse();
		sut.Authority.ShouldBeNull();
		sut.ClientId.ShouldBeNull();
		sut.Scope.ShouldBe("elasticsearch:read elasticsearch:write");
		sut.Audience.ShouldBeNull();
		sut.RefreshBuffer.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new OAuth2Options
		{
			Enabled = true,
			Authority = "https://auth.example.com",
			ClientId = "my-client",
			Scope = "custom:scope",
			Audience = "https://elastic.example.com",
			RefreshBuffer = TimeSpan.FromMinutes(10),
		};

		sut.Enabled.ShouldBeTrue();
		sut.Authority.ShouldBe("https://auth.example.com");
		sut.ClientId.ShouldBe("my-client");
		sut.Scope.ShouldBe("custom:scope");
		sut.Audience.ShouldBe("https://elastic.example.com");
		sut.RefreshBuffer.ShouldBe(TimeSpan.FromMinutes(10));
	}
}
