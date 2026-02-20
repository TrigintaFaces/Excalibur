// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.MaterializedViews;

namespace Excalibur.Data.Tests.ElasticSearch.MaterializedViews;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticSearchMaterializedViewStoreOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions();
		sut.NodeUri.ShouldBe("http://localhost:9200");
		sut.ViewsIndexName.ShouldBe(ElasticSearchMaterializedViewStoreOptions.DefaultViewsIndexName);
		sut.PositionsIndexName.ShouldBe(ElasticSearchMaterializedViewStoreOptions.DefaultPositionsIndexName);
		sut.RequestTimeoutSeconds.ShouldBe(30);
		sut.NumberOfShards.ShouldBe(1);
		sut.NumberOfReplicas.ShouldBe(0);
		sut.CreateIndexOnInitialize.ShouldBeTrue();
		sut.RefreshInterval.ShouldBe("1s");
		sut.Username.ShouldBeNull();
		sut.Password.ShouldBeNull();
		sut.ApiKey.ShouldBeNull();
		sut.EnableDebugMode.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectDefaultConstants()
	{
		ElasticSearchMaterializedViewStoreOptions.DefaultViewsIndexName.ShouldBe("materialized-views");
		ElasticSearchMaterializedViewStoreOptions.DefaultPositionsIndexName.ShouldBe("materialized-view-positions");
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://custom:9200",
			ViewsIndexName = "custom-views",
			PositionsIndexName = "custom-positions",
			RequestTimeoutSeconds = 60,
			NumberOfShards = 3,
			NumberOfReplicas = 2,
			CreateIndexOnInitialize = false,
			RefreshInterval = "5s",
			Username = "admin",
			Password = "secret",
			ApiKey = "key123",
			EnableDebugMode = true,
		};

		sut.NodeUri.ShouldBe("http://custom:9200");
		sut.ViewsIndexName.ShouldBe("custom-views");
		sut.PositionsIndexName.ShouldBe("custom-positions");
		sut.RequestTimeoutSeconds.ShouldBe(60);
		sut.NumberOfShards.ShouldBe(3);
		sut.NumberOfReplicas.ShouldBe(2);
		sut.CreateIndexOnInitialize.ShouldBeFalse();
		sut.RefreshInterval.ShouldBe("5s");
		sut.Username.ShouldBe("admin");
		sut.Password.ShouldBe("secret");
		sut.ApiKey.ShouldBe("key123");
		sut.EnableDebugMode.ShouldBeTrue();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions();
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void ThrowWhenNodeUriIsNull()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions { NodeUri = null! };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void ThrowWhenNodeUriIsEmpty()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions { NodeUri = "" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void ThrowWhenNodeUriIsInvalidUri()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions { NodeUri = "not-a-uri" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("not a valid URI");
	}

	[Fact]
	public void ThrowWhenViewsIndexNameIsEmpty()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions { ViewsIndexName = "" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("ViewsIndexName");
	}

	[Fact]
	public void ThrowWhenPositionsIndexNameIsEmpty()
	{
		var sut = new ElasticSearchMaterializedViewStoreOptions { PositionsIndexName = "" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("PositionsIndexName");
	}
}
