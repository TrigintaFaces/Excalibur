// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Hosting.AzureFunctions;

using Microsoft.Azure.Functions.Worker;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureFunctionsHostProviderShould : UnitTestBase
{
	private readonly AzureFunctionsHostProvider _sut = new(EnabledTestLogger.Create());

	[Fact]
	public void Platform_ReturnAzureFunctions()
	{
		_sut.Platform.ShouldBe(ServerlessPlatform.AzureFunctions);
	}

	[Fact]
	public void IsAvailable_DetectBasedOnEnvironmentVariables()
	{
		ClearEnvironment();
		_sut.IsAvailable.ShouldBeFalse();

		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
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
			EnableMetrics = true,
		};
		options.AzureFunctions.EnableDurableFunctions = true;

		_sut.ConfigureServices(services, options);

		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessContext));
	}

	[Fact]
	public void ConfigureServices_CurrentDefaultContextResolutionThrows()
	{
		var services = new ServiceCollection();
		_sut.ConfigureServices(services, new ServerlessHostOptions());
		using var provider = services.BuildServiceProvider();

		Should.Throw<NotSupportedException>(() => provider.GetRequiredService<IServerlessContext>());
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
		options.EnvironmentVariables["Dispatch:Mode"] = "AzureFunctions";

		_sut.ConfigureHost(hostBuilder, options);
		using var host = hostBuilder.Build();

		host.Services.GetRequiredService<IConfiguration>()["Dispatch:Mode"].ShouldBe("AzureFunctions");
	}

	[Fact]
	public void CreateContext_ReturnAzureFunctionsContext_ForFunctionContextInput()
	{
		var functionContext = A.Fake<FunctionContext>();

		using var context = _sut.CreateContext(functionContext);

		context.ShouldBeOfType<AzureFunctionsServerlessContext>();
		context.Platform.ShouldBe(ServerlessPlatform.AzureFunctions);
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
	public void InternalBootstrapTypes_ExposeExpectedDefaultBehavior()
	{
		var providerType = typeof(AzureFunctionsHostProvider);

		var invocationFeaturesType = providerType.GetNestedType("DefaultInvocationFeatures", BindingFlags.NonPublic);
		invocationFeaturesType.ShouldNotBeNull();
		var invocationFeatures = Activator.CreateInstance(invocationFeaturesType!);
		invocationFeatures.ShouldNotBeNull();

		var setMethod = invocationFeaturesType!.GetMethod("Set")!.MakeGenericMethod(typeof(string));
		_ = setMethod.Invoke(invocationFeatures, ["enabled"]);
		var getMethod = invocationFeaturesType.GetMethod("Get")!.MakeGenericMethod(typeof(string));
		getMethod.Invoke(invocationFeatures, null).ShouldBe("enabled");
		var entries = ((System.Collections.IEnumerable)invocationFeatures!).Cast<object>().ToArray();
		entries.Length.ShouldBe(1);

		var traceContextType = providerType.GetNestedType("DefaultTraceContext", BindingFlags.NonPublic);
		traceContextType.ShouldNotBeNull();
		var traceContext = Activator.CreateInstance(traceContextType!);
		traceContext.ShouldNotBeNull();
		traceContextType!.GetProperty("TraceParent")!.GetValue(traceContext)!.ToString()!
			.ShouldStartWith("00-");
		traceContextType.GetProperty("TraceState")!.GetValue(traceContext).ShouldBeNull();

		var retryContextType = providerType.GetNestedType("DefaultRetryContext", BindingFlags.NonPublic);
		retryContextType.ShouldNotBeNull();
		var retryContext = Activator.CreateInstance(retryContextType!);
		retryContext.ShouldNotBeNull();
		retryContextType!.GetProperty("RetryCount")!.GetValue(retryContext).ShouldBe(0);
		retryContextType.GetProperty("MaxRetryCount")!.GetValue(retryContext).ShouldBe(3);

		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "orders-site");
		try
		{
			var defaultContextType = providerType.GetNestedType("DefaultFunctionContext", BindingFlags.NonPublic);
			defaultContextType.ShouldNotBeNull();
			var uninitialized = RuntimeHelpers.GetUninitializedObject(defaultContextType!);

			defaultContextType!.GetProperty("FunctionName")!.GetValue(uninitialized).ShouldBe("orders-site");
			var itemsProperty = defaultContextType.GetProperty("Items")!;
			_ = itemsProperty.GetValue(uninitialized);
			var itemsSetterEx = Should.Throw<TargetInvocationException>(() =>
				itemsProperty.SetValue(uninitialized, new Dictionary<object, object>()));
			itemsSetterEx.InnerException.ShouldBeOfType<NotSupportedException>();
			defaultContextType.GetProperty("Features")!.GetValue(uninitialized).ShouldBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
		}
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
		_sut.GetService(typeof(AzureFunctionsHostProvider)).ShouldBeSameAs(_sut);
		_sut.GetService(typeof(object)).ShouldBeSameAs(_sut);
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	private static void ClearEnvironment()
	{
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
		Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);
	}
}
