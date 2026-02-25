// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="ServerlessHostProviderFactory" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class ServerlessHostProviderFactoryShould : UnitTestBase
{
	private readonly ServerlessHostProviderFactory _sut;
	private readonly ILogger<ServerlessHostProviderFactory> _logger;

	public ServerlessHostProviderFactoryShould()
	{
		_logger = NullLogger<ServerlessHostProviderFactory>.Instance;
		_sut = new ServerlessHostProviderFactory(_logger);
	}

	[Fact]
	public void Constructor_AcceptsNullProviders()
	{
		// Act
		var factory = new ServerlessHostProviderFactory(_logger, null);

		// Assert
		factory.AvailableProviders.ShouldBeEmpty();
	}

	[Fact]
	public void AvailableProviders_ReturnsEmpty_WhenNoProvidersRegistered()
	{
		// Act
		var result = _sut.AvailableProviders;

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void RegisterProvider_AddsProviderToCollection()
	{
		// Arrange
		var mockProvider = new MockServerlessHostProvider(ServerlessPlatform.AwsLambda, isAvailable: true);

		// Act
		_sut.RegisterProvider(mockProvider);

		// Assert
		_sut.AvailableProviders.ShouldContain(mockProvider);
	}

	[Fact]
	public void RegisterProvider_ThrowsOnNullProvider()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.RegisterProvider(null!));
	}

	[Fact]
	public void GetProvider_ReturnsProvider_WhenRegisteredAndAvailable()
	{
		// Arrange
		var mockProvider = new MockServerlessHostProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		_sut.RegisterProvider(mockProvider);

		// Act
		var result = _sut.GetProvider(ServerlessPlatform.AwsLambda);

		// Assert
		result.ShouldBe(mockProvider);
	}

	[Fact]
	public void GetProvider_ThrowsInvalidOperation_WhenProviderNotAvailable()
	{
		// Arrange
		var mockProvider = new MockServerlessHostProvider(ServerlessPlatform.AwsLambda, isAvailable: false);
		_sut.RegisterProvider(mockProvider);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _sut.GetProvider(ServerlessPlatform.AwsLambda));
	}

	[Fact]
	public void GetProvider_ThrowsArgumentException_WhenProviderNotRegistered()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _sut.GetProvider(ServerlessPlatform.AwsLambda));
	}

	[Fact]
	public void CreateProvider_ReturnsRegisteredProvider_WhenPreferredPlatformSpecified()
	{
		// Arrange
		var mockProvider = new MockServerlessHostProvider(ServerlessPlatform.AwsLambda, isAvailable: true);
		_sut.RegisterProvider(mockProvider);

		// Act
		var result = _sut.CreateProvider(ServerlessPlatform.AwsLambda);

		// Assert
		result.ShouldBe(mockProvider);
	}

	[Fact]
	public void CreateProvider_ThrowsInvalidOperation_WhenNoProvidersAvailable()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => _sut.CreateProvider());
	}

	[Fact]
	public void DetectPlatform_ReturnsUnknown_WhenNoEnvironmentVariablesSet()
	{
		// Arrange - Clear environment
		ClearAllEnvironment();

		// Act
		var result = _sut.DetectPlatform();

		// Assert
		result.ShouldBe(ServerlessPlatform.Unknown);
	}

	[Fact]
	public void DetectPlatform_ReturnsAwsLambda_WhenAwsLambdaEnvironmentSet()
	{
		// Arrange
		ClearAllEnvironment();
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act
			var result = _sut.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AwsLambda);
		}
		finally
		{
			ClearAllEnvironment();
		}
	}

	[Fact]
	public void DetectPlatform_ReturnsAzureFunctions_WhenAzureEnvironmentSet()
	{
		// Arrange
		ClearAllEnvironment();
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");

		try
		{
			// Act
			var result = _sut.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.AzureFunctions);
		}
		finally
		{
			ClearAllEnvironment();
		}
	}

	[Fact]
	public void DetectPlatform_ReturnsGoogleCloudFunctions_WhenGcfEnvironmentSet()
	{
		// Arrange
		ClearAllEnvironment();
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "test-function");

		try
		{
			// Act
			var result = _sut.DetectPlatform();

			// Assert
			result.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		}
		finally
		{
			ClearAllEnvironment();
		}
	}

	private static void ClearAllEnvironment()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("K_SERVICE", null);
	}

	private sealed class MockServerlessHostProvider(ServerlessPlatform platform, bool isAvailable) : IServerlessHostProvider
	{
		public ServerlessPlatform Platform => platform;
		public bool IsAvailable => isAvailable;

		public void ConfigureServices(IServiceCollection services, ServerlessHostOptions options) { }
		public void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options) { }
		public IServerlessContext CreateContext(object platformContext) => throw new NotImplementedException();
		public object? GetService(Type serviceType) => null;

		public Task<TOutput> ExecuteAsync<TInput, TOutput>(
			TInput input,
			IServerlessContext context,
			Func<TInput, IServerlessContext, CancellationToken, Task<TOutput>> handler,
			CancellationToken cancellationToken = default)
			where TInput : class
			where TOutput : class => throw new NotImplementedException();
	}
}
