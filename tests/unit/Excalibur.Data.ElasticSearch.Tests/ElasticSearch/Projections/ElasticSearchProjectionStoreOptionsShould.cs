// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticSearchProjectionStoreOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new ElasticSearchProjectionStoreOptions();

		sut.NodeUri.ShouldBe("http://localhost:9200");
		sut.IndexPrefix.ShouldBe("projections");
		sut.RequestTimeoutSeconds.ShouldBe(30);
		sut.NumberOfShards.ShouldBe(1);
		sut.NumberOfReplicas.ShouldBe(0);
		sut.CreateIndexOnInitialize.ShouldBeTrue();
		sut.RefreshInterval.ShouldBe("1s");
		sut.IndexName.ShouldBeNull();
		sut.NodeUris.ShouldBeNull();
		sut.ConnectionPoolType.ShouldBe(ConnectionPoolType.Static);
		sut.Auth.Username.ShouldBeNull();
		sut.Auth.Password.ShouldBeNull();
		sut.Auth.ApiKey.ShouldBeNull();
		sut.EnableDebugMode.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://custom:9200",
			IndexPrefix = "my-projections",
			RequestTimeoutSeconds = 60,
			NumberOfShards = 3,
			NumberOfReplicas = 2,
			CreateIndexOnInitialize = false,
			RefreshInterval = "5s",
			Auth =
			{
				Username = "admin",
				Password = "secret",
				ApiKey = "key123"
			},
			EnableDebugMode = true,
		};

		sut.NodeUri.ShouldBe("http://custom:9200");
		sut.IndexPrefix.ShouldBe("my-projections");
		sut.RequestTimeoutSeconds.ShouldBe(60);
		sut.NumberOfShards.ShouldBe(3);
		sut.NumberOfReplicas.ShouldBe(2);
		sut.CreateIndexOnInitialize.ShouldBeFalse();
		sut.RefreshInterval.ShouldBe("5s");
		sut.Auth.Username.ShouldBe("admin");
		sut.Auth.Password.ShouldBe("secret");
		sut.Auth.ApiKey.ShouldBe("key123");
		sut.EnableDebugMode.ShouldBeTrue();
	}

	[Fact]
	public void ValidateSuccessfullyWithDefaults()
	{
		var sut = new ElasticSearchProjectionStoreOptions();
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void ThrowWhenNodeUriIsNull()
	{
		var sut = new ElasticSearchProjectionStoreOptions { NodeUri = null! };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void ThrowWhenNodeUriIsEmpty()
	{
		var sut = new ElasticSearchProjectionStoreOptions { NodeUri = "" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void ThrowWhenNodeUriIsWhitespace()
	{
		var sut = new ElasticSearchProjectionStoreOptions { NodeUri = "   " };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void ThrowWhenNodeUriIsInvalidUri()
	{
		var sut = new ElasticSearchProjectionStoreOptions { NodeUri = "not-a-uri" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("not a valid URI");
	}

	[Fact]
	public void NotThrowWhenIndexPrefixIsEmpty()
	{
		// IndexPrefix is optional -- empty/whitespace prefix is valid (index name uses just the type name)
		var sut = new ElasticSearchProjectionStoreOptions { IndexPrefix = "" };
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void NotThrowWhenIndexPrefixIsWhitespace()
	{
		// IndexPrefix is optional -- whitespace prefix is valid (index name uses just the type name)
		var sut = new ElasticSearchProjectionStoreOptions { IndexPrefix = "   " };
		Should.NotThrow(() => sut.Validate());
	}

	// --- T.3: IndexName override ---

	[Fact]
	public void AllowSettingIndexName()
	{
		var sut = new ElasticSearchProjectionStoreOptions { IndexName = "custom-index" };
		sut.IndexName.ShouldBe("custom-index");
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void AllowNullIndexName()
	{
		var sut = new ElasticSearchProjectionStoreOptions { IndexName = null };
		sut.IndexName.ShouldBeNull();
		Should.NotThrow(() => sut.Validate());
	}

	// --- T.4: NodeUris + ConnectionPoolType ---

	[Fact]
	public void ValidateSuccessfullyWithMultipleNodeUris()
	{
		var sut = new ElasticSearchProjectionStoreOptions
		{
			NodeUris =
			[
				new Uri("http://node1:9200"),
				new Uri("http://node2:9200"),
				new Uri("http://node3:9200"),
			]
		};
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void ThrowWhenNodeUrisContainsNullUri()
	{
		var sut = new ElasticSearchProjectionStoreOptions
		{
			NodeUris = [new Uri("http://node1:9200"), null!]
		};
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("NodeUris");
	}

	[Fact]
	public void SkipNodeUriValidationWhenNodeUrisIsSet()
	{
		// When NodeUris is set, NodeUri is ignored -- even if NodeUri is invalid
		var sut = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "not-a-uri",
			NodeUris = [new Uri("http://node1:9200")]
		};
		Should.NotThrow(() => sut.Validate());
	}

	[Fact]
	public void FallBackToNodeUriValidationWhenNodeUrisIsEmpty()
	{
		var sut = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "not-a-uri",
			NodeUris = []
		};
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("not a valid URI");
	}

	[Fact]
	public void AllowSettingConnectionPoolType()
	{
		var sut = new ElasticSearchProjectionStoreOptions
		{
			ConnectionPoolType = ConnectionPoolType.Sniffing
		};
		sut.ConnectionPoolType.ShouldBe(ConnectionPoolType.Sniffing);
	}
}
