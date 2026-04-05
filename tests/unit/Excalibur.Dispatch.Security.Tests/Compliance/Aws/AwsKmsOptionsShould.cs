// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;

using Excalibur.Dispatch.Compliance.Aws;

using Shouldly;

using Tests.Shared.Categories;

using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Aws;

/// <summary>
/// Unit tests for <see cref="AwsKmsOptions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class AwsKmsOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Arrange & Act
		var options = new AwsKmsOptions();

		// Assert
		options.Region.ShouldBeNull();
		options.UseFipsEndpoint.ShouldBeFalse();
		options.KeyAliasPrefix.ShouldBe("excalibur-dispatch");
		options.Environment.ShouldBeNull();
		options.KeyPolicy.DefaultKeySpec.ShouldBe("SYMMETRIC_DEFAULT");
		options.Cache.MetadataCacheDurationSeconds.ShouldBe(300);
		options.Cache.EnableDataKeyCache.ShouldBeTrue();
		options.Cache.DataKeyCacheDurationSeconds.ShouldBe(300);
		options.Cache.DataKeyCacheMaxSize.ShouldBe(1000);
		options.KeyPolicy.DefaultDeletionRetentionDays.ShouldBe(30);
		options.KeyPolicy.EnableAutoRotation.ShouldBeTrue();
		options.ServiceUrl.ShouldBeNull();
		options.KeyPolicy.CreateMultiRegionKeys.ShouldBeFalse();
		options.KeyPolicy.ReplicaRegions.ShouldNotBeNull();
		options.KeyPolicy.ReplicaRegions.ShouldBeEmpty();
	}

	#endregion

	#region BuildKeyAlias Tests

	[Fact]
	public void BuildKeyAlias_WithDefaultPrefix_ReturnsCorrectAlias()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		var alias = options.BuildKeyAlias("my-key");

		// Assert
		alias.ShouldBe("alias/excalibur-dispatch-my-key");
	}

	[Fact]
	public void BuildKeyAlias_WithCustomPrefix_ReturnsCorrectAlias()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "custom-app"
		};

		// Act
		var alias = options.BuildKeyAlias("encryption-key");

		// Assert
		alias.ShouldBe("alias/custom-app-encryption-key");
	}

	[Fact]
	public void BuildKeyAlias_WithEnvironment_IncludesEnvironmentInAlias()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "my-app",
			Environment = "prod"
		};

		// Act
		var alias = options.BuildKeyAlias("master-key");

		// Assert
		alias.ShouldBe("alias/my-app-prod-master-key");
	}

	[Fact]
	public void BuildKeyAlias_WithEmptyEnvironment_ExcludesEnvironment()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "my-app",
			Environment = string.Empty
		};

		// Act
		var alias = options.BuildKeyAlias("data-key");

		// Assert
		alias.ShouldBe("alias/my-app-data-key");
	}

	[Fact]
	public void BuildKeyAlias_WithNullEnvironment_ExcludesEnvironment()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "my-app",
			Environment = null
		};

		// Act
		var alias = options.BuildKeyAlias("data-key");

		// Assert
		alias.ShouldBe("alias/my-app-data-key");
	}

	[Fact]
	public void BuildKeyAlias_WithEmptyKeyId_ReturnsAliasWithEmptyKeyId()
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "my-app"
		};

		// Act
		var alias = options.BuildKeyAlias(string.Empty);

		// Assert
		alias.ShouldBe("alias/my-app-");
	}

	[Theory]
	[InlineData("dev", "dev-key", "alias/test-prefix-dev-dev-key")]
	[InlineData("staging", "api-key", "alias/test-prefix-staging-api-key")]
	[InlineData("production", "master", "alias/test-prefix-production-master")]
	public void BuildKeyAlias_WithVariousEnvironments_ReturnsCorrectAlias(
		string environment,
		string keyId,
		string expected)
	{
		// Arrange
		var options = new AwsKmsOptions
		{
			KeyAliasPrefix = "test-prefix",
			Environment = environment
		};

		// Act
		var alias = options.BuildKeyAlias(keyId);

		// Assert
		alias.ShouldBe(expected);
	}

	#endregion

	#region Property Setters Tests

	[Fact]
	public void AllowSettingRegion()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Region = RegionEndpoint.EUCentral1;

		// Assert
		options.Region.ShouldBe(RegionEndpoint.EUCentral1);
	}

	[Fact]
	public void AllowSettingUseFipsEndpoint()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.UseFipsEndpoint = true;

		// Assert
		options.UseFipsEndpoint.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingKeyAliasPrefix()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.KeyAliasPrefix = "new-prefix";

		// Assert
		options.KeyAliasPrefix.ShouldBe("new-prefix");
	}

	[Fact]
	public void AllowSettingEnvironment()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Environment = "test-env";

		// Assert
		options.Environment.ShouldBe("test-env");
	}

	[Fact]
	public void AllowSettingDefaultKeySpec()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.KeyPolicy.DefaultKeySpec = "RSA_2048";

		// Assert
		options.KeyPolicy.DefaultKeySpec.ShouldBe("RSA_2048");
	}

	[Fact]
	public void AllowSettingMetadataCacheDurationSeconds()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Cache.MetadataCacheDurationSeconds = 600;

		// Assert
		options.Cache.MetadataCacheDurationSeconds.ShouldBe(600);
	}

	[Fact]
	public void AllowSettingEnableDataKeyCache()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Cache.EnableDataKeyCache = false;

		// Assert
		options.Cache.EnableDataKeyCache.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDataKeyCacheDurationSeconds()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Cache.DataKeyCacheDurationSeconds = 120;

		// Assert
		options.Cache.DataKeyCacheDurationSeconds.ShouldBe(120);
	}

	[Fact]
	public void AllowSettingDataKeyCacheMaxSize()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.Cache.DataKeyCacheMaxSize = 500;

		// Assert
		options.Cache.DataKeyCacheMaxSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingDefaultDeletionRetentionDays()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.KeyPolicy.DefaultDeletionRetentionDays = 14;

		// Assert
		options.KeyPolicy.DefaultDeletionRetentionDays.ShouldBe(14);
	}

	[Fact]
	public void AllowSettingEnableAutoRotation()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.KeyPolicy.EnableAutoRotation = false;

		// Assert
		options.KeyPolicy.EnableAutoRotation.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingServiceUrl()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.ServiceUrl = "http://localhost:4566";

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:4566");
	}

	[Fact]
	public void AllowSettingCreateMultiRegionKeys()
	{
		// Arrange
		var options = new AwsKmsOptions();

		// Act
		options.KeyPolicy.CreateMultiRegionKeys = true;

		// Assert
		options.KeyPolicy.CreateMultiRegionKeys.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingReplicaRegions()
	{
		// Arrange
		var options = new AwsKmsOptions();
		var regions = new List<RegionEndpoint>
		{
			RegionEndpoint.EUWest1,
			RegionEndpoint.APSoutheast1
		};

		// Act
		options.KeyPolicy.ReplicaRegions = regions;

		// Assert
		options.KeyPolicy.ReplicaRegions.ShouldBe(regions);
		options.KeyPolicy.ReplicaRegions.Count.ShouldBe(2);
	}

	#endregion

	#region Complex Configuration Scenarios

	[Fact]
	public void SupportFullyConfiguredOptions()
	{
		// Arrange & Act - Use USWest2 with FIPS endpoint as a realistic FIPS-compliant scenario
		var options = new AwsKmsOptions
		{
			Region = RegionEndpoint.USWest2,
			UseFipsEndpoint = true,
			KeyAliasPrefix = "gov-app",
			Environment = "govcloud",
			Cache =
			{
				MetadataCacheDurationSeconds = 600,
				EnableDataKeyCache = true,
				DataKeyCacheDurationSeconds = 180,
				DataKeyCacheMaxSize = 2000
			},
			KeyPolicy =
			{
				DefaultKeySpec = "SYMMETRIC_DEFAULT",
				DefaultDeletionRetentionDays = 7,
				EnableAutoRotation = true,
				CreateMultiRegionKeys = false,
				ReplicaRegions = []
			},
			ServiceUrl = null
		};

		// Assert
		options.Region.ShouldBe(RegionEndpoint.USWest2);
		options.UseFipsEndpoint.ShouldBeTrue();
		options.KeyAliasPrefix.ShouldBe("gov-app");
		options.Environment.ShouldBe("govcloud");
		options.Cache.MetadataCacheDurationSeconds.ShouldBe(600);
		options.Cache.EnableDataKeyCache.ShouldBeTrue();
		options.Cache.DataKeyCacheDurationSeconds.ShouldBe(180);
		options.Cache.DataKeyCacheMaxSize.ShouldBe(2000);
		options.KeyPolicy.DefaultDeletionRetentionDays.ShouldBe(7);
		options.KeyPolicy.EnableAutoRotation.ShouldBeTrue();
		options.ServiceUrl.ShouldBeNull();
		options.KeyPolicy.CreateMultiRegionKeys.ShouldBeFalse();
		options.KeyPolicy.ReplicaRegions.ShouldBeEmpty();

		// Verify alias generation with full config
		var alias = options.BuildKeyAlias("classified-key");
		alias.ShouldBe("alias/gov-app-govcloud-classified-key");
	}

	[Fact]
	public void SupportLocalStackConfiguration()
	{
		// Arrange & Act
		var options = new AwsKmsOptions
		{
			Region = RegionEndpoint.USEast1,
			ServiceUrl = "http://localhost:4566",
			KeyAliasPrefix = "localtest",
			KeyPolicy =
			{
				EnableAutoRotation = false
			}
		};

		// Assert
		options.ServiceUrl.ShouldBe("http://localhost:4566");
		options.Region.ShouldBe(RegionEndpoint.USEast1);
		options.KeyPolicy.EnableAutoRotation.ShouldBeFalse();

		var alias = options.BuildKeyAlias("test-key");
		alias.ShouldBe("alias/localtest-test-key");
	}

	[Fact]
	public void SupportMultiRegionConfiguration()
	{
		// Arrange & Act
		var options = new AwsKmsOptions
		{
			Region = RegionEndpoint.USEast1,
			KeyPolicy =
			{
				CreateMultiRegionKeys = true,
				ReplicaRegions =
				[
					RegionEndpoint.EUWest1,
					RegionEndpoint.APNortheast1,
					RegionEndpoint.SAEast1
				]
			},
			KeyAliasPrefix = "global-app",
			Environment = "production"
		};

		// Assert
		options.KeyPolicy.CreateMultiRegionKeys.ShouldBeTrue();
		options.KeyPolicy.ReplicaRegions.Count.ShouldBe(3);
		options.KeyPolicy.ReplicaRegions.ShouldContain(RegionEndpoint.EUWest1);
		options.KeyPolicy.ReplicaRegions.ShouldContain(RegionEndpoint.APNortheast1);
		options.KeyPolicy.ReplicaRegions.ShouldContain(RegionEndpoint.SAEast1);

		var alias = options.BuildKeyAlias("mrk-key");
		alias.ShouldBe("alias/global-app-production-mrk-key");
	}

	#endregion
}
