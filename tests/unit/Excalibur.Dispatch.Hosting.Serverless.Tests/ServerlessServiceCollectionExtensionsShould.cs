// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessServiceCollectionExtensionsShould : UnitTestBase
{
	#region AddServerlessHosting Tests

	[Fact]
	public void AddServerlessHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => ServerlessServiceCollectionExtensions.AddServerlessHosting(null!));
	}

	[Fact]
	public void AddServerlessHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddServerlessHosting();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddServerlessHosting_WithConfigureAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddServerlessHosting(options =>
		{
			options.PreferredPlatform = ServerlessPlatform.AwsLambda;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddServerlessHosting_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddServerlessHosting();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddAwsLambdaHosting Tests

	[Fact]
	public void AddAwsLambdaHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => ServerlessServiceCollectionExtensions.AddAwsLambdaHosting(null!));
	}

	[Fact]
	public void AddAwsLambdaHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsLambdaHosting();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddAwsLambdaHosting_WithConfigureAction_Succeeds()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsLambdaHosting(options =>
		{
			options.Runtime = "dotnet10";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddAwsLambdaHosting_WithConfigureAction_ConfiguresPreferredPlatformAndAwsOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsLambdaHosting(options => options.Runtime = "dotnet10");
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;

		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AwsLambda);
		options.AwsLambda.Runtime.ShouldBe("dotnet10");
	}

	#endregion

	#region AddAzureFunctionsHosting Tests

	[Fact]
	public void AddAzureFunctionsHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => ServerlessServiceCollectionExtensions.AddAzureFunctionsHosting(null!));
	}

	[Fact]
	public void AddAzureFunctionsHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAzureFunctionsHosting();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddAzureFunctionsHosting_WithConfigureAction_Succeeds()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAzureFunctionsHosting(options =>
		{
			options.EnableDurableFunctions = true;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddAzureFunctionsHosting_WithConfigureAction_ConfiguresPreferredPlatformAndAzureOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureFunctionsHosting(options => options.EnableDurableFunctions = true);
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;

		options.PreferredPlatform.ShouldBe(ServerlessPlatform.AzureFunctions);
		options.AzureFunctions.EnableDurableFunctions.ShouldBeTrue();
	}

	#endregion

	#region AddGoogleCloudFunctionsHosting Tests

	[Fact]
	public void AddGoogleCloudFunctionsHosting_WithNullServices_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => ServerlessServiceCollectionExtensions.AddGoogleCloudFunctionsHosting(null!));
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudFunctionsHosting();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_WithConfigureAction_Succeeds()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudFunctionsHosting(options =>
		{
			options.MinInstances = 1;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProviderFactory));
	}

	[Fact]
	public void AddGoogleCloudFunctionsHosting_WithConfigureAction_ConfiguresPreferredPlatformAndGoogleOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddGoogleCloudFunctionsHosting(options => options.MinInstances = 2);
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;

		options.PreferredPlatform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		options.GoogleCloudFunctions.MinInstances.ShouldBe(2);
	}

	#endregion

	#region AddCustomServerlessProvider Tests

	[Fact]
	public void AddCustomServerlessProvider_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		var provider = A.Fake<IServerlessHostProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => ServerlessServiceCollectionExtensions.AddCustomServerlessProvider(null!, provider));
	}

	[Fact]
	public void AddCustomServerlessProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddCustomServerlessProvider(null!));
	}

	[Fact]
	public void AddCustomServerlessProvider_RegistersProviderAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var provider = A.Fake<IServerlessHostProvider>();

		// Act
		services.AddCustomServerlessProvider(provider);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IServerlessHostProvider) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	#endregion
}
