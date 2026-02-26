// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Hosting.Tests.Serverless;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DefaultServerlessContextShould : UnitTestBase
{
	[Fact]
	public void CreateWithAwsPlatform()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.Platform.ShouldBe(ServerlessPlatform.AwsLambda);
		ctx.CloudProvider.ShouldBe("AWS");
	}

	[Fact]
	public void CreateWithAzurePlatform()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AzureFunctions, NullLogger.Instance);

		// Assert
		ctx.Platform.ShouldBe(ServerlessPlatform.AzureFunctions);
		ctx.CloudProvider.ShouldBe("Azure");
	}

	[Fact]
	public void CreateWithGooglePlatform()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.GoogleCloudFunctions, NullLogger.Instance);

		// Assert
		ctx.Platform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		ctx.CloudProvider.ShouldBe("Google Cloud");
	}

	[Fact]
	public void CreateWithUnknownPlatform()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.Unknown, NullLogger.Instance);

		// Assert
		ctx.CloudProvider.ShouldBe("Unknown");
	}

	[Fact]
	public void HaveUniqueRequestId()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.RequestId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(ctx.RequestId, out _).ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultFunctionVersion()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.FunctionVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void HaveMemoryLimit()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.MemoryLimitInMB.ShouldBe(512);
	}

	[Fact]
	public void HaveLocalRegion()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.Region.ShouldBe("local");
		ctx.AccountId.ShouldBe("local");
	}

	[Fact]
	public void HaveExecutionDeadlineInFuture()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		var after = DateTimeOffset.UtcNow;

		// Assert
		ctx.ExecutionDeadline.ShouldBeGreaterThanOrEqualTo(before);
		ctx.ExecutionDeadline.ShouldBeLessThanOrEqualTo(after.AddMinutes(16));
	}

	[Fact]
	public void HavePositiveRemainingTime()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.RemainingTime.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void HaveEmptyPropertiesByDefault()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void AllowAddingProperties()
	{
		// Arrange
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		ctx.Properties["key"] = "value";

		// Assert
		ctx.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void HaveInvokedFunctionArn()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.InvokedFunctionArn.ShouldNotBeNullOrEmpty();
		ctx.InvokedFunctionArn.ShouldStartWith("local:AWSLAMBDA:function:");
	}

	[Fact]
	public void HaveLogGroupAndStreamNames()
	{
		// Act
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		ctx.LogGroupName.ShouldNotBeNullOrEmpty();
		ctx.LogStreamName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ReturnSelfForMatchingServiceType()
	{
		// Arrange
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = ctx.GetService(typeof(DefaultServerlessContext));

		// Assert
		result.ShouldBeSameAs(ctx);
	}

	[Fact]
	public void ReturnNullForNonMatchingServiceType()
	{
		// Arrange
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = ctx.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert
		Should.NotThrow(() => ctx.Dispose());
	}

	[Fact]
	public void TrackElapsedTime()
	{
		// Arrange
		using var ctx = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act — let a tiny amount of time pass
		Thread.SpinWait(100);

		// Assert
		ctx.ElapsedTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}
}
