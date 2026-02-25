// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Patterns.Tests.Azure;

/// <summary>
/// Depth coverage tests for <see cref="AzurePatternsServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AzurePatternsServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddAzureBlobClaimCheck_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureBlobClaimCheck(_ => { }));
	}

	[Fact]
	public void AddAzureBlobClaimCheck_ThrowsArgumentNullException_WhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();
		Action<ClaimCheckOptions> configureOptions = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureBlobClaimCheck(configureOptions));
	}

	[Fact]
	public void AddAzureBlobClaimCheck_RegistersIClaimCheckProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddAzureBlobClaimCheck(o =>
		{
			o.ConnectionString = "UseDevelopmentStorage=true";
			o.ContainerName = "test-container";
		});

		// Assert
		var descriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IClaimCheckProvider));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddAzureBlobClaimCheck_AppliesConfigureOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddAzureBlobClaimCheck(o =>
		{
			o.PayloadThreshold = 4096;
			o.ConnectionString = "UseDevelopmentStorage=true";
		});
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;
		options.PayloadThreshold.ShouldBe(4096);
	}

	[Fact]
	public void AddAzureBlobClaimCheck_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAzureBlobClaimCheck(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAzureBlobClaimCheck_ImplementationType_IsAzureBlobClaimCheckProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAzureBlobClaimCheck(o =>
		{
			o.ConnectionString = "UseDevelopmentStorage=true";
		});

		// Assert
		var descriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IClaimCheckProvider));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(AzureBlobClaimCheckProvider));
	}

	[Fact]
	public void AddAzureBlobClaimCheckWithCleanup_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureBlobClaimCheck(_ => { }, enableCleanup: true));
	}

	[Fact]
	public void AddAzureBlobClaimCheckWithCleanup_ThrowsArgumentNullException_WhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();
		Action<ClaimCheckOptions> configureOptions = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureBlobClaimCheck(configureOptions, enableCleanup: true));
	}

	[Fact]
	public void AddAzureBlobClaimCheckWithCleanup_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddAzureBlobClaimCheck(o =>
		{
			o.ConnectionString = "UseDevelopmentStorage=true";
		}, enableCleanup: false);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAzureBlobClaimCheck_DoesNotOverrideExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingProvider = A.Fake<IClaimCheckProvider>();
		services.AddSingleton(existingProvider);
		services.AddLogging();

		// Act
		services.AddAzureBlobClaimCheck(o =>
		{
			o.ConnectionString = "UseDevelopmentStorage=true";
		});
		using var sp = services.BuildServiceProvider();

		// Assert - TryAddSingleton means the first registration wins
		sp.GetRequiredService<IClaimCheckProvider>().ShouldBeSameAs(existingProvider);
	}
}
