// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Authorization;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbAuthorizationOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify default values and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Authorization")]
public sealed class CosmosDbAuthorizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void AccountEndpoint_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.AccountEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AccountKey_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.AccountKey.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void DatabaseName_DefaultsToAuthorization()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.DatabaseName.ShouldBe("authorization");
	}

	[Fact]
	public void GrantsContainerName_DefaultsToGrants()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.GrantsContainerName.ShouldBe("grants");
	}

	[Fact]
	public void ActivityGroupsContainerName_DefaultsToActivityGroups()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.ActivityGroupsContainerName.ShouldBe("activity-groups");
	}

	[Fact]
	public void ConsistencyLevel_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.ConsistencyLevel.ShouldBeNull();
	}

	[Fact]
	public void MaxRetryAttempts_DefaultsToNine()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(9);
	}

	[Fact]
	public void MaxRetryWaitTimeInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(30);
	}

	[Fact]
	public void RequestTimeoutInSeconds_DefaultsToThirty()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void UseDirectMode_DefaultsToTrue()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.UseDirectMode.ShouldBeTrue();
	}

	[Fact]
	public void PreferredRegions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.PreferredRegions.ShouldBeNull();
	}

	[Fact]
	public void EnableContentResponseOnWrite_DefaultsToFalse()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
	}

	[Fact]
	public void HttpClientFactory_DefaultsToNull()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions();

		// Assert
		options.Client.HttpClientFactory.ShouldBeNull();
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfo()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions();
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions();
		options.Client.AccountEndpoint = "https://test.documents.azure.com:443/";
		options.Client.AccountKey = "xyz";

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenDatabaseNameEmpty()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions
		{
			DatabaseName = ""
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenGrantsContainerNameEmpty()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions
		{
			GrantsContainerName = ""
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenActivityGroupsContainerNameEmpty()
	{
		// Arrange
		var options = new CosmosDbAuthorizationOptions
		{
			ActivityGroupsContainerName = ""
		};
		options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=xyz";

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var options = new CosmosDbAuthorizationOptions
		{
			Client =
			{
				AccountEndpoint = "https://test.documents.azure.com:443/",
				AccountKey = "test-key",
				ConnectionString = "connection-string",
				UseDirectMode = false,
				PreferredRegions = new List<string> { "East US", "West US" },
				HttpClientFactory = () => new HttpClient(),
				Resilience =
				{
					MaxRetryAttempts = 5,
					MaxRetryWaitTimeInSeconds = 60,
					RequestTimeoutInSeconds = 120,
					EnableContentResponseOnWrite = true
				}
			},
			DatabaseName = "my-db",
			GrantsContainerName = "my-grants",
			ActivityGroupsContainerName = "my-groups"
		};

		// Assert
		options.Client.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.Client.AccountKey.ShouldBe("test-key");
		options.Client.ConnectionString.ShouldBe("connection-string");
		options.DatabaseName.ShouldBe("my-db");
		options.GrantsContainerName.ShouldBe("my-grants");
		options.ActivityGroupsContainerName.ShouldBe("my-groups");
		options.Client.Resilience.MaxRetryAttempts.ShouldBe(5);
		options.Client.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(60);
		options.Client.Resilience.RequestTimeoutInSeconds.ShouldBe(120);
		options.Client.UseDirectMode.ShouldBeFalse();
		options.Client.Resilience.EnableContentResponseOnWrite.ShouldBeTrue();
		options.Client.PreferredRegions.Count.ShouldBe(2);
		options.Client.HttpClientFactory.ShouldNotBeNull();
	}

	#endregion
}
