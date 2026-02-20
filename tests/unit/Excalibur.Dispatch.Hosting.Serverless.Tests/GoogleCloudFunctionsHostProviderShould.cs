// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleCloudFunctionsHostProviderShould : UnitTestBase
{
	private readonly GoogleCloudFunctionsHostProvider _sut = new(EnabledTestLogger.Create<GoogleCloudFunctionsHostProvider>());

	[Fact]
	public void Platform_ReturnGoogleCloudFunctions()
	{
		_sut.Platform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
	}

	[Fact]
	public void IsAvailable_DetectBasedOnEnvironmentVariables()
	{
		ClearEnvironment();
		_sut.IsAvailable.ShouldBeFalse();

		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");
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

		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessContext));
	}

	[Fact]
	public void ConfigureServices_AllowResolvingDefaultServerlessContext()
	{
		var services = new ServiceCollection();
		_sut.ConfigureServices(services, new ServerlessHostOptions());
		using var provider = services.BuildServiceProvider();

		using var context = provider.GetRequiredService<IServerlessContext>();
		context.ShouldBeOfType<GoogleCloudFunctionsServerlessContext>();
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
		options.EnvironmentVariables["Dispatch:Mode"] = "GoogleCloudFunctions";

		_sut.ConfigureHost(hostBuilder, options);
		using var host = hostBuilder.Build();

		host.Services.GetRequiredService<IConfiguration>()["Dispatch:Mode"].ShouldBe("GoogleCloudFunctions");
	}

	[Fact]
	public void CreateContext_ReturnContext_ForAnyObject()
	{
		using var context = _sut.CreateContext(new { });

		context.ShouldBeOfType<GoogleCloudFunctionsServerlessContext>();
		context.Platform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
	}

	[Fact]
	public void CreateContext_ThrowOnNullPlatformContext()
	{
		Should.Throw<ArgumentNullException>(() => _sut.CreateContext(null!));
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
		A.CallTo(() => context.RemainingTime).Returns(TimeSpan.FromMilliseconds(700));

		await Should.ThrowAsync<TimeoutException>(() => _sut.ExecuteAsync(
			"ping",
			context,
			static async (_, _, token) =>
			{
				await Task.Delay(TimeSpan.FromSeconds(2), token);
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
				await Task.Delay(10, token);
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
	public void GetService_ReturnProvider_WhenAssignable()
	{
		_sut.GetService(typeof(GoogleCloudFunctionsHostProvider)).ShouldBeSameAs(_sut);
		_sut.GetService(typeof(object)).ShouldBeSameAs(_sut);
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	private static void ClearEnvironment()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
		Environment.SetEnvironmentVariable("FUNCTION_REGION", null);
		Environment.SetEnvironmentVariable("K_SERVICE", null);
	}
}
