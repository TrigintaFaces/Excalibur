// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.MaterializedViews;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="ElasticSearchMaterializedViewStoreOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 518: Materialized Views provider tests.
/// Tests verify options defaults, property behavior, and validation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Elasticsearch")]
public sealed class ElasticSearchMaterializedViewStoreOptionsShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(ElasticSearchMaterializedViewStoreOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(ElasticSearchMaterializedViewStoreOptions).IsPublic.ShouldBeTrue();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveCorrectDefaultNodeUri()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NodeUri.ShouldBe("http://localhost:9200");
	}

	[Fact]
	public void HaveCorrectDefaultViewsIndexName()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.ViewsIndexName.ShouldBe("materialized-views");
	}

	[Fact]
	public void HaveCorrectDefaultPositionsIndexName()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.PositionsIndexName.ShouldBe("materialized-view-positions");
	}

	[Fact]
	public void HaveDefaultRequestTimeoutOf30Seconds()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.RequestTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultNumberOfShardsOf1()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NumberOfShards.ShouldBe(1);
	}

	[Fact]
	public void HaveDefaultNumberOfReplicasOf0()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.NumberOfReplicas.ShouldBe(0);
	}

	[Fact]
	public void HaveCreateIndexOnInitializeEnabledByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.CreateIndexOnInitialize.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultRefreshIntervalOf1Second()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.RefreshInterval.ShouldBe("1s");
	}

	[Fact]
	public void HaveNullUsernameByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.Username.ShouldBeNull();
	}

	[Fact]
	public void HaveNullPasswordByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.Password.ShouldBeNull();
	}

	[Fact]
	public void HaveNullApiKeyByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.ApiKey.ShouldBeNull();
	}

	[Fact]
	public void HaveDebugModeDisabledByDefault()
	{
		// Arrange & Act
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Assert
		options.EnableDebugMode.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingNodeUri()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();
		var nodeUri = "http://custom:9200";

		// Act
		options.NodeUri = nodeUri;

		// Assert
		options.NodeUri.ShouldBe(nodeUri);
	}

	[Fact]
	public void AllowSettingViewsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();
		var indexName = "custom-views";

		// Act
		options.ViewsIndexName = indexName;

		// Assert
		options.ViewsIndexName.ShouldBe(indexName);
	}

	[Fact]
	public void AllowSettingPositionsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();
		var indexName = "custom-positions";

		// Act
		options.PositionsIndexName = indexName;

		// Assert
		options.PositionsIndexName.ShouldBe(indexName);
	}

	[Fact]
	public void AllowSettingRequestTimeout()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.RequestTimeoutSeconds = 60;

		// Assert
		options.RequestTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingNumberOfShards()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.NumberOfShards = 5;

		// Assert
		options.NumberOfShards.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingNumberOfReplicas()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.NumberOfReplicas = 2;

		// Assert
		options.NumberOfReplicas.ShouldBe(2);
	}

	[Fact]
	public void AllowDisablingCreateIndexOnInitialize()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.CreateIndexOnInitialize = false;

		// Assert
		options.CreateIndexOnInitialize.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRefreshInterval()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.RefreshInterval = "30s";

		// Assert
		options.RefreshInterval.ShouldBe("30s");
	}

	[Fact]
	public void AllowSettingUsername()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.Username = "elastic";

		// Assert
		options.Username.ShouldBe("elastic");
	}

	[Fact]
	public void AllowSettingPassword()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.Password = "secret";

		// Assert
		options.Password.ShouldBe("secret");
	}

	[Fact]
	public void AllowSettingApiKey()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.ApiKey = "api-key-123";

		// Assert
		options.ApiKey.ShouldBe("api-key-123");
	}

	[Fact]
	public void AllowEnablingDebugMode()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions();

		// Act
		options.EnableDebugMode = true;

		// Assert
		options.EnableDebugMode.ShouldBeTrue();
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowOnEmptyNodeUri()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void Validate_ThrowOnWhitespaceNodeUri()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("NodeUri");
	}

	[Fact]
	public void Validate_ThrowOnInvalidNodeUri()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "not-a-valid-uri"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("not a valid URI");
	}

	[Fact]
	public void Validate_ThrowOnEmptyViewsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			ViewsIndexName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ViewsIndexName");
	}

	[Fact]
	public void Validate_ThrowOnWhitespaceViewsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			ViewsIndexName = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ViewsIndexName");
	}

	[Fact]
	public void Validate_ThrowOnEmptyPositionsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			PositionsIndexName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("PositionsIndexName");
	}

	[Fact]
	public void Validate_ThrowOnWhitespacePositionsIndexName()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200",
			PositionsIndexName = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("PositionsIndexName");
	}

	[Fact]
	public void Validate_SucceedWithValidOptions()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "http://localhost:9200"
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_AcceptHttpsNodeUri()
	{
		// Arrange
		var options = new ElasticSearchMaterializedViewStoreOptions
		{
			NodeUri = "https://localhost:9200"
		};

		// Act & Assert - should not throw
		options.Validate();
	}

	#endregion

	#region Constant Tests

	[Fact]
	public void HaveDefaultViewsIndexNameConstant()
	{
		// Assert
		ElasticSearchMaterializedViewStoreOptions.DefaultViewsIndexName.ShouldBe("materialized-views");
	}

	[Fact]
	public void HaveDefaultPositionsIndexNameConstant()
	{
		// Assert
		ElasticSearchMaterializedViewStoreOptions.DefaultPositionsIndexName.ShouldBe("materialized-view-positions");
	}

	#endregion
}
