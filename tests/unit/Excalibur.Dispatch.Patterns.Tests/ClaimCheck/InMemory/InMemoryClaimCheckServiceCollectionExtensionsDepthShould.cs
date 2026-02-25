// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Depth coverage tests for <see cref="InMemoryClaimCheckServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryClaimCheckServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddInMemoryClaimCheck_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddInMemoryClaimCheck());
	}

	[Fact]
	public void AddInMemoryClaimCheck_RegistersIClaimCheckProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddInMemoryClaimCheck();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IClaimCheckProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryClaimCheck_RegistersConcreteProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddInMemoryClaimCheck();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<InMemoryClaimCheckProvider>().ShouldNotBeNull();
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfigure_AppliesOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddInMemoryClaimCheck(o => o.PayloadThreshold = 512);
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<ClaimCheckOptions>>().Value;
		options.PayloadThreshold.ShouldBe(512);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithCleanupDisabled_DoesNotRegisterHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddInMemoryClaimCheck(enableCleanup: false);

		// Assert - No InMemoryClaimCheckCleanupService should be registered
		var cleanupDescriptors = services.Where(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType != null &&
			sd.ImplementationType.Name == "InMemoryClaimCheckCleanupService").ToList();
		cleanupDescriptors.ShouldBeEmpty();
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithCleanupEnabled_RegistersHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddInMemoryClaimCheck(enableCleanup: true);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService));
	}

	[Fact]
	public void AddInMemoryClaimCheck_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddInMemoryClaimCheck();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddInMemoryClaimCheck_IsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddInMemoryClaimCheck(enableCleanup: false);
		using var sp = services.BuildServiceProvider();

		// Act
		var first = sp.GetRequiredService<IClaimCheckProvider>();
		var second = sp.GetRequiredService<IClaimCheckProvider>();

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfigurationOverload_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		var fakeConfig = A.Fake<IConfiguration>();
		Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryClaimCheck(fakeConfig));
	}

	[Fact]
	public void AddInMemoryClaimCheck_WithConfigurationOverload_ThrowsOnNullConfiguration()
	{
		var services = new ServiceCollection();
		IConfiguration config = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddInMemoryClaimCheck(config));
	}
}
