// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

namespace Excalibur.Hosting.Tests.GoogleCloudFunctions;

/// <summary>
/// Unit tests for <see cref="ExcaliburGoogleCloudFunctionsServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GoogleCloudFunctionsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_RegistersHostProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburGoogleCloudFunctionsServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var hostProvider = provider.GetService<IServerlessHostProvider>();
		_ = hostProvider.ShouldNotBeNull();
		_ = hostProvider.ShouldBeOfType<GoogleCloudFunctionsHostProvider>();
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_RegistersColdStartOptimizer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburGoogleCloudFunctionsServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var optimizer = provider.GetService<IColdStartOptimizer>();
		_ = optimizer.ShouldNotBeNull();
		_ = optimizer.ShouldBeOfType<GoogleCloudFunctionsColdStartOptimizer>();
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburGoogleCloudFunctionsServerless());
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_WithOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburGoogleCloudFunctionsServerless(options =>
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
	public void AddExcaliburGoogleCloudFunctionsServerless_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburGoogleCloudFunctionsServerless(_ => { }));
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburGoogleCloudFunctionsServerless(null!));
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddExcaliburGoogleCloudFunctionsServerless();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburGoogleCloudFunctionsServerless_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburGoogleCloudFunctionsServerless();
		_ = services.AddExcaliburGoogleCloudFunctionsServerless();

		// Assert - Should not register duplicate services
		var hostProviders = services.Where(s => s.ServiceType == typeof(IServerlessHostProvider)).ToList();
		hostProviders.Count.ShouldBe(1);
	}
}
