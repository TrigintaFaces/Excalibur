// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Tests.AwsLambda;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ExcaliburAwsLambdaServiceCollectionExtensionsShould : UnitTestBase
{
#pragma warning disable IL2026
	[Fact]
	public void RegisterServicesSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburAwsLambdaServerless();

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburAwsLambdaServerless());
	}

	[Fact]
	public void RegisterWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburAwsLambdaServerless(options =>
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
			((IServiceCollection)null!).AddExcaliburAwsLambdaServerless(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburAwsLambdaServerless(null!));
	}
#pragma warning restore IL2026
}
