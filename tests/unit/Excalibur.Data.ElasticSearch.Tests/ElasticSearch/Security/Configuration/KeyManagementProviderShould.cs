// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class KeyManagementProviderShould
{
	[Fact]
	public void DefineExpectedValues()
	{
		KeyManagementProvider.Local.ShouldBe((KeyManagementProvider)0);
		KeyManagementProvider.AzureKeyVault.ShouldBe((KeyManagementProvider)1);
		KeyManagementProvider.AwsKms.ShouldBe((KeyManagementProvider)2);
		KeyManagementProvider.GoogleCloudKms.ShouldBe((KeyManagementProvider)3);
		KeyManagementProvider.HashiCorpVault.ShouldBe((KeyManagementProvider)4);
		KeyManagementProvider.Hsm.ShouldBe((KeyManagementProvider)5);
	}

	[Fact]
	public void HaveExactlySixMembers()
	{
		Enum.GetValues<KeyManagementProvider>().Length.ShouldBe(6);
	}
}
