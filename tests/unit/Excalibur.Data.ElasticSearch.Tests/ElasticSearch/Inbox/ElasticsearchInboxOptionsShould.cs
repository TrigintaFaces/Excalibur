// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Inbox.ElasticSearch;
using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Data.Tests.ElasticSearch.Inbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticsearchInboxOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchInboxOptions();
		sut.IndexName.ShouldBe("excalibur-inbox");
		sut.RefreshPolicy.ShouldBe(ElasticsearchRefreshPolicy.WaitFor);
		sut.RetentionDays.ShouldBe(7);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticsearchInboxOptions
		{
			IndexName = "custom-inbox",
			RefreshPolicy = ElasticsearchRefreshPolicy.None,
			RetentionDays = 30,
		};

		sut.IndexName.ShouldBe("custom-inbox");
		sut.RefreshPolicy.ShouldBe(ElasticsearchRefreshPolicy.None);
		sut.RetentionDays.ShouldBe(30);
	}
}
