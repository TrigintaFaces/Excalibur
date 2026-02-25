// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/> DI registration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServiceCollectionExtensionsTests
{
	[Fact]
	public void AddInMemoryClaimCheck_ShouldRegisterProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var provider = serviceProvider.GetService<IClaimCheckProvider>();
		_ = provider.ShouldNotBeNull();
		_ = provider.ShouldBeOfType<InMemoryClaimCheckProvider>();
	}

	[Fact]
	public void AddInMemoryClaimCheck_ShouldRegisterConcreteTypeAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var concrete = serviceProvider.GetService<InMemoryClaimCheckProvider>();
		_ = concrete.ShouldNotBeNull();

		// Verify singleton behavior
		var concrete2 = serviceProvider.GetService<InMemoryClaimCheckProvider>();
		concrete2.ShouldBeSameAs(concrete);
	}

	[Fact]
	public void AddInMemoryClaimCheck_ShouldRegisterInterfaceAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var provider1 = serviceProvider.GetService<IClaimCheckProvider>();
		var provider2 = serviceProvider.GetService<IClaimCheckProvider>();

		_ = provider1.ShouldNotBeNull();
		_ = provider2.ShouldNotBeNull();
		provider2.ShouldBeSameAs(provider1);
	}

	[Fact]
	public void AddInMemoryClaimCheck_InterfaceAndConcrete_ShouldReturnSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck();
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var viaInterface = serviceProvider.GetService<IClaimCheckProvider>();
		var viaConcrete = serviceProvider.GetService<InMemoryClaimCheckProvider>();

		_ = viaInterface.ShouldNotBeNull();
		_ = viaConcrete.ShouldNotBeNull();
		viaInterface.ShouldBeSameAs(viaConcrete);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithEnableCleanupTrue_ShouldRegisterCleanupService()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(); // Cleanup service requires ILogger

		// Act
		_ = services.AddInMemoryClaimCheck(enableCleanup: true);
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var hostedServices = serviceProvider.GetServices<IHostedService>();
		hostedServices.ShouldContain(s => s is InMemoryClaimCheckCleanupService);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithEnableCleanupFalse_ShouldNotRegisterCleanupService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck(enableCleanup: false);
		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var hostedServices = serviceProvider.GetServices<IHostedService>();
		hostedServices.ShouldNotContain(s => s is InMemoryClaimCheckCleanupService);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithOptions_ShouldConfigureOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck(options =>
		{
			options.PayloadThreshold = 128 * 1024; // 128KB
			options.EnableCompression = true;
			options.DefaultTtl = TimeSpan.FromDays(3);
		});

		var serviceProvider = services.BuildServiceProvider();
		var options = serviceProvider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		// Assert
		options.PayloadThreshold.ShouldBe(128 * 1024);
		options.EnableCompression.ShouldBeTrue();
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(3));
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfiguration_ShouldBindOptions()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ClaimCheck:PayloadThreshold"] = "131072", // 128KB
			["ClaimCheck:EnableCompression"] = "true",
			["ClaimCheck:DefaultTtl"] = "3.00:00:00", // 3 days
			["ClaimCheck:CleanupInterval"] = "00:30:00" // 30 minutes
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck(configuration.GetSection("ClaimCheck"));

		var serviceProvider = services.BuildServiceProvider();
		var options = serviceProvider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		// Assert
		options.PayloadThreshold.ShouldBe(131072);
		options.EnableCompression.ShouldBeTrue();
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(3));
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfigurationAndCleanupEnabled_ShouldRegisterBoth()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["ClaimCheck:PayloadThreshold"] = "102400"
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging(); // Cleanup service requires ILogger

		// Act
		_ = services.AddInMemoryClaimCheck(configuration.GetSection("ClaimCheck"), enableCleanup: true);

		var serviceProvider = services.BuildServiceProvider();

		// Assert
		var provider = serviceProvider.GetService<IClaimCheckProvider>();
		_ = provider.ShouldNotBeNull();

		var hostedServices = serviceProvider.GetServices<IHostedService>();
		hostedServices.ShouldContain(s => s is InMemoryClaimCheckCleanupService);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithNullServices_ShouldThrowArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryClaimCheck());
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfiguration_WithNullServices_ShouldThrowArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryClaimCheck(configuration));
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfiguration_WithNullConfiguration_ShouldThrowArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfiguration configuration = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryClaimCheck(configuration));
	}

	[Fact]
	public void AddInMemoryClaimCheck_CalledTwice_ShouldUseTryAddPattern()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Add twice
		_ = services.AddInMemoryClaimCheck(options => options.PayloadThreshold = 1024);
		_ = services.AddInMemoryClaimCheck(options => options.PayloadThreshold = 2048);

		var serviceProvider = services.BuildServiceProvider();

		// Assert - Should not throw, TryAdd prevents duplicates
		var providers = serviceProvider.GetServices<IClaimCheckProvider>().ToList();
		providers.Count.ShouldBe(1); // Only one registration due to TryAdd
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithNullConfigureOptions_ShouldUseDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck(configureOptions: null);

		var serviceProvider = services.BuildServiceProvider();
		var options = serviceProvider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		// Assert - Should use default values
		options.PayloadThreshold.ShouldBe(262144); // Default 256KB
		options.EnableCompression.ShouldBeTrue();
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void AddInMemoryClaimCheck_ShouldAllowOverridingInTests()
	{
		// Arrange
		var services = new ServiceCollection();
		var mockProvider = new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions()));

		// Act - Register custom provider first, then try to add default
		_ = services.AddSingleton(mockProvider);
		_ = services.AddSingleton<IClaimCheckProvider>(mockProvider);
		_ = services.AddInMemoryClaimCheck(); // Should not override due to TryAdd

		var serviceProvider = services.BuildServiceProvider();

		// Assert - Should get the mock provider
		var provider = serviceProvider.GetService<IClaimCheckProvider>();
		provider.ShouldBeSameAs(mockProvider);
	}

	[Fact]
	public void AddInMemoryClaimCheck_MultipleOptions_ShouldApplyAll()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Configure via action
		_ = services.AddInMemoryClaimCheck(options =>
		{
			options.PayloadThreshold = 1024;
			options.EnableCompression = true;
		});

		// Further configure via Options.Configure
		_ = services.Configure<ClaimCheckOptions>(options =>
		{
			options.DefaultTtl = TimeSpan.FromDays(1);
		});

		var serviceProvider = services.BuildServiceProvider();
		var options = serviceProvider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		// Assert - Both configurations applied
		options.PayloadThreshold.ShouldBe(1024);
		options.EnableCompression.ShouldBeTrue();
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(1));
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithEmptyConfiguration_ShouldUseDefaults()
	{
		// Arrange
		var configuration = new ConfigurationBuilder().Build();
		var services = new ServiceCollection();

		// Act
		_ = services.AddInMemoryClaimCheck(configuration.GetSection("NonExistent"));

		var serviceProvider = services.BuildServiceProvider();
		var options = serviceProvider.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;

		// Assert - Should use default values
		options.PayloadThreshold.ShouldBe(262144);
		options.DefaultTtl.ShouldBe(TimeSpan.FromDays(7));
	}
}
