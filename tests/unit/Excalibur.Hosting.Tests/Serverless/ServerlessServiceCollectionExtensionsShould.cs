// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="ServerlessServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerlessServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddServerlessHosting_RegistersFactoryService()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddServerlessHosting();

		// Assert - Check service is registered without building provider
		services.Any(static sd =>
			sd.ServiceType == typeof(IServerlessHostProviderFactory) &&
			sd.ImplementationType == typeof(ServerlessHostProviderFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddServerlessHosting_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddServerlessHosting());
	}

	[Fact]
	public void AddServerlessHosting_WithOptions_RegistersOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
			options.EnableColdStartOptimization = false;
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<ServerlessHostOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsLambdaHosting_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAwsLambdaHosting();

		// Assert - Check both factory and options configuration are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IServerlessHostProviderFactory)).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<ServerlessHostOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsLambdaHosting_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAwsLambdaHosting());
	}

	[Fact]
	public void AddAzureFunctionsHosting_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddAzureFunctionsHosting();

		// Assert - Check both factory and options configuration are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IServerlessHostProviderFactory)).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<ServerlessHostOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddAzureFunctionsHosting_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAzureFunctionsHosting());
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddGoogleCloudFunctionsHosting();

		// Assert - Check both factory and options configuration are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IServerlessHostProviderFactory)).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<ServerlessHostOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddGoogleCloudFunctionsHosting());
	}

	[Fact]
	public void AddServerlessHosting_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddServerlessHosting();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
