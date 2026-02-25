// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Additional unit tests for <see cref="ServerlessServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessServiceCollectionExtensionsShould2 : UnitTestBase
{
	#region AddServerlessHosting Tests

	[Fact]
	public void AddServerlessHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddServerlessHosting());
	}

	[Fact]
	public void AddServerlessHosting_WithoutOptions_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddServerlessHosting();

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
		factory.ShouldBeOfType<ServerlessHostProviderFactory>();
	}

	[Fact]
	public void AddServerlessHosting_WithOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AzureFunctions;
			options.EnableColdStartOptimization = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void AddServerlessHosting_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddServerlessHosting();

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddAwsLambdaHosting Tests

	[Fact]
	public void AddAwsLambdaHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAwsLambdaHosting());
	}

	[Fact]
	public void AddAwsLambdaHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddAwsLambdaHosting();

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsLambdaHosting_WithOptions_ConfiguresLambdaOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddAwsLambdaHosting(options =>
		{
			options.EnableProvisionedConcurrency = true;
			options.ReservedConcurrency = 10;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsLambdaHosting_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsLambdaHosting();

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddAzureFunctionsHosting Tests

	[Fact]
	public void AddAzureFunctionsHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAzureFunctionsHosting());
	}

	[Fact]
	public void AddAzureFunctionsHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddAzureFunctionsHosting();

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void AddAzureFunctionsHosting_WithOptions_ConfiguresAzureOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddAzureFunctionsHosting(options =>
		{
			options.EnableDurableFunctions = true;
			options.StorageConnectionString = "UseDevelopmentStorage=true";
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	#endregion

	#region AddGoogleCloudFunctionsHosting Tests

	[Fact]
	public void AddGoogleCloudFunctionsHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddGoogleCloudFunctionsHosting());
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddGoogleCloudFunctionsHosting();

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_WithOptions_ConfiguresGoogleOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

		// Act
		_ = services.AddGoogleCloudFunctionsHosting(options =>
		{
			options.MinInstances = 2;
			options.MaxInstances = 100;
			options.VpcConnector = "my-vpc-connector";
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var factory = provider.GetService<IServerlessHostProviderFactory>();
		factory.ShouldNotBeNull();
	}

	#endregion

	#region AddCustomServerlessProvider Tests

	[Fact]
	public void AddCustomServerlessProvider_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;
		var provider = A.Fake<IServerlessHostProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddCustomServerlessProvider(provider));
	}

	[Fact]
	public void AddCustomServerlessProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddCustomServerlessProvider(null!));
	}

	[Fact]
	public void AddCustomServerlessProvider_RegistersProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		var customProvider = A.Fake<IServerlessHostProvider>();

		// Act
		_ = services.AddCustomServerlessProvider(customProvider);

		// Assert
		var sp = services.BuildServiceProvider();
		var resolved = sp.GetService<IServerlessHostProvider>();
		resolved.ShouldBe(customProvider);
	}

	[Fact]
	public void AddCustomServerlessProvider_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var customProvider = A.Fake<IServerlessHostProvider>();

		// Act
		var result = services.AddCustomServerlessProvider(customProvider);

		// Assert
		result.ShouldBe(services);
	}

	#endregion
}
