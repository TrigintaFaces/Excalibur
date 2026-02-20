// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

namespace Excalibur.Hosting.Tests.AzureFunctions;

/// <summary>
/// Unit tests for <see cref="ExcaliburAzureFunctionsServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AzureFunctionsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddExcaliburAzureFunctionsServerless_RegistersHostProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburAzureFunctionsServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var hostProvider = provider.GetService<IServerlessHostProvider>();
		_ = hostProvider.ShouldNotBeNull();
		_ = hostProvider.ShouldBeOfType<AzureFunctionsHostProvider>();
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_RegistersColdStartOptimizer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburAzureFunctionsServerless();

		// Assert
		var provider = services.BuildServiceProvider();
		var optimizer = provider.GetService<IColdStartOptimizer>();
		_ = optimizer.ShouldNotBeNull();
		_ = optimizer.ShouldBeOfType<AzureFunctionsColdStartOptimizer>();
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAzureFunctionsServerless());
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_WithOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		// Act
		_ = services.AddExcaliburAzureFunctionsServerless(options =>
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
	public void AddExcaliburAzureFunctionsServerless_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAzureFunctionsServerless(_ => { }));
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburAzureFunctionsServerless(null!));
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddExcaliburAzureFunctionsServerless();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburAzureFunctionsServerless_IsIdempotent()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcaliburAzureFunctionsServerless();
		_ = services.AddExcaliburAzureFunctionsServerless();

		// Assert - Should not register duplicate services
		var hostProviders = services.Where(s => s.ServiceType == typeof(IServerlessHostProvider)).ToList();
		hostProviders.Count.ShouldBe(1);
	}
}
