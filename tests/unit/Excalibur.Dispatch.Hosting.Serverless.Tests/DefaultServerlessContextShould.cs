// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultServerlessContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DefaultServerlessContextShould : UnitTestBase
{
	[Theory]
	[InlineData(ServerlessPlatform.AwsLambda)]
	[InlineData(ServerlessPlatform.AzureFunctions)]
	[InlineData(ServerlessPlatform.GoogleCloudFunctions)]
	[InlineData(ServerlessPlatform.Unknown)]
	public void Constructor_WithPlatform_SetsPropertiesCorrectly(ServerlessPlatform platform)
	{
		// Act
		using var context = new DefaultServerlessContext(platform, NullLogger.Instance);

		// Assert
		context.RequestId.ShouldNotBeNullOrWhiteSpace();
		context.FunctionName.ShouldNotBeNullOrWhiteSpace();
		context.FunctionVersion.ShouldBe("1.0.0");
		context.InvokedFunctionArn.ShouldNotBeNullOrWhiteSpace();
		context.MemoryLimitInMB.ShouldBe(512);
		context.LogGroupName.ShouldNotBeNullOrWhiteSpace();
		context.LogStreamName.ShouldNotBeNullOrWhiteSpace();
		context.Region.ShouldBe("local");
		context.AccountId.ShouldBe("local");
		context.Platform.ShouldBe(platform);
	}

	[Fact]
	public void CloudProvider_ForAwsLambda_ReturnsAWS()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("AWS");
	}

	[Fact]
	public void CloudProvider_ForAzureFunctions_ReturnsAzure()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AzureFunctions, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("Azure");
	}

	[Fact]
	public void CloudProvider_ForGoogleCloudFunctions_ReturnsGoogleCloud()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.GoogleCloudFunctions, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("Google Cloud");
	}

	[Fact]
	public void CloudProvider_ForUnknown_ReturnsUnknown()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.Unknown, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("Unknown");
	}

	[Fact]
	public void ExecutionDeadline_ShouldBeFutureTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		var after = DateTimeOffset.UtcNow;

		// Assert
		context.ExecutionDeadline.ShouldBeGreaterThanOrEqualTo(before);
		context.ExecutionDeadline.ShouldBeLessThanOrEqualTo(after.AddMinutes(16));
	}

	[Fact]
	public void InvokedFunctionArn_ContainsPlatformName()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.InvokedFunctionArn.ShouldContain("AWSLAMBDA");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new DefaultServerlessContext(ServerlessPlatform.AwsLambda, null!));
	}

	[Fact]
	public void RemainingTime_ShouldBePositive()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.RemainingTime.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void ElapsedTime_ShouldBeNonNegative()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.ElapsedTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void Properties_ShouldBeEmptyDictionary()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.Properties.ShouldNotBeNull();
		context.Properties.Count.ShouldBe(0);
	}

	[Fact]
	public void Properties_ShouldSupportAddAndRetrieve()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		context.Properties["testKey"] = "testValue";

		// Assert
		context.Properties.Count.ShouldBe(1);
		context.Properties["testKey"].ShouldBe("testValue");
	}

	[Fact]
	public void TraceContext_ShouldBeNullByDefault()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.TraceContext.ShouldBeNull();
	}

	[Fact]
	public void TraceContext_ShouldBeSettable()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		var trace = new TraceContext
		{
			TraceId = "trace-123",
			SpanId = "span-456",
			ParentSpanId = "parent-789",
			TraceFlags = "01",
			TraceState = "key=value",
			TraceParent = "00-trace-123-span-456-01",
		};

		// Act
		context.TraceContext = trace;

		// Assert
		context.TraceContext.ShouldNotBeNull();
		context.TraceContext.TraceId.ShouldBe("trace-123");
		context.TraceContext.SpanId.ShouldBe("span-456");
		context.TraceContext.ParentSpanId.ShouldBe("parent-789");
	}

	[Fact]
	public void GetService_WithIServerlessContext_ReturnsSelf()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(IServerlessContext));

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(context);
	}

	[Fact]
	public void GetService_WithIServerlessPlatformDetails_ReturnsSelf()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(IServerlessPlatformDetails));

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(context);
	}

	[Fact]
	public void GetService_WithNonMatchingType_ReturnsNull()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetService_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.GetService(null!));
	}

	[Fact]
	public void Dispose_ShouldBeSafe_WhenCalledMultipleTimes()
	{
		// Arrange
		var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert - should not throw
		context.Dispose();
		context.Dispose();
	}

	[Fact]
	public void Logger_ShouldReturnProvidedInstance()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.Logger.ShouldBe(NullLogger.Instance);
	}

	[Fact]
	public void PlatformContext_ShouldNotBeNull()
	{
		// Act
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.PlatformContext.ShouldNotBeNull();
	}

	[Fact]
	public void RequestId_ShouldBeUniquePerInstance()
	{
		// Act
		using var context1 = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		using var context2 = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context1.RequestId.ShouldNotBe(context2.RequestId);
	}
}
