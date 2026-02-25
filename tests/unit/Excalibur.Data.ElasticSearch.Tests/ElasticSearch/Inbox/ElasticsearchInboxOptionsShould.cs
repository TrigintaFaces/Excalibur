// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Inbox;

namespace Excalibur.Data.Tests.ElasticSearch.Inbox;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchInboxOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchInboxOptions();
		sut.IndexName.ShouldBe("excalibur-inbox");
		sut.RefreshPolicy.ShouldBe("wait_for");
		sut.RetentionDays.ShouldBe(7);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticsearchInboxOptions
		{
			IndexName = "custom-inbox",
			RefreshPolicy = "false",
			RetentionDays = 30,
		};

		sut.IndexName.ShouldBe("custom-inbox");
		sut.RefreshPolicy.ShouldBe("false");
		sut.RetentionDays.ShouldBe(30);
	}
}
