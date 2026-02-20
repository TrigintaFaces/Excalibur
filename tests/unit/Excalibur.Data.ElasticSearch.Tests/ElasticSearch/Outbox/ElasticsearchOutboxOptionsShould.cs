// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Outbox;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchOutboxOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchOutboxOptions();
		sut.IndexName.ShouldBe("excalibur-outbox");
		sut.DefaultBatchSize.ShouldBe(100);
		sut.RefreshPolicy.ShouldBe("wait_for");
		sut.SentMessageRetentionDays.ShouldBe(7);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticsearchOutboxOptions
		{
			IndexName = "custom-outbox",
			DefaultBatchSize = 500,
			RefreshPolicy = "false",
			SentMessageRetentionDays = 14,
		};

		sut.IndexName.ShouldBe("custom-outbox");
		sut.DefaultBatchSize.ShouldBe(500);
		sut.RefreshPolicy.ShouldBe("false");
		sut.SentMessageRetentionDays.ShouldBe(14);
	}
}
