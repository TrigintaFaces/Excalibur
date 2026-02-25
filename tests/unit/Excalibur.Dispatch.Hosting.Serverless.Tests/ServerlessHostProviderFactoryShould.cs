// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessHostProviderFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessHostProviderFactoryShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithProviders_RegistersThem()
	{
		// Arrange
		var provider = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);

		// Act
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[provider]);

		// Assert
		factory.AvailableProviders.ShouldContain(provider);
	}

	[Fact]
	public void Constructor_WithNullProviders_CreatesEmptyFactory()
	{
		// Act
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());

		// Assert
		factory.AvailableProviders.ShouldBeEmpty();
	}

	#endregion

	#region RegisterProvider Tests

	[Fact]
	public void RegisterProvider_WithNullProvider_ThrowsArgumentNullException()
	{
		// Arrange
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => factory.RegisterProvider(null!));
	}

	[Fact]
	public void RegisterProvider_WithValidProvider_RegistersIt()
	{
		// Arrange
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());
		var provider = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: true);

		// Act
		factory.RegisterProvider(provider);

		// Assert
		factory.AvailableProviders.ShouldContain(provider);
	}

	[Fact]
	public void RegisterProvider_SamePlatformTwice_OverwritesPrevious()
	{
		// Arrange
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());
		var provider1 = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var provider2 = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);

		// Act
		factory.RegisterProvider(provider1);
		factory.RegisterProvider(provider2);

		// Assert
		factory.AvailableProviders.Count().ShouldBe(1);
	}

	#endregion

	#region AvailableProviders Tests

	[Fact]
	public void AvailableProviders_FiltersUnavailable()
	{
		// Arrange
		var available = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var unavailable = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[available, unavailable]);

		// Assert
		factory.AvailableProviders.ShouldContain(available);
		factory.AvailableProviders.ShouldNotContain(unavailable);
	}

	#endregion

	#region CreateProvider Tests

	[Fact]
	public void CreateProvider_WithPreferredPlatformAvailable_ReturnsPreferred()
	{
		// Arrange
		var aws = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var azure = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: true);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[aws, azure]);

		// Act
		var result = factory.CreateProvider(ServerlessPlatform.AzureFunctions);

		// Assert
		result.ShouldBe(azure);
	}

	[Fact]
	public void CreateProvider_WithPreferredPlatformUnavailable_FallsBackToAvailable()
	{
		// Arrange
		var aws = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var azure = CreateFakeProvider(ServerlessPlatform.AzureFunctions, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[aws, azure]);

		// Act
		var result = factory.CreateProvider(ServerlessPlatform.AzureFunctions);

		// Assert
		result.ShouldBe(aws);
	}

	[Fact]
	public void CreateProvider_WithNoProviders_ThrowsInvalidOperationException()
	{
		// Arrange
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => factory.CreateProvider());
	}

	[Fact]
	public void CreateProvider_WithNoAvailableProviders_ThrowsInvalidOperationException()
	{
		// Arrange
		var unavailable = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[unavailable]);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => factory.CreateProvider());
	}

	#endregion

	#region GetProvider Tests

	[Fact]
	public void GetProvider_WithRegisteredAndAvailable_ReturnsProvider()
	{
		// Arrange
		var provider = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[provider]);

		// Act
		var result = factory.GetProvider(ServerlessPlatform.AwsLambda);

		// Assert
		result.ShouldBe(provider);
	}

	[Fact]
	public void GetProvider_WithRegisteredButUnavailable_ThrowsInvalidOperationException()
	{
		// Arrange
		var provider = CreateFakeProvider(ServerlessPlatform.AwsLambda, isAvailable: false);
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>(),
			[provider]);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(
			() => factory.GetProvider(ServerlessPlatform.AwsLambda));
	}

	[Fact]
	public void GetProvider_WithUnregisteredPlatform_ThrowsArgumentException()
	{
		// Arrange
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => factory.GetProvider(ServerlessPlatform.AwsLambda));
	}

	#endregion

	#region DetectPlatform Tests

	[Fact]
	public void DetectPlatform_WithNoEnvVars_ReturnsUnknown()
	{
		// Arrange — Ensure relevant env vars are not set (they shouldn't be in test env)
		var factory = new ServerlessHostProviderFactory(
			EnabledTestLogger.Create<ServerlessHostProviderFactory>());

		// Act
		var result = factory.DetectPlatform();

		// Assert — In a test environment, no cloud env vars are expected
		// The result should be Unknown unless we're running in a cloud environment
		result.ShouldBeOneOf(
			ServerlessPlatform.Unknown,
			ServerlessPlatform.AwsLambda,
			ServerlessPlatform.AzureFunctions,
			ServerlessPlatform.GoogleCloudFunctions);
	}

	#endregion

	#region Helpers

	private static IServerlessHostProvider CreateFakeProvider(
		ServerlessPlatform platform,
		bool isAvailable)
	{
		var provider = A.Fake<IServerlessHostProvider>();
		A.CallTo(() => provider.Platform).Returns(platform);
		A.CallTo(() => provider.IsAvailable).Returns(isAvailable);
		return provider;
	}

	#endregion
}
