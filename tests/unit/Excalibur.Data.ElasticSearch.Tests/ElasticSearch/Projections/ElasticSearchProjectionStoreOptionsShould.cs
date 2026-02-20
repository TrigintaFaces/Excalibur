// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
		sut.Username.ShouldBeNull();
		sut.Password.ShouldBeNull();
		sut.ApiKey.ShouldBeNull();
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
			Username = "admin",
			Password = "secret",
			ApiKey = "key123",
			EnableDebugMode = true,
		};

		sut.NodeUri.ShouldBe("http://custom:9200");
		sut.IndexPrefix.ShouldBe("my-projections");
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
	public void ThrowWhenIndexPrefixIsEmpty()
	{
		var sut = new ElasticSearchProjectionStoreOptions { IndexPrefix = "" };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("IndexPrefix");
	}

	[Fact]
	public void ThrowWhenIndexPrefixIsWhitespace()
	{
		var sut = new ElasticSearchProjectionStoreOptions { IndexPrefix = "   " };
		Should.Throw<InvalidOperationException>(() => sut.Validate())
			.Message.ShouldContain("IndexPrefix");
	}
}
