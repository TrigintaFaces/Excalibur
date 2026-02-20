// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Data.Tests.ElasticSearch.Persistence;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchPersistenceOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticsearchPersistenceOptions();
		sut.IndexPrefix.ShouldBe("excalibur-");
		sut.RefreshPolicy.ShouldBe(ElasticsearchRefreshPolicy.WaitFor);
		sut.NumberOfShards.ShouldBe(1);
		sut.NumberOfReplicas.ShouldBe(1);
		sut.MaxResultCount.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticsearchPersistenceOptions
		{
			IndexPrefix = "custom-",
			RefreshPolicy = ElasticsearchRefreshPolicy.Immediate,
			NumberOfShards = 3,
			NumberOfReplicas = 2,
			MaxResultCount = 5000,
		};

		sut.IndexPrefix.ShouldBe("custom-");
		sut.RefreshPolicy.ShouldBe(ElasticsearchRefreshPolicy.Immediate);
		sut.NumberOfShards.ShouldBe(3);
		sut.NumberOfReplicas.ShouldBe(2);
		sut.MaxResultCount.ShouldBe(5000);
	}
}
