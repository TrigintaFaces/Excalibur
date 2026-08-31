// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ElasticsearchOutboxOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchOutboxOptions();
		sut.IndexName.ShouldBe("excalibur-outbox");
		sut.DefaultBatchSize.ShouldBe(100);
		sut.RefreshPolicy.ShouldBe("wait_for");

		// The claim lease must default to a usable value: zero or negative would make every claimed
		// message instantly reclaimable by another poller, re-creating duplicate delivery by default.
		sut.LeaseTimeoutSeconds.ShouldBe(300);
		sut.ProcessorId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticsearchOutboxOptions
		{
			IndexName = "custom-outbox",
			DefaultBatchSize = 500,
			RefreshPolicy = "false",
			LeaseTimeoutSeconds = 45,
			ProcessorId = "poller-1",
		};

		sut.IndexName.ShouldBe("custom-outbox");
		sut.DefaultBatchSize.ShouldBe(500);
		sut.RefreshPolicy.ShouldBe("false");
		sut.LeaseTimeoutSeconds.ShouldBe(45);
		sut.ProcessorId.ShouldBe("poller-1");
	}
}
