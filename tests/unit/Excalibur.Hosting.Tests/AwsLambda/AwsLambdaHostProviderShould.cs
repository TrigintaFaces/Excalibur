// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for <see cref="AwsLambdaHostProvider" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class AwsLambdaHostProviderShould : UnitTestBase
{
	private readonly AwsLambdaHostProvider _sut;
	private readonly ILogger<AwsLambdaHostProvider> _logger;

	public AwsLambdaHostProviderShould()
	{
		_logger = NullLogger<AwsLambdaHostProvider>.Instance;
		_sut = new AwsLambdaHostProvider(_logger);
	}

	[Fact]
	public void Platform_ReturnsAwsLambda()
	{
		// Act
		var result = _sut.Platform;

		// Assert
		result.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void IsAvailable_ReturnsFalse_WhenNotInLambdaEnvironment()
	{
		// Arrange - Clean environment
		ClearAwsEnvironment();

		// Act
		var result = _sut.IsAvailable;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsAvailable_ReturnsTrue_WhenAwsLambdaFunctionNameSet()
	{
		// Arrange
		ClearAwsEnvironment();
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act
			var result = _sut.IsAvailable;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			ClearAwsEnvironment();
		}
	}

	[Fact]
	public void IsAvailable_ReturnsTrue_WhenAwsExecutionEnvSet()
	{
		// Arrange
		ClearAwsEnvironment();
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_Lambda_dotnet6");

		try
		{
			// Act
			var result = _sut.IsAvailable;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			ClearAwsEnvironment();
		}
	}

	[Fact]
	public void IsAvailable_ReturnsTrue_WhenLambdaTaskRootSet()
	{
		// Arrange
		ClearAwsEnvironment();
		Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", "/var/task");

		try
		{
			// Act
			var result = _sut.IsAvailable;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			ClearAwsEnvironment();
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
	public void ConfigureServices_RegistersExpectedServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ServerlessHostOptions();

		// Act
		_sut.ConfigureServices(services, options);

		// Assert
		services.ShouldNotBeEmpty();
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
	public void CreateContext_ReturnsAwsLambdaServerlessContext()
	{
		// Arrange
		var lambdaContext = new TestLambdaContext
		{
			AwsRequestId = "test-request-id",
			FunctionName = "test-function",
			FunctionVersion = "1.0.0",
			InvokedFunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:test-function",
			MemoryLimitInMB = 128,
			RemainingTime = TimeSpan.FromMinutes(5)
		};

		// Act
		using var context = _sut.CreateContext(lambdaContext);

		// Assert
		_ = context.ShouldBeOfType<AwsLambdaServerlessContext>();
		context.Platform.ShouldBe(ServerlessPlatform.AwsLambda);
		context.RequestId.ShouldBe("test-request-id");
	}

	[Fact]
	public void CreateContext_ThrowsForInvalidContext()
	{
		// Arrange
		var invalidContext = new object();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _sut.CreateContext(invalidContext));
	}

	private static void ClearAwsEnvironment()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
		Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", null);
		Environment.SetEnvironmentVariable("AWS_REGION", null);
		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);
	}
}
