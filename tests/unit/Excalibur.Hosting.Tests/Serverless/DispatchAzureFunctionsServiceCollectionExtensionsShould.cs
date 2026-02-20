// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Tests for Dispatch AzureFunctionsServiceCollectionExtensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DispatchAzureFunctionsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterAzureFunctionsServerlessServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAzureFunctionsServerless();

		// Assert
		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddAzureFunctionsServerless());
	}

	[Fact]
	public void RegisterWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAzureFunctionsServerless(options =>
		{
			options.EnableColdStartOptimization = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<ServerlessHostOptions>>();
		resolved.Value.EnableColdStartOptimization.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenServicesIsNullWithOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddAzureFunctionsServerless(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddAzureFunctionsServerless(null!));
	}
}
