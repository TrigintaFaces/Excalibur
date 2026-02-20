// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ConfigurationChangeTypeShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		ConfigurationChangeType.SecuritySettings.ShouldBe((ConfigurationChangeType)0);
		ConfigurationChangeType.AuthenticationSettings.ShouldBe((ConfigurationChangeType)1);
		ConfigurationChangeType.AuthorizationSettings.ShouldBe((ConfigurationChangeType)2);
		ConfigurationChangeType.EncryptionSettings.ShouldBe((ConfigurationChangeType)3);
		ConfigurationChangeType.NetworkSettings.ShouldBe((ConfigurationChangeType)4);
		ConfigurationChangeType.ApplicationSettings.ShouldBe((ConfigurationChangeType)5);
		ConfigurationChangeType.Other.ShouldBe((ConfigurationChangeType)6);
	}

	[Fact]
	public void HaveExactlySevenMembers()
	{
		Enum.GetValues<ConfigurationChangeType>().Length.ShouldBe(7);
	}
}
