// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Hosting.AwsLambda;
using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Tests.Functional.Serverless;

/// <summary>
/// E2E tests for serverless cold-start scenarios.
/// Validates: Lambda DI bootstrap -> Dispatch configured -> message dispatched and handled.
/// </summary>
/// <remarks>
/// Beads issue: Excalibur.Dispatch-ly32um (G.3).
/// These tests verify the full cold-start lifecycle without requiring real AWS infrastructure.
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Serverless")]
[Trait("Feature", "ColdStart")]
public sealed class ServerlessColdStartE2EShould : FunctionalTestBase
{
	private ServiceProvider? _serviceProvider;

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public async Task BootstrapLambdaHostAndDispatchMessage()
	{
		// Arrange: full DI with Dispatch + Lambda serverless
		var services = CreateServerlessServices();
		services.AddTransient<IActionHandler<ColdStartTestAction>, ColdStartTestActionHandler>();

		_serviceProvider = services.BuildServiceProvider();
		ColdStartTestActionHandler.Reset();

		// Act: resolve core services (cold-start DI resolution)
		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var hostProvider = _serviceProvider.GetRequiredService<IServerlessHostProvider>();
		var coldStartOptimizer = _serviceProvider.GetRequiredService<IColdStartOptimizer>();
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Verify DI wiring
		hostProvider.Platform.ShouldBe(ServerlessPlatform.AwsLambda);

		// Cold start warmup
		await coldStartOptimizer.WarmupAsync().ConfigureAwait(false);

		// Dispatch a message through the pipeline
		var action = new ColdStartTestAction { Input = "cold-start-test" };
		var context = contextFactory.CreateContext();
		context.MessageId = Guid.NewGuid().ToString();

		var result = await dispatcher.DispatchAsync(action, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue($"Dispatch failed: {result.ErrorMessage}");
		ColdStartTestActionHandler.LastInput.ShouldBe("cold-start-test");
		ColdStartTestActionHandler.HandleCount.ShouldBe(1);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public async Task CompleteColdStartWithinBudget()
	{
		// Arrange
		var services = CreateServerlessServices();
		services.AddTransient<IActionHandler<ColdStartTestAction>, ColdStartTestActionHandler>();
		ColdStartTestActionHandler.Reset();

		// Act: measure full cold-start time (DI build + warmup + first dispatch)
		var sw = Stopwatch.StartNew();

		_serviceProvider = services.BuildServiceProvider();
		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var coldStartOptimizer = _serviceProvider.GetRequiredService<IColdStartOptimizer>();
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		await coldStartOptimizer.WarmupAsync().ConfigureAwait(false);

		var action = new ColdStartTestAction { Input = "budget-test" };
		var context = contextFactory.CreateContext();
		context.MessageId = Guid.NewGuid().ToString();

		var result = await dispatcher.DispatchAsync(action, context, CancellationToken.None)
			.ConfigureAwait(false);
		sw.Stop();

		// Assert: full cold-start should complete within generous budget
		// (2 seconds is very generous for in-process dispatch without real AWS)
		result.IsSuccess.ShouldBeTrue();
		sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2),
			$"Cold start took {sw.Elapsed.TotalMilliseconds:F0}ms, exceeds 2000ms budget");
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public async Task PropagateHandlerExceptionAsFailedResult()
	{
		// Arrange
		var services = CreateServerlessServices();
		services.AddTransient<IActionHandler<FailingColdStartAction>, FailingColdStartActionHandler>();

		_serviceProvider = services.BuildServiceProvider();
		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Act
		var action = new FailingColdStartAction();
		var context = contextFactory.CreateContext();
		context.MessageId = Guid.NewGuid().ToString();

		var result = await dispatcher.DispatchAsync(action, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert: handler failure should propagate as failed result
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public async Task HandleCancellationDuringSlowDispatch()
	{
		// Arrange
		var services = CreateServerlessServices();
		services.AddTransient<IActionHandler<SlowColdStartAction>, SlowColdStartActionHandler>();

		_serviceProvider = services.BuildServiceProvider();
		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Act: cancel before handler can complete
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		var action = new SlowColdStartAction();
		var context = contextFactory.CreateContext();
		context.MessageId = Guid.NewGuid().ToString();

		// The dispatcher may either throw OCE or return a failed result -- either is acceptable
		try
		{
			var result = await dispatcher.DispatchAsync(action, context, cts.Token)
				.ConfigureAwait(false);

			// If it returned without throwing, result should indicate failure
			result.IsSuccess.ShouldBeFalse(
				"Cancelled dispatch should either throw OperationCanceledException or return failed result");
		}
		catch (OperationCanceledException)
		{
			// Expected: cancellation propagated as exception
		}
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public async Task ExecuteMultipleDispatchesAfterColdStart()
	{
		// Arrange
		var services = CreateServerlessServices();
		services.AddTransient<IActionHandler<ColdStartTestAction>, ColdStartTestActionHandler>();

		_serviceProvider = services.BuildServiceProvider();
		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		ColdStartTestActionHandler.Reset();

		// Act: dispatch 10 messages sequentially (simulating warm invocations after cold start)
		for (var i = 0; i < 10; i++)
		{
			var action = new ColdStartTestAction { Input = $"warm-{i}" };
			var context = contextFactory.CreateContext();
			context.MessageId = Guid.NewGuid().ToString();

			var result = await dispatcher.DispatchAsync(action, context, CancellationToken.None)
				.ConfigureAwait(false);
			result.IsSuccess.ShouldBeTrue($"Dispatch {i} failed: {result.ErrorMessage}");
		}

		// Assert
		ColdStartTestActionHandler.HandleCount.ShouldBe(10);
		ColdStartTestActionHandler.LastInput.ShouldBe("warm-9");
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	public void ResolveLambdaHostProviderFromDI()
	{
		// Arrange: minimal DI with just Lambda hosting (no dispatch pipeline)
		var services = new ServiceCollection();
		services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

		// AwsLambdaHostProvider requires non-generic ILogger -- register it explicitly
		services.AddSingleton<ILogger>(static sp =>
			sp.GetRequiredService<ILoggerFactory>().CreateLogger("AwsLambda"));
		services.AddAwsLambdaServerless();

		_serviceProvider = services.BuildServiceProvider();

		// Act
		var hostProvider = _serviceProvider.GetRequiredService<IServerlessHostProvider>();
		var coldStartOptimizer = _serviceProvider.GetRequiredService<IColdStartOptimizer>();

		// Assert: verifies DI registrations are correct
		hostProvider.ShouldBeOfType<AwsLambdaHostProvider>();
		coldStartOptimizer.ShouldBeOfType<AwsLambdaColdStartOptimizer>();
		hostProvider.Platform.ShouldBe(ServerlessPlatform.AwsLambda);

		// IsAvailable depends on env vars -- in test environment, should be false
		// (no AWS_LAMBDA_FUNCTION_NAME set)
		hostProvider.IsAvailable.ShouldBeFalse(
			"IsAvailable should be false when not running in actual Lambda environment");
	}

	public override async Task DisposeAsync()
	{
		if (_serviceProvider is not null)
		{
			await _serviceProvider.DisposeAsync().ConfigureAwait(false);
		}

		await base.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Creates a <see cref="ServiceCollection"/> configured for Lambda serverless + Dispatch.
	/// </summary>
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code, not trimmed")]
	private static ServiceCollection CreateServerlessServices()
	{
		var services = new ServiceCollection();
		services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

		// AwsLambdaHostProvider constructor takes non-generic ILogger
		services.AddSingleton<ILogger>(static sp =>
			sp.GetRequiredService<ILoggerFactory>().CreateLogger("AwsLambda"));

		services.AddDispatchPipeline();
		services.AddDispatchHandlers(typeof(ServerlessColdStartE2EShould).Assembly);
		services.AddAwsLambdaServerless();

		return services;
	}
}

#region Test message types and handlers

/// <summary>
/// Test action for cold-start E2E tests.
/// </summary>
public sealed record ColdStartTestAction : IDispatchAction
{
	public string Input { get; init; } = string.Empty;
}

/// <summary>
/// Handler that tracks invocations for verification.
/// </summary>
public sealed class ColdStartTestActionHandler : IActionHandler<ColdStartTestAction>
{
	private static int _handleCount;
	private static string _lastInput = string.Empty;

	public static int HandleCount => Volatile.Read(ref _handleCount);
	public static string LastInput => Volatile.Read(ref _lastInput);

	public static void Reset()
	{
		Interlocked.Exchange(ref _handleCount, 0);
		Volatile.Write(ref _lastInput, string.Empty);
	}

	public Task HandleAsync(ColdStartTestAction action, CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _handleCount);
		Volatile.Write(ref _lastInput, action.Input);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Action that triggers a handler failure.
/// </summary>
public sealed record FailingColdStartAction : IDispatchAction;

/// <summary>
/// Handler that always throws.
/// </summary>
public sealed class FailingColdStartActionHandler : IActionHandler<FailingColdStartAction>
{
	public Task HandleAsync(FailingColdStartAction action, CancellationToken cancellationToken)
		=> throw new InvalidOperationException("Simulated handler failure during serverless execution");
}

/// <summary>
/// Action for testing cancellation behavior.
/// </summary>
public sealed record SlowColdStartAction : IDispatchAction;

/// <summary>
/// Handler that delays to allow cancellation testing.
/// </summary>
public sealed class SlowColdStartActionHandler : IActionHandler<SlowColdStartAction>
{
	public async Task HandleAsync(SlowColdStartAction action, CancellationToken cancellationToken)
	{
		// Wait long enough to be cancelled by the test's 50ms timeout
		await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
	}
}

#endregion
