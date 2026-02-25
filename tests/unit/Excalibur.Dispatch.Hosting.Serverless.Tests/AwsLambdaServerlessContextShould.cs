// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Lambda.Core;

using Excalibur.Dispatch.Hosting.AwsLambda;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaServerlessContextShould : UnitTestBase
{
	private readonly StubLambdaContext _lambdaContext = new()
	{
		AwsRequestId = "req-123",
		FunctionName = "orders-handler",
		FunctionVersion = "7",
		InvokedFunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:orders-handler",
		MemoryLimitInMB = 256,
		LogGroupName = "/aws/lambda/orders-handler",
		LogStreamName = "2026/01/01/[$LATEST]abcdef",
		RemainingTime = TimeSpan.FromSeconds(45)
	};

	[Fact]
	public void ThrowWhenPlatformContextIsNotLambdaContext()
	{
		var ex = Should.Throw<ArgumentException>(() => new AwsLambdaServerlessContext(new object(), NullLogger.Instance));
		ex.Message.ShouldContain("ILambdaContext");
	}

	[Fact]
	public void ExposeLambdaValuesAndPlatformMetadata()
	{
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance);

		sut.RequestId.ShouldBe("req-123");
		sut.FunctionName.ShouldBe("orders-handler");
		sut.FunctionVersion.ShouldBe("7");
		sut.InvokedFunctionArn.ShouldBe("arn:aws:lambda:us-east-1:123456789012:function:orders-handler");
		sut.MemoryLimitInMB.ShouldBe(256);
		sut.LogGroupName.ShouldBe("/aws/lambda/orders-handler");
		sut.LogStreamName.ShouldBe("2026/01/01/[$LATEST]abcdef");
		sut.CloudProvider.ShouldBe("AWS");
		sut.Platform.ShouldBe(ServerlessPlatform.AwsLambda);
		sut.LambdaContext.ShouldBeSameAs(_lambdaContext);
		sut.RemainingExecutionTime.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void ParseAccountIdFromArn()
	{
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance);

		sut.AccountId.ShouldBe("123456789012");
	}

	[Fact]
	public void ReturnUnknownAccountId_WhenArnCannotBeParsed()
	{
		var context = new StubLambdaContext { InvokedFunctionArn = "bad-arn" };
		using var sut = new AwsLambdaServerlessContext(context, NullLogger.Instance);

		sut.AccountId.ShouldBe("unknown");
	}

	[Fact]
	public void ResolveRegionFromAwsRegionThenDefaultThenFallback()
	{
		Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-1");
		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", "ap-southeast-1");
		using (var fromPrimary = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance))
		{
			fromPrimary.Region.ShouldBe("eu-west-1");
		}

		Environment.SetEnvironmentVariable("AWS_REGION", null);
		using (var fromFallback = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance))
		{
			fromFallback.Region.ShouldBe("ap-southeast-1");
		}

		Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", null);
		using (var defaultRegion = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance))
		{
			defaultRegion.Region.ShouldBe("us-east-1");
		}
	}

	[Fact]
	public void ExecutionDeadline_TrackRemainingTime()
	{
		using var sut = new AwsLambdaServerlessContext(_lambdaContext, NullLogger.Instance);

		var delta = sut.ExecutionDeadline - DateTimeOffset.UtcNow;
		delta.ShouldBeGreaterThan(TimeSpan.FromSeconds(40));
	}

	private sealed class StubLambdaContext : ILambdaContext
	{
		public string AwsRequestId { get; set; } = Guid.NewGuid().ToString();
		public IClientContext? ClientContext => null;
		public string FunctionName { get; set; } = "function";
		public string FunctionVersion { get; set; } = "$LATEST";
		public ICognitoIdentity? Identity => null;
		public string InvokedFunctionArn { get; set; } = "arn:aws:lambda:us-east-1:123456789012:function:function";
		public ILambdaLogger Logger { get; } = new StubLambdaLogger();
		public string LogGroupName { get; set; } = "/aws/lambda/function";
		public string LogStreamName { get; set; } = "stream";
		public int MemoryLimitInMB { get; set; } = 128;
		public TimeSpan RemainingTime { get; set; } = TimeSpan.FromMinutes(1);
	}

	private sealed class StubLambdaLogger : ILambdaLogger
	{
		public void Log(string message)
		{
		}

		public void LogLine(string message)
		{
		}
	}
}
