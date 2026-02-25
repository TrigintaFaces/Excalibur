// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.KeyManagementService;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Aws;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Categories;

using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Aws;

/// <summary>
/// Unit tests for <see cref="AwsComplianceServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class AwsComplianceServiceCollectionExtensionsShould
{
	#region AddAwsKmsKeyManagement Tests

	[Fact]
	public void AddAwsKmsKeyManagement_RegistersRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Use mock client to avoid real AWS client creation
		var mockClient = A.Fake<IAmazonKeyManagementService>();
		_ = services.AddSingleton(mockClient);

		// Act
		_ = services.AddAwsKmsKeyManagement(options => options.Region = RegionEndpoint.USEast1);

		// Assert
		var provider = services.BuildServiceProvider();
		var kmsProvider = provider.GetService<IKeyManagementProvider>();
		var awsKmsProvider = provider.GetService<AwsKmsProvider>();

		kmsProvider.ShouldNotBeNull();
		awsKmsProvider.ShouldNotBeNull();
		kmsProvider.ShouldBeSameAs(awsKmsProvider);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ConfiguresOptions_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(options =>
		{
			options.Region = RegionEndpoint.USWest2;
			options.UseFipsEndpoint = true;
			options.Environment = "production";
			options.KeyAliasPrefix = "my-app";
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.Region.ShouldBe(RegionEndpoint.USWest2);
		options.UseFipsEndpoint.ShouldBeTrue();
		options.Environment.ShouldBe("production");
		options.KeyAliasPrefix.ShouldBe("my-app");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_UsesDefaultOptions_WhenNoConfigureProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.KeyAliasPrefix.ShouldBe("excalibur-dispatch");
		options.DefaultKeySpec.ShouldBe("SYMMETRIC_DEFAULT");
		options.MetadataCacheDurationSeconds.ShouldBe(300);
		options.EnableAutoRotation.ShouldBeTrue();
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ThrowsArgumentNullException_WhenServicesNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAwsKmsKeyManagement());
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ReturnsSameServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		var result = services.AddAwsKmsKeyManagement();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_DoesNotRegisterDuplicates_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement();
		_ = services.AddAwsKmsKeyManagement();

		// Assert
		var kmsClientCount = services.Count(s => s.ServiceType == typeof(IAmazonKeyManagementService));
		var providerCount = services.Count(s => s.ServiceType == typeof(AwsKmsProvider));

		kmsClientCount.ShouldBe(1);
		providerCount.ShouldBe(1);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_RegistersServicesWithCorrectLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement();

		// Assert - verify singleton lifetime
		var kmsClientDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAmazonKeyManagementService));
		var providerDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(AwsKmsProvider));
		var interfaceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IKeyManagementProvider));

		kmsClientDescriptor.ShouldNotBeNull();
		kmsClientDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		providerDescriptor.ShouldNotBeNull();
		providerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		interfaceDescriptor.ShouldNotBeNull();
		interfaceDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ConfiguresServiceUrl_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		const string customEndpoint = "https://custom-kms.example.com";

		// Act
		_ = services.AddAwsKmsKeyManagement(options =>
		{
			options.ServiceUrl = customEndpoint;
			options.Region = RegionEndpoint.USEast1;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe(customEndpoint);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ConfiguresFipsEndpoint_WhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(options =>
		{
			options.UseFipsEndpoint = true;
			options.Region = RegionEndpoint.USWest2;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.UseFipsEndpoint.ShouldBeTrue();
	}

	#endregion

	#region AddAwsKmsKeyManagement with ClientFactory Tests

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_UsesProvidedFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		var mockClient = A.Fake<IAmazonKeyManagementService>();

		// Act
		_ = services.AddAwsKmsKeyManagement(_ => mockClient);

		// Assert
		var provider = services.BuildServiceProvider();
		var resolvedClient = provider.GetRequiredService<IAmazonKeyManagementService>();

		resolvedClient.ShouldBeSameAs(mockClient);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		var mockClient = A.Fake<IAmazonKeyManagementService>();

		// Act
		_ = services.AddAwsKmsKeyManagement(
			_ => mockClient,
			options => options.Environment = "staging");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.Environment.ShouldBe("staging");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_ThrowsArgumentNullException_WhenServicesNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var mockClient = A.Fake<IAmazonKeyManagementService>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagement(_ => mockClient));
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_ThrowsArgumentNullException_WhenFactoryNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Func<IServiceProvider, IAmazonKeyManagementService>? factory = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagement(factory));
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_RegistersProviders()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		var mockClient = A.Fake<IAmazonKeyManagementService>();

		// Act
		_ = services.AddAwsKmsKeyManagement(_ => mockClient);

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<AwsKmsProvider>().ShouldNotBeNull();
		provider.GetService<IKeyManagementProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithClientFactory_WithNullConfigure_WorksCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		var mockClient = A.Fake<IAmazonKeyManagementService>();

		// Act - pass null for configure
		_ = services.AddAwsKmsKeyManagement(_ => mockClient, null);

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<AwsKmsProvider>().ShouldNotBeNull();
	}

	#endregion

	#region AddAwsKmsKeyManagementLocalStack Tests

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_ConfiguresForLocalStack()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementLocalStack();

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:4566");
		options.Region.ShouldBe(RegionEndpoint.USEast1);
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_UsesCustomEndpoint()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementLocalStack("http://localhost:5566");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:5566");
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_AppliesAdditionalConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementLocalStack(
			"http://localhost:4566",
			options => options.KeyAliasPrefix = "test-prefix");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:4566");
		options.KeyAliasPrefix.ShouldBe("test-prefix");
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_ThrowsArgumentNullException_WhenServicesNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAwsKmsKeyManagementLocalStack());
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_RegistersAllRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementLocalStack();

		// Assert
		var provider = services.BuildServiceProvider();

		provider.GetService<IAmazonKeyManagementService>().ShouldNotBeNull();
		provider.GetService<AwsKmsProvider>().ShouldNotBeNull();
		provider.GetService<IKeyManagementProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_ConfiguresHttpUsage()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act - HTTP URL (not HTTPS)
		_ = services.AddAwsKmsKeyManagementLocalStack("http://localhost:4566");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldStartWith("http://");
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_ConfiguresHttpsUsage()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act - HTTPS URL
		_ = services.AddAwsKmsKeyManagementLocalStack("https://localhost:4566");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldStartWith("https://");
	}

	[Fact]
	public void AddAwsKmsKeyManagementLocalStack_WithNullConfigure_WorksCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementLocalStack("http://localhost:4566", null);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:4566");
	}

	#endregion

	#region AddAwsKmsKeyManagementMultiRegion Tests

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_ConfiguresMultiRegion()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		var replicaRegions = new[] { RegionEndpoint.EUWest1, RegionEndpoint.APNortheast1 };

		// Act
		_ = services.AddAwsKmsKeyManagementMultiRegion(
			RegionEndpoint.USEast1,
			replicaRegions);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.Region.ShouldBe(RegionEndpoint.USEast1);
		options.CreateMultiRegionKeys.ShouldBeTrue();
		options.ReplicaRegions.ShouldContain(RegionEndpoint.EUWest1);
		options.ReplicaRegions.ShouldContain(RegionEndpoint.APNortheast1);
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_AppliesAdditionalConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementMultiRegion(
			RegionEndpoint.USEast1,
			[RegionEndpoint.EUWest1],
			options => options.Environment = "dr-enabled");

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.Environment.ShouldBe("dr-enabled");
		options.CreateMultiRegionKeys.ShouldBeTrue();
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_ThrowsArgumentNullException_WhenServicesNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagementMultiRegion(
				RegionEndpoint.USEast1,
				[RegionEndpoint.EUWest1]));
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_ThrowsArgumentNullException_WhenPrimaryRegionNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagementMultiRegion(
				null!,
				[RegionEndpoint.EUWest1]));
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_ThrowsArgumentNullException_WhenReplicaRegionsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagementMultiRegion(
				RegionEndpoint.USEast1,
				null!));
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_WorksWithEmptyReplicaRegions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementMultiRegion(
			RegionEndpoint.USEast1,
			Array.Empty<RegionEndpoint>());

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.CreateMultiRegionKeys.ShouldBeTrue();
		options.ReplicaRegions.ShouldBeEmpty();
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		var result = services.AddAwsKmsKeyManagementMultiRegion(
			RegionEndpoint.USEast1,
			[RegionEndpoint.EUWest1]);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAwsKmsKeyManagementMultiRegion_WithNullConfigure_WorksCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagementMultiRegion(
			RegionEndpoint.USEast1,
			[RegionEndpoint.EUWest1],
			null);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.CreateMultiRegionKeys.ShouldBeTrue();
		options.ReplicaRegions.ShouldContain(RegionEndpoint.EUWest1);
	}

	#endregion
}
