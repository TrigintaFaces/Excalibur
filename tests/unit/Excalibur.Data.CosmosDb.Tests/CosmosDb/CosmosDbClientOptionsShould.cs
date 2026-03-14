// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="CosmosDbClientOptions"/> -- the extracted sub-options class
/// shared across all CosmosDb store options (Sprint 630 A.2).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Options")]
public sealed class CosmosDbClientOptionsShould
{
	#region Default Values

	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new CosmosDbClientOptions();

		// Assert
		options.AccountEndpoint.ShouldBeNull();
		options.AccountKey.ShouldBeNull();
		options.ConnectionString.ShouldBeNull();
		options.ConsistencyLevel.ShouldBeNull();
		options.UseDirectMode.ShouldBeTrue();
		options.PreferredRegions.ShouldBeNull();
		options.Resilience.MaxRetryAttempts.ShouldBe(9);
		options.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(30);
		options.Resilience.RequestTimeoutInSeconds.ShouldBe(30);
		options.Resilience.EnableContentResponseOnWrite.ShouldBeFalse();
		options.HttpClientFactory.ShouldBeNull();
		options.ApplicationName.ShouldBeNull();
	}

	#endregion

	#region Validation

	[Fact]
	public void Validate_Succeeds_WithConnectionString()
	{
		// Arrange
		var options = new CosmosDbClientOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA=="
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WithEndpointAndKey()
	{
		// Arrange
		var options = new CosmosDbClientOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "dGVzdA=="
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenNoConnectionInfoProvided()
	{
		// Arrange
		var options = new CosmosDbClientOptions();

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_Throws_WhenOnlyEndpointProvided()
	{
		// Arrange
		var options = new CosmosDbClientOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/"
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Throws_WhenOnlyKeyProvided()
	{
		// Arrange
		var options = new CosmosDbClientOptions
		{
			AccountKey = "dGVzdA=="
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	#endregion

	#region DataAnnotations

	[Fact]
	public void DataAnnotations_MaxRetryAttempts_ValidWhenZero()
	{
		// Arrange -- [Range(0, int.MaxValue)]
		var resilience = new CosmosDbClientResilienceOptions { MaxRetryAttempts = 0 };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(
			resilience.MaxRetryAttempts,
			new ValidationContext(resilience) { MemberName = nameof(CosmosDbClientResilienceOptions.MaxRetryAttempts) },
			results);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void DataAnnotations_MaxRetryAttempts_InvalidWhenNegative()
	{
		// Arrange
		var resilience = new CosmosDbClientResilienceOptions { MaxRetryAttempts = -1 };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(
			resilience.MaxRetryAttempts,
			new ValidationContext(resilience) { MemberName = nameof(CosmosDbClientResilienceOptions.MaxRetryAttempts) },
			results);

		// Assert
		isValid.ShouldBeFalse();
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public void DataAnnotations_MaxRetryWaitTimeInSeconds_InvalidWhenZero()
	{
		// Arrange -- [Range(1, int.MaxValue)]
		var resilience = new CosmosDbClientResilienceOptions { MaxRetryWaitTimeInSeconds = 0 };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(
			resilience.MaxRetryWaitTimeInSeconds,
			new ValidationContext(resilience) { MemberName = nameof(CosmosDbClientResilienceOptions.MaxRetryWaitTimeInSeconds) },
			results);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void DataAnnotations_RequestTimeoutInSeconds_InvalidWhenZero()
	{
		// Arrange -- [Range(1, int.MaxValue)]
		var resilience = new CosmosDbClientResilienceOptions { RequestTimeoutInSeconds = 0 };
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateProperty(
			resilience.RequestTimeoutInSeconds,
			new ValidationContext(resilience) { MemberName = nameof(CosmosDbClientResilienceOptions.RequestTimeoutInSeconds) },
			results);

		// Assert
		isValid.ShouldBeFalse();
	}

	#endregion

	#region Property Assignment

	[Fact]
	public void AllProperties_CanBeSet()
	{
		// Act
		var options = new CosmosDbClientOptions
		{
			AccountEndpoint = "https://test.documents.azure.com:443/",
			AccountKey = "testKey==",
			ConnectionString = "connstr",
			ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.BoundedStaleness,
			UseDirectMode = false,
			PreferredRegions = ["East US", "West US"],
			Resilience =
			{
				MaxRetryAttempts = 3,
				MaxRetryWaitTimeInSeconds = 10,
				RequestTimeoutInSeconds = 60,
				EnableContentResponseOnWrite = true
			},
			HttpClientFactory = () => new HttpClient(),
			ApplicationName = "MyApp"
		};

		// Assert
		options.AccountEndpoint.ShouldBe("https://test.documents.azure.com:443/");
		options.AccountKey.ShouldBe("testKey==");
		options.ConnectionString.ShouldBe("connstr");
		options.ConsistencyLevel.ShouldBe(Microsoft.Azure.Cosmos.ConsistencyLevel.BoundedStaleness);
		options.UseDirectMode.ShouldBeFalse();
		options.PreferredRegions.ShouldNotBeNull();
		options.PreferredRegions!.Count.ShouldBe(2);
		options.Resilience.MaxRetryAttempts.ShouldBe(3);
		options.Resilience.MaxRetryWaitTimeInSeconds.ShouldBe(10);
		options.Resilience.RequestTimeoutInSeconds.ShouldBe(60);
		options.Resilience.EnableContentResponseOnWrite.ShouldBeTrue();
		options.HttpClientFactory.ShouldNotBeNull();
		options.ApplicationName.ShouldBe("MyApp");
	}

	#endregion
}
