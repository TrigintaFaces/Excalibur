// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.KeyManagementService;

using Excalibur.Compliance;
using Excalibur.Compliance.Aws;

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
[Trait(TraitNames.Component, TestComponents.Compliance)]
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
		_ = services.AddAwsKmsKeyManagement(aws => aws.Region("us-east-1"));

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
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.Region("us-west-2")
			   .UseFipsEndpoint()
			   .Environment("production")
			   .KeyAliasPrefix("my-app");
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

		// Act - builder with no-op configure
		_ = services.AddAwsKmsKeyManagement(_ => { });

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.KeyAliasPrefix.ShouldBe("excalibur-dispatch");
		options.KeyPolicy.DefaultKeySpec.ShouldBe("SYMMETRIC_DEFAULT");
		options.Cache.MetadataCacheDurationSeconds.ShouldBe(300);
		options.KeyPolicy.EnableAutoRotation.ShouldBeTrue();
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ThrowsArgumentNullException_WhenServicesNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagement(_ => { }));
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ThrowsArgumentNullException_WhenConfigureNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsKmsKeyManagement((Action<IComplianceAwsBuilder>)null!));
	}

	[Fact]
	public void AddAwsKmsKeyManagement_ReturnsSameServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		var result = services.AddAwsKmsKeyManagement(_ => { });

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
		_ = services.AddAwsKmsKeyManagement(_ => { });
		_ = services.AddAwsKmsKeyManagement(_ => { });

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
		_ = services.AddAwsKmsKeyManagement(_ => { });

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
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl(customEndpoint)
			   .Region("us-east-1");
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
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.UseFipsEndpoint()
			   .Region("us-west-2");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.UseFipsEndpoint.ShouldBeTrue();
	}

	#endregion

	#region AddAwsKmsKeyManagement Builder Pattern Tests

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresLocalStack()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl("http://localhost:4566")
			   .Region("us-east-1");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:4566");
		options.Region.ShouldBe(RegionEndpoint.USEast1);
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresCustomEndpoint()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl("http://localhost:5566");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldBe("http://localhost:5566");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresKeyAliasPrefix()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.KeyAliasPrefix("test-prefix")
			   .ServiceUrl("http://localhost:4566");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.KeyAliasPrefix.ShouldBe("test-prefix");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_RegistersAllRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl("http://localhost:4566")
			   .Region("us-east-1");
		});

		// Assert
		var provider = services.BuildServiceProvider();

		provider.GetService<IAmazonKeyManagementService>().ShouldNotBeNull();
		provider.GetService<AwsKmsProvider>().ShouldNotBeNull();
		provider.GetService<IKeyManagementProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresHttpEndpoint()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act - HTTP URL (not HTTPS)
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl("http://localhost:4566");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldStartWith("http://");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresHttpsEndpoint()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act - HTTPS URL
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.ServiceUrl("https://localhost:4566");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.ServiceUrl.ShouldStartWith("https://");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_ConfiguresEnvironment()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.Region("us-east-1")
			   .Environment("dr-enabled");
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsKmsOptions>>().Value;

		options.Environment.ShouldBe("dr-enabled");
	}

	[Fact]
	public void AddAwsKmsKeyManagement_WithBuilder_SupportsBindConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
			new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

		// Act
		_ = services.AddAwsKmsKeyManagement(aws =>
		{
			aws.BindConfiguration("Aws:Kms");
		});

		// Assert - should not throw; options are registered
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<AwsKmsOptions>>();
		options.ShouldNotBeNull();
	}

	#endregion
}
