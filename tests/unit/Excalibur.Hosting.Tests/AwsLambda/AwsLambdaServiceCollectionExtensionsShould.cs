// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for <see cref="ExcaliburAwsLambdaServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsLambdaServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddExcaliburAwsLambdaServerless_RegistersHostProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburAwsLambdaServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var hostProvider = provider.GetService<IServerlessHostProvider>();
		_ = hostProvider.ShouldNotBeNull();
		_ = hostProvider.ShouldBeOfType<AwsLambdaHostProvider>();
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_RegistersColdStartOptimizer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburAwsLambdaServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var optimizer = provider.GetService<IColdStartOptimizer>();
		_ = optimizer.ShouldNotBeNull();
		_ = optimizer.ShouldBeOfType<AwsLambdaColdStartOptimizer>();
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAwsLambdaServerless());
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_WithOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburAwsLambdaServerless(options =>
		{
			options.EnableColdStartOptimization = true;
			options.EnableDistributedTracing = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var hostProvider = provider.GetService<IServerlessHostProvider>();
		_ = hostProvider.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAwsLambdaServerless(_ => { }));
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAwsLambdaServerless(null!));
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddExcaliburAwsLambdaServerless();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburAwsLambdaServerless_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburAwsLambdaServerless();
		_ = services.AddExcaliburAwsLambdaServerless();

		// Assert - Should not register duplicate services
		var provider = services.BuildServiceProvider();
		var hostProviders = services.Where(s => s.ServiceType == typeof(IServerlessHostProvider)).ToList();
		hostProviders.Count.ShouldBe(1);
	}
}
