// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddClaimCheck_RegistersIClaimCheckProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IClaimCheckProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddClaimCheck_RegistersNamingStrategy()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IClaimCheckNamingStrategy>().ShouldNotBeNull();
	}

	[Fact]
	public void AddClaimCheck_RegistersOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>();
		using var sp = services.BuildServiceProvider();

		// Assert
		var validators = sp.GetServices<IValidateOptions<ClaimCheckOptions>>();
		validators.ShouldContain(v => v is ClaimCheckOptionsValidator);
	}

	[Fact]
	public void AddClaimCheck_WithConfigure_AppliesOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>(o => o.PayloadThreshold = 2048);
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;
		options.PayloadThreshold.ShouldBe(2048);
	}

	[Fact]
	public void AddClaimCheck_WithNullConfigure_UsesDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>(configureOptions: null);
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;
		options.PayloadThreshold.ShouldBe(256 * 1024); // Default
	}

	[Fact]
	public void AddClaimCheck_WithCleanupEnabled_RegistersHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>(enableCleanup: true);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHostedService));
	}

	[Fact]
	public void AddClaimCheck_WithCleanupDisabled_DoesNotRegisterCleanupHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<InMemoryClaimCheckProvider>(enableCleanup: false);

		// Assert - No hosted services should be registered
		var hostedServiceDescriptors = services.Where(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "ClaimCheckCleanupService").ToList();
		hostedServiceDescriptors.ShouldBeEmpty();
	}

	[Fact]
	public void AddClaimCheck_IsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddClaimCheck<InMemoryClaimCheckProvider>();
		using var sp = services.BuildServiceProvider();

		// Act
		var first = sp.GetRequiredService<IClaimCheckProvider>();
		var second = sp.GetRequiredService<IClaimCheckProvider>();

		// Assert
		first.ShouldBeSameAs(second);
	}
}
