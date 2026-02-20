// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Tests.GoogleCloudFunctions;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ExcaliburGoogleCloudFunctionsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterServicesSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburGoogleCloudFunctionsServerless();

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburGoogleCloudFunctionsServerless());
	}

	[Fact]
	public void RegisterWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburGoogleCloudFunctionsServerless(options =>
		{
			options.EnableMetrics = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<ServerlessHostOptions>>();
		resolved.Value.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenServicesIsNullWithOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburGoogleCloudFunctionsServerless(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburGoogleCloudFunctionsServerless(null!));
	}
}
