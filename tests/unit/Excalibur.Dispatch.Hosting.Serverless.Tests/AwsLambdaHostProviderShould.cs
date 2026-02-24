// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Lambda.Core;

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaHostProviderShould : UnitTestBase
{
	private readonly AwsLambdaHostProvider _sut = new(EnabledTestLogger.Create());

	[Fact]
	public void Platform_ReturnAwsLambda()
	{
		_sut.Platform.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void IsAvailable_ReturnFalse_WhenNoLambdaEnvironmentVariables()
	{
		ClearEnvironment();

		_sut.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void IsAvailable_ReturnTrue_WhenLambdaFunctionNameIsSet()
	{
		ClearEnvironment();
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "orders-handler");

		_sut.IsAvailable.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureServices_ThrowOnNullArguments()
	{
		Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(null!, new ServerlessHostOptions()));
		Should.Throw<ArgumentNullException>(() => _sut.ConfigureServices(new ServiceCollection(), null!));
	}

	[Fact]
	public void ConfigureServices_RegisterExpectedServices()
	{
		var services = new ServiceCollection();
		var options = new ServerlessHostOptions
		{
			EnableColdStartOptimization = true,
			EnableDistributedTracing = true,
			EnableMetrics = true
		};

		_sut.ConfigureServices(services, options);

		services.ShouldContain(sd => sd.ServiceType == typeof(DefaultLambdaJsonSerializer));
		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessContext));
	}

	[Fact]
	public void ConfigureServices_AllowResolvingDefaultServerlessContext()
	{
		var services = new ServiceCollection();
		var options = new ServerlessHostOptions();
		_sut.ConfigureServices(services, options);
		using var provider = services.BuildServiceProvider();

		using var context = provider.GetRequiredService<IServerlessContext>();
		context.ShouldBeOfType<AwsLambdaServerlessContext>();
	}

	[Fact]
	public void ConfigureHost_ThrowOnNullArguments()
	{
		Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(null!, new ServerlessHostOptions()));
		Should.Throw<ArgumentNullException>(() => _sut.ConfigureHost(Host.CreateDefaultBuilder(), null!));
	}

	[Fact]
	public void ConfigureHost_ApplyEnvironmentVariablesToConfiguration()
	{
		var hostBuilder = Host.CreateDefaultBuilder();
		var options = new ServerlessHostOptions();
		options.EnvironmentVariables["Dispatch:Mode"] = "Lambda";

		_sut.ConfigureHost(hostBuilder, options);
		using var host = hostBuilder.Build();

		var configuration = host.Services.GetRequiredService<IConfiguration>();
		configuration["Dispatch:Mode"].ShouldBe("Lambda");
	}

	[Fact]
	public void CreateContext_ReturnAwsLambdaContext_ForLambdaContextInput()
	{
		using var context = _sut.CreateContext(new TestLambdaContext());

		context.ShouldBeOfType<AwsLambdaServerlessContext>();
		context.Platform.ShouldBe(ServerlessPlatform.AwsLambda);
	}

	[Fact]
	public void CreateContext_ThrowOnNullOrInvalidPlatformContext()
	{
		Should.Throw<ArgumentNullException>(() => _sut.CreateContext(null!));
		Should.Throw<ArgumentException>(() => _sut.CreateContext(new object()));
	}

	[Fact]
	public async Task ExecuteAsync_ExecuteHandlerAndReturnResult()
	{
		var context = A.Fake<IServerlessContext>();
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromSeconds(1));

		var result = await _sut.ExecuteAsync(
			"ping",
			context,
			static (input, _, _) => Task.FromResult(input + "-ok"),
			CancellationToken.None);

		result.ShouldBe("ping-ok");
	}

	[Fact]
	public async Task ExecuteAsync_MapInternalCancellationToTimeoutException()
	{
		var context = A.Fake<IServerlessContext>();
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromMilliseconds(150));

		await Should.ThrowAsync<TimeoutException>(() => _sut.ExecuteAsync(
			"ping",
			context,
			static async (_, _, token) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromSeconds(2), token);
				return "unreachable";
			},
			CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_PropagateExternalCancellation()
	{
		var context = A.Fake<IServerlessContext>();
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromMinutes(1));
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() => _sut.ExecuteAsync(
			"ping",
			context,
			static async (_, _, token) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10, token);
				return "unreachable";
			},
			cts.Token));
	}

	[Fact]
	public async Task ExecuteAsync_PropagateHandlerExceptions()
	{
		var context = A.Fake<IServerlessContext>();
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromMinutes(1));

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => _sut.ExecuteAsync<string, string>(
			"ping",
			context,
			static (_, _, _) => throw new InvalidOperationException("handler failed"),
			CancellationToken.None));

		ex.Message.ShouldBe("handler failed");
	}

	[Fact]
	public async Task ExecuteAsync_ThrowOnNullArguments()
	{
		var context = A.Fake<IServerlessContext>();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExecuteAsync<string, string>(null!, context, (_, _, _) => Task.FromResult(string.Empty), CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExecuteAsync("ping", null!, (_, _, _) => Task.FromResult(string.Empty), CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExecuteAsync<string, string>("ping", context, null!, CancellationToken.None));
	}

	[Fact]
	public void ConfigureServices_DefaultContextExposeBootstrapLambdaContextValues()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "orders-handler");
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", "3");
		Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-1");
		Environment.SetEnvironmentVariable("AWS_ACCOUNT_ID", "999999999999");
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", "256");
		Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_STREAM_NAME", "stream-1");

		try
		{
			var services = new ServiceCollection();
			_sut.ConfigureServices(services, new ServerlessHostOptions());
			using var provider = services.BuildServiceProvider();
			using var context = provider.GetRequiredService<IServerlessContext>();
			var lambdaContext = context.ShouldBeOfType<AwsLambdaServerlessContext>().LambdaContext;

			lambdaContext.AwsRequestId.ShouldNotBeNullOrWhiteSpace();
			lambdaContext.FunctionName.ShouldBe("orders-handler");
			lambdaContext.FunctionVersion.ShouldBe("3");
			lambdaContext.InvokedFunctionArn.ShouldContain(":eu-west-1:999999999999:");
			lambdaContext.MemoryLimitInMB.ShouldBe(256);
			lambdaContext.LogGroupName.ShouldBe("/aws/lambda/orders-handler");
			lambdaContext.LogStreamName.ShouldContain("stream-1");
			lambdaContext.RemainingTime.ShouldBe(TimeSpan.FromMinutes(15));
			lambdaContext.Identity.ShouldBeNull();
			lambdaContext.ClientContext.ShouldBeNull();

			lambdaContext.Logger.Log("hello");
			lambdaContext.Logger.LogLine("world");
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", null);
			Environment.SetEnvironmentVariable("AWS_REGION", null);
			Environment.SetEnvironmentVariable("AWS_ACCOUNT_ID", null);
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", null);
			Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_STREAM_NAME", null);
		}
	}

	[Fact]
	public void GetService_ReturnProvider_WhenAssignable()
	{
		_sut.GetService(typeof(AwsLambdaHostProvider)).ShouldBeSameAs(_sut);
		_sut.GetService(typeof(object)).ShouldBeSameAs(_sut);
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	private static void ClearEnvironment()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
		Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", null);
	}

	private sealed class TestLambdaContext : ILambdaContext
	{
		public string AwsRequestId { get; set; } = Guid.NewGuid().ToString();
		public IClientContext? ClientContext => null;
		public string FunctionName { get; set; } = "function";
		public string FunctionVersion { get; set; } = "$LATEST";
		public ICognitoIdentity? Identity => null;
		public string InvokedFunctionArn { get; set; } = "arn:aws:lambda:us-east-1:123456789012:function:function";
		public ILambdaLogger Logger { get; } = new TestLambdaLogger();
		public string LogGroupName { get; set; } = "/aws/lambda/function";
		public string LogStreamName { get; set; } = "stream";
		public int MemoryLimitInMB { get; set; } = 128;
		public TimeSpan RemainingTime { get; set; } = TimeSpan.FromMinutes(1);
	}

	private sealed class TestLambdaLogger : ILambdaLogger
	{
		public void Log(string message)
		{
		}

		public void LogLine(string message)
		{
		}
	}
}
