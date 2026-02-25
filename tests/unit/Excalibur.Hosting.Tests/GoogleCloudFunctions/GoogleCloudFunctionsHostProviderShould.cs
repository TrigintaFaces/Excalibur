// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

namespace Excalibur.Hosting.Tests.GoogleCloudFunctions;

/// <summary>
/// Unit tests for <see cref="GoogleCloudFunctionsHostProvider" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class GoogleCloudFunctionsHostProviderShould : UnitTestBase
{
	private readonly GoogleCloudFunctionsHostProvider _sut;
	private readonly ILogger<GoogleCloudFunctionsHostProvider> _logger;

	public GoogleCloudFunctionsHostProviderShould()
	{
		_logger = NullLogger<GoogleCloudFunctionsHostProvider>.Instance;
		_sut = new GoogleCloudFunctionsHostProvider(_logger);
	}

	[Fact]
	public void Platform_ReturnsGoogleCloudFunctions()
	{
		// Act
		var result = _sut.Platform;

		// Assert
		result.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
	}

	[Fact]
	public void IsAvailable_ReturnsFalse_WhenNotInGcfEnvironment()
	{
		// Arrange - Clean environment
		ClearGcfEnvironment();

		// Act
		var result = _sut.IsAvailable;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsAvailable_ReturnsTrue_WhenFunctionNameSet()
	{
		// Arrange
		ClearGcfEnvironment();
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "test-function");

		try
		{
			// Act
			var result = _sut.IsAvailable;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			ClearGcfEnvironment();
		}
	}

	[Fact]
	public void IsAvailable_ReturnsTrue_WhenKServiceSet()
	{
		// Arrange
		ClearGcfEnvironment();
		Environment.SetEnvironmentVariable("K_SERVICE", "test-service");

		try
		{
			// Act
			var result = _sut.IsAvailable;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			ClearGcfEnvironment();
		}
	}

	[Fact]
	public void ConfigureServices_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ServerlessHostOptions();

		// Act & Assert
		Should.NotThrow(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureServices_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = new ServerlessHostOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureServices_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		ServerlessHostOptions options = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(services, options));
	}

	[Fact]
	public void ConfigureHost_DoesNotThrow()
	{
		// Arrange
		var hostBuilder = Host.CreateDefaultBuilder();
		var options = new ServerlessHostOptions();

		// Act & Assert
		Should.NotThrow(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void ConfigureHost_ThrowsOnNullHostBuilder()
	{
		// Arrange
		IHostBuilder hostBuilder = null!;
		var options = new ServerlessHostOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void ConfigureHost_ThrowsOnNullOptions()
	{
		// Arrange
		var hostBuilder = Host.CreateDefaultBuilder();
		ServerlessHostOptions options = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(hostBuilder, options));
	}

	[Fact]
	public void CreateContext_ReturnsContext_ForAnyObject()
	{
		// Arrange - Google Cloud Functions accepts any object as platform context
		// since it uses environment variables rather than a strongly-typed context
		var context = new { RequestId = "test-request" };

		// Act
		using var result = _sut.CreateContext(context);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeOfType<GoogleCloudFunctionsServerlessContext>();
		result.Platform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
	}

	[Fact]
	public void CreateContext_ThrowsOnNullContext()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.CreateContext(null!));
	}

	private static void ClearGcfEnvironment()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("K_SERVICE", null);
		Environment.SetEnvironmentVariable("GCLOUD_PROJECT", null);
		Environment.SetEnvironmentVariable("GCP_PROJECT", null);
	}
}
