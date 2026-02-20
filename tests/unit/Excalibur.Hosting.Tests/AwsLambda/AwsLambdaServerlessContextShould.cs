// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for <see cref="AwsLambdaServerlessContext" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AwsLambdaServerlessContextShould : UnitTestBase
{
	private readonly TestLambdaContext _lambdaContext;
	private readonly ILogger _logger;

	public AwsLambdaServerlessContextShould()
	{
		_logger = NullLogger.Instance;
		_lambdaContext = new TestLambdaContext
		{
			AwsRequestId = "test-request-id-123",
			FunctionName = "test-function",
			FunctionVersion = "1.0.0",
			InvokedFunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:test-function",
			MemoryLimitInMB = 256,
			LogGroupName = "/aws/lambda/test-function",
			LogStreamName = "2025/01/01/[$LATEST]abcdef123456",
			RemainingTime = TimeSpan.FromSeconds(30)
		};
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenContextIsNotILambdaContext()
	{
		// Arrange
		var invalidContext = new object();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => new AwsLambdaServerlessContext(invalidContext, _logger));
		exception.Message.ShouldContain("ILambdaContext");
	}

	[Fact]
	public void RequestId_ReturnsAwsRequestId()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.RequestId;

		// Assert
		result.ShouldBe("test-request-id-123");
	}

	[Fact]
	public void FunctionName_ReturnsFunctionName()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.FunctionName;

		// Assert
		result.ShouldBe("test-function");
	}

	[Fact]
	public void FunctionVersion_ReturnsFunctionVersion()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.FunctionVersion;

		// Assert
		result.ShouldBe("1.0.0");
	}

	[Fact]
	public void InvokedFunctionArn_ReturnsArn()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.InvokedFunctionArn;

		// Assert
		result.ShouldBe("arn:aws:lambda:us-east-1:123456789012:function:test-function");
	}

	[Fact]
	public void MemoryLimitInMB_ReturnsMemoryLimit()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.MemoryLimitInMB;

		// Assert
		result.ShouldBe(256);
	}

	[Fact]
	public void LogGroupName_ReturnsLogGroup()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.LogGroupName;

		// Assert
		result.ShouldBe("/aws/lambda/test-function");
	}

	[Fact]
	public void LogStreamName_ReturnsLogStream()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.LogStreamName;

		// Assert
		result.ShouldBe("2025/01/01/[$LATEST]abcdef123456");
	}

	[Fact]
	public void CloudProvider_ReturnsAws()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.CloudProvider;

		// Assert
		result.ShouldBe("AWS");
	}

	[Fact]
	public void AccountId_ExtractsFromArn()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.AccountId;

		// Assert
		result.ShouldBe("123456789012");
	}

	[Fact]
	public void AccountId_ReturnsUnknown_WhenArnInvalid()
	{
		// Arrange
		var context = new TestLambdaContext
		{
			InvokedFunctionArn = "invalid-arn"
		};
		using var sut = new AwsLambdaServerlessContext(context, _logger);

		// Act
		var result = sut.AccountId;

		// Assert
		result.ShouldBe("unknown");
	}

	[Fact]
	public void Region_ReturnsAwsRegionFromEnvironment()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-1");
		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);

		try
		{
			using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

			// Act
			var result = sut.Region;

			// Assert
			result.ShouldBe("eu-west-1");
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_REGION", null);
		}
	}

	[Fact]
	public void Region_FallsBackToDefaultRegion()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_REGION", null);
		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", "ap-southeast-1");

		try
		{
			using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

			// Act
			var result = sut.Region;

			// Assert
			result.ShouldBe("ap-southeast-1");
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);
		}
	}

	[Fact]
	public void Region_DefaultsToUsEast1_WhenNoEnvironmentVariables()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_REGION", null);
		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);

		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.Region;

		// Assert
		result.ShouldBe("us-east-1");
	}

	[Fact]
	public void RemainingExecutionTime_ReturnsRemainingTime()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.RemainingExecutionTime;

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void LambdaContext_ReturnsUnderlyingContext()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.LambdaContext;

		// Assert
		result.ShouldBeSameAs(_lambdaContext);
	}

	[Fact]
	public void Platform_ReturnsAwsLambda()
	{
		// Arrange
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act
		var result = sut.Platform;

		// Assert
		result.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Arrange
		var sut = new AwsLambdaServerlessContext(_lambdaContext, _logger);

		// Act & Assert
		Should.NotThrow(() => sut.Dispose());
	}
}
