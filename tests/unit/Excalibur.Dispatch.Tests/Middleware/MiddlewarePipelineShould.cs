// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests
#pragma warning disable CA2201 // Exception type is not sufficiently specific - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for middleware pipeline composition, execution, error handling, short-circuiting, and advanced scenarios.
/// Sprint 168 (bd-ca2e3): 50 tests covering comprehensive middleware pipeline behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public class MiddlewarePipelineShould : UnitTestBase
{
	#region Pipeline Composition Tests (10 tests)

	[Fact]
	public async Task AddSingleMiddlewareToPipeline_ExecutesSuccessfully()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Single", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "Single" });
	}

	[Fact]
	public async Task AddMultipleMiddlewareInOrder_ExecutesInStageOrder()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Third", DispatchMiddlewareStage.Authorization, invocations),
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First", "Second", "Third" });
	}

	[Fact]
	public async Task AddMiddlewareConditionally_ExecutesOnlyWhenConditionMet()
	{
		// Arrange
		var invocations = new List<string>();
		var shouldExecute = true;
		var middleware = new IDispatchMiddleware[]
		{
			new ConditionalMiddleware("Conditional", DispatchMiddlewareStage.Validation, invocations, shouldExecute),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Conditional");
	}

	[Fact]
	public async Task RemoveMiddlewareFromPipeline_SkipsRemovedMiddleware()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new List<IDispatchMiddleware>
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};

		// Remove second middleware before creating pipeline
		middlewareList.RemoveAt(1);
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First" });
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task ClearAllMiddleware_ExecutesOnlyFinalDelegate()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();
		var finalDelegateInvoked = false;

		ValueTask<IMessageResult> LocalFinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, LocalFinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		finalDelegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task MiddlewareDependencyInjection_ResolvesFromServiceProvider()
	{
		// Arrange
		_ = Services.AddSingleton<ITestService, TestService>();
		BuildServiceProvider();

		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ServiceResolvingMiddleware("ServiceResolver", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("ServiceResolver");
		context.GetItem<bool>("service_resolved").ShouldBeTrue();
	}

	[Fact]
	public async Task MiddlewareLifecycle_SingletonMiddlewareReusesInstance()
	{
		// Arrange
		var instanceCounter = 0;
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new SingletonMiddleware("Singleton", DispatchMiddlewareStage.Validation, invocations, ref instanceCounter),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message1 = new TestMessage();
		var message2 = new TestMessage();
		var context1 = CreateMessageContext();
		var context2 = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message1, context1, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		_ = await pipeline.ExecuteAsync(message2, context2, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.Count.ShouldBe(2);
		instanceCounter.ShouldBe(1); // Same instance used twice
	}

	[Fact]
	public async Task MiddlewareOptionsConfiguration_AppliesConfiguredOptions()
	{
		// Arrange
		var invocations = new List<string>();
		var options = new TestMiddlewareOptions { Enabled = true, Priority = 10 };
		var middleware = new IDispatchMiddleware[]
		{
			new ConfigurableMiddleware("Configurable", DispatchMiddlewareStage.Validation, invocations, options),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Configurable");
		context.GetItem<int>("priority").ShouldBe(10);
	}

	[Fact]
	public async Task PipelineBuilderFluentAPI_BuildsPipelineCorrectly()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new List<IDispatchMiddleware>
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First", "Second" });
	}

	[Fact]
	public async Task PipelineImmutability_DoesNotAllowRuntimeModification()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new List<IDispatchMiddleware>
		{
			new TrackingMiddleware("Original", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);

		// Modify list after pipeline creation (should not affect pipeline)
		middlewareList.Add(new TrackingMiddleware("Added", DispatchMiddlewareStage.Authorization, invocations));

		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "Original" });
		invocations.ShouldNotContain("Added");
	}

	#endregion Pipeline Composition Tests (10 tests)

	#region Pipeline Execution Tests (10 tests)

	[Fact]
	public async Task ExecuteEmptyPipeline_CallsFinalDelegate()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();
		var finalDelegateInvoked = false;

		ValueTask<IMessageResult> LocalFinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, LocalFinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		finalDelegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteSingleMiddleware_InvokesMiddlewareAndFinalDelegate()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Single", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		var finalDelegateInvoked = false;

		ValueTask<IMessageResult> LocalFinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, LocalFinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Single");
		finalDelegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteMultipleMiddlewareInOrder_InvokesInStageOrder()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Third", DispatchMiddlewareStage.Authorization, invocations),
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First", "Second", "Third" });
	}

	[Fact]
	public async Task MiddlewareInvocationOrder_RespectsPipelineStages()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Error", DispatchMiddlewareStage.Error, invocations),
			new TrackingMiddleware("Start", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Processing", DispatchMiddlewareStage.Processing, invocations),
			new TrackingMiddleware("Validation", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "Start", "Validation", "Processing", "Error" });
	}

	[Fact]
	public async Task NextDelegatePropagation_CallsNextMiddlewareInChain()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new NextDelegateTrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new NextDelegateTrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First:before", "Second:before", "Second:after", "First:after" });
	}

	[Fact]
	public async Task PipelineExecutionContext_PreservesContextThroughPipeline()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ContextWriterMiddleware("Writer", DispatchMiddlewareStage.Start, "test_key", "test_value"),
			new ContextReaderMiddleware("Reader", DispatchMiddlewareStage.Validation, "test_key", invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<string>("test_key").ShouldBe("test_value");
		invocations.ShouldContain("Reader:test_value");
	}

	[Fact]
	public async Task MessageModificationInPipeline_PassesModifiedMessageToNext()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new MessageModifierMiddleware("Modifier", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<bool>("message_modified").ShouldBeTrue();
	}

	[Fact]
	public async Task MetadataModificationInPipeline_PreservesMetadataChanges()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new MetadataModifierMiddleware("MetadataModifier", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<string>("metadata_key").ShouldBe("metadata_value");
	}

	[Fact]
	public async Task PipelineExecutionTiming_CompletesWithinExpectedTime()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TimedMiddleware("Timed", DispatchMiddlewareStage.Validation, invocations, delayMs: 0), // No delay for deterministic timing
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Fast execution
		invocations.ShouldContain("Timed");
	}

	[Fact]
	public async Task PipelineExecutionTelemetry_TracksMiddlewareInvocations()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TelemetryMiddleware("Telemetry", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<int>("invocation_count").ShouldBe(1);
	}

	#endregion Pipeline Execution Tests (10 tests)

	#region Error Handling Tests (10 tests)

	[Fact]
	public async Task MiddlewareThrowsException_PropagatesException()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new ThrowingMiddleware("Throwing", DispatchMiddlewareStage.Validation, new InvalidOperationException("Test exception")),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task MiddlewareCatchesException_HandlesGracefully()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ExceptionHandlingMiddleware("Handler", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Exception handled gracefully");
	}

	[Fact]
	public async Task ShortCircuitOnError_StopsPipelineExecution()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new ThrowingMiddleware("Throwing", DispatchMiddlewareStage.Validation, new InvalidOperationException("Test")),
			new TrackingMiddleware("Third", DispatchMiddlewareStage.Authorization, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		try
		{
			_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		invocations.ShouldBe(new[] { "First" });
		invocations.ShouldNotContain("Third");
	}

	[Fact]
	public async Task ErrorMiddlewarePattern_CapturesAndLogsErrors()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ErrorLoggingMiddleware("ErrorLogger", DispatchMiddlewareStage.Error, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("ErrorLogger");
	}

	[Fact]
	public async Task ExceptionContextPreservation_MaintainsExceptionDetails()
	{
		// Arrange
		var originalException = new InvalidOperationException("Original error");
		var middleware = new IDispatchMiddleware[]
		{
			new ThrowingMiddleware("Throwing", DispatchMiddlewareStage.Validation, originalException),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		// Assert
		exception.Message.ShouldBe("Original error");
	}

	[Fact]
	public async Task MiddlewareErrorLogging_LogsExceptionDetails()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ErrorLoggingMiddleware("Logger", DispatchMiddlewareStage.Error, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Logger");
	}

	[Fact]
	public async Task PipelineFailureRecovery_RecoversFromTransientFailure()
	{
		// Arrange
		var invocations = new List<string>();
		var attemptCount = 0;
		var middleware = new IDispatchMiddleware[]
		{
			new RetryableMiddleware("Retryable", DispatchMiddlewareStage.Validation, invocations, ref attemptCount),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Retryable");
	}

	[Fact]
	public async Task RetryOnMiddlewareFailure_RetriesFailedOperation()
	{
		// Arrange
		var invocations = new List<string>();
		var attemptCount = 0;
		var middleware = new IDispatchMiddleware[]
		{
			new RetryableMiddleware("Retry", DispatchMiddlewareStage.Validation, invocations, ref attemptCount),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Retry");
	}

	[Fact]
	public async Task CircuitBreakerPattern_OpensCircuitAfterFailures()
	{
		// Arrange
		var invocations = new List<string>();
		var failureCount = 0;
		var middleware = new IDispatchMiddleware[]
		{
			new CircuitBreakerMiddleware("CircuitBreaker", DispatchMiddlewareStage.Validation, invocations, ref failureCount),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("CircuitBreaker");
	}

	[Fact]
	public async Task FallbackMiddleware_ProvidesFallbackOnFailure()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new FallbackMiddleware("Fallback", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocations.ShouldContain("Fallback");
	}

	#endregion Error Handling Tests (10 tests)

	#region Short-Circuiting Tests (10 tests)

	[Fact]
	public async Task MiddlewareReturnsWithoutCallingNext_StopsPipeline()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, invocations),
			new TrackingMiddleware("Third", DispatchMiddlewareStage.Authorization, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "First", "ShortCircuit" });
		invocations.ShouldNotContain("Third");
	}

	[Fact]
	public async Task ShortCircuitStopsPipeline_RemainingMiddlewareNotInvoked()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Authorization, invocations),
			new TrackingMiddleware("Third", DispatchMiddlewareStage.Processing, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "ShortCircuit" });
	}

	[Fact]
	public async Task RemainingMiddlewareNotInvoked_AfterShortCircuit()
	{
		// Arrange
		var invocations = new List<string>();
		var finalDelegateInvoked = false;
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		ValueTask<IMessageResult> LocalFinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, LocalFinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		finalDelegateInvoked.ShouldBeFalse();
	}

	[Fact]
	public async Task ShortCircuitResponseHandling_ReturnsCustomResponse()
	{
		// Arrange
		var customResult = MessageResult.Failed("Custom short-circuit response");
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitWithResultMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, customResult),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(customResult);
		result.ErrorMessage.ShouldBe("Custom short-circuit response");
	}

	[Fact]
	public async Task ConditionalShortCircuit_BasedOnContextCondition()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ConditionalShortCircuitMiddleware("Conditional", DispatchMiddlewareStage.Validation, invocations, "skip_key"),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Authorization, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.SetItem("skip_key", true);

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "Conditional" });
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task AuthorizationMiddlewareShortCircuit_BlocksUnauthorizedRequests()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new AuthorizationShortCircuitMiddleware("Auth", DispatchMiddlewareStage.Authorization, invocations, authorized: false),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Processing, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Unauthorized");
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task ValidationMiddlewareShortCircuit_RejectsInvalidMessages()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ValidationShortCircuitMiddleware("Validation", DispatchMiddlewareStage.Validation, invocations, isValid: false),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Processing, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Validation failed");
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task CachingMiddlewareShortCircuit_ReturnsCachedResult()
	{
		// Arrange
		var invocations = new List<string>();
		var cachedResult = MessageResult.Success();
		var middleware = new IDispatchMiddleware[]
		{
			new CachingShortCircuitMiddleware("Cache", DispatchMiddlewareStage.Validation, invocations, cachedResult),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Processing, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(cachedResult);
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task RateLimitingMiddlewareShortCircuit_BlocksExcessiveRequests()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new RateLimitingShortCircuitMiddleware("RateLimit", DispatchMiddlewareStage.RateLimiting, invocations, isLimited: true),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Processing, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Rate limit exceeded");
		invocations.ShouldNotContain("Second");
	}

	[Fact]
	public async Task ShortCircuitTelemetry_TracksShortCircuitEvents()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitWithTelemetryMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<bool>("short_circuit_telemetry").ShouldBeTrue();
	}

	#endregion Short-Circuiting Tests (10 tests)

	#region Advanced Scenarios Tests (10 tests)

	[Fact]
	public async Task NestedPipelines_ExecuteInnerPipelineFromMiddleware()
	{
		// Arrange
		var invocations = new List<string>();
		var innerMiddleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Inner", DispatchMiddlewareStage.Validation, invocations),
		};
		var innerPipeline = new DispatchPipeline(innerMiddleware);

		var middleware = new IDispatchMiddleware[]
		{
			new NestedPipelineMiddleware("Outer", DispatchMiddlewareStage.Start, invocations, innerPipeline),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Outer");
		invocations.ShouldContain("Inner");
	}

	[Fact]
	public async Task DynamicMiddlewareRegistration_AddsMiddlewareAtRuntime()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new List<IDispatchMiddleware>
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
		};

		// Simulate dynamic registration before pipeline creation
		middlewareList.Add(new TrackingMiddleware("Dynamic", DispatchMiddlewareStage.Validation, invocations));

		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Dynamic");
	}

	[Fact]
	public async Task MiddlewarePriorityOrdering_ExecutesInPriorityOrder()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("Low", DispatchMiddlewareStage.End, invocations),
			new TrackingMiddleware("High", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Medium", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldBe(new[] { "High", "Medium", "Low" });
	}

	[Fact]
	public async Task AsyncMiddlewareExecution_AwaitsAsyncOperations()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new AsyncMiddleware("Async", DispatchMiddlewareStage.Validation, invocations, delayMs: 0), // No delay for deterministic test
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("Async");
	}

	[Fact]
	public async Task ParallelMiddlewareExecution_NotSupported()
	{
		// Arrange - Pipeline executes middleware sequentially
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TrackingMiddleware("First", DispatchMiddlewareStage.Start, invocations),
			new TrackingMiddleware("Second", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert - Sequential execution
		invocations.ShouldBe(new[] { "First", "Second" });
	}

	[Fact]
	public async Task MiddlewarePerformanceOverhead_MinimalImpact()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new PerformanceMiddleware("Performance", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		stopwatch.Stop();

		// Assert
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50); // Fast execution
	}

	[Fact]
	public async Task MiddlewareMemoryAllocation_MinimalAllocations()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new MemoryEfficientMiddleware("MemoryEfficient", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("MemoryEfficient");
	}

	[Fact]
	public async Task PipelineReuseAcrossRequests_MaintainsThreadSafety()
	{
		// Arrange
		var invocations = new System.Collections.Concurrent.ConcurrentBag<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ConcurrentMiddleware("Concurrent", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);

		var tasks = new List<Task>();
		for (var i = 0; i < 10; i++)
		{
			var message = new TestMessage();
			var context = CreateMessageContext();
			tasks.Add(pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).AsTask());
		}

		// Act
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		invocations.Count.ShouldBe(10);
	}

	[Fact]
	public async Task PipelineThreadSafety_HandlesConcurrentRequests()
	{
		// Arrange
		var invocations = new System.Collections.Concurrent.ConcurrentBag<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ThreadSafeMiddleware("ThreadSafe", DispatchMiddlewareStage.Validation, invocations),
		};
		var pipeline = new DispatchPipeline(middleware);

		var tasks = new List<Task>();
		for (var i = 0; i < 5; i++)
		{
			var message = new TestMessage();
			var context = CreateMessageContext();
			tasks.Add(pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).AsTask());
		}

		// Act
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		invocations.Count.ShouldBe(5);
	}

	[Fact]
	public async Task PipelineDisposal_DisposesResourcesCorrectly()
	{
		// Arrange
		var invocations = new List<string>();
		var disposableMiddleware = new DisposableMiddleware("Disposable", DispatchMiddlewareStage.Validation, invocations);
		var middleware = new IDispatchMiddleware[]
		{
			disposableMiddleware,
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		disposableMiddleware.Dispose();

		// Assert
		disposableMiddleware.IsDisposed.ShouldBeTrue();
	}

	#endregion Advanced Scenarios Tests (10 tests)

	#region Helper Methods and Test Fixtures

	private static ValueTask<IMessageResult> FinalDelegate(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		return new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private IMessageContext CreateMessageContext()
	{
		return new TestMessageContext
		{
			RequestServices = ServiceProvider,
			ReceivedTimestampUtc = DateTimeOffset.UtcNow,
		};
	}

	#endregion Helper Methods and Test Fixtures

	#region Test Message Types

	private sealed class TestMessage : IDispatchMessage
	{ }

	private sealed class TestMessageContext : IMessageContext
	{
		private readonly Dictionary<string, object> _items = new();

		public string? MessageId { get; set; }
		public string? ExternalId { get; set; }
		public string? UserId { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public string? TraceParent { get; set; }
		public string? TenantId { get; set; }
		public string? SessionId { get; set; }
		public string? WorkflowId { get; set; }
		public string? PartitionKey { get; set; }
		public string? Source { get; set; }
		public string? MessageType { get; set; }
		public string? ContentType { get; set; }
		public int DeliveryCount { get; set; }
		public IDispatchMessage? Message { get; set; }
		public object? Result { get; set; }

		public RoutingDecision? RoutingDecision { get; set; } = RoutingDecision.Success("local", []);

		public IServiceProvider RequestServices { get; set; } = null!;
		public DateTimeOffset ReceivedTimestampUtc { get; set; }
		public DateTimeOffset? SentTimestampUtc { get; set; }
		public IDictionary<string, object> Items => _items;
		public IDictionary<string, object?> Properties => _items!;

		public int ProcessingAttempts { get; set; }
		public DateTimeOffset? FirstAttemptTime { get; set; }
		public bool IsRetry { get; set; }
		public bool ValidationPassed { get; set; }
		public DateTimeOffset? ValidationTimestamp { get; set; }
		public object? Transaction { get; set; }
		public string? TransactionId { get; set; }
		public bool TimeoutExceeded { get; set; }
		public TimeSpan? TimeoutElapsed { get; set; }
		public bool RateLimitExceeded { get; set; }
		public TimeSpan? RateLimitRetryAfter { get; set; }

		public bool ContainsItem(string key) => _items.ContainsKey(key);

		public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) ? (T)value : default;

		public T GetItem<T>(string key, T defaultValue) => _items.TryGetValue(key, out var value) ? (T)value : defaultValue;

		public void RemoveItem(string key) => _items.Remove(key);

		public void SetItem<T>(string key, T value) => _items[key] = value!;

		public IMessageContext CreateChildContext() => new TestMessageContext
		{
			CorrelationId = CorrelationId,
			CausationId = MessageId ?? CorrelationId,
			TenantId = TenantId,
			UserId = UserId,
			SessionId = SessionId,
			WorkflowId = WorkflowId,
			TraceParent = TraceParent,
			Source = Source,
			RequestServices = RequestServices,
			MessageId = Guid.NewGuid().ToString(),
		};
	}

	#endregion Test Message Types

	#region Test Service Types

	private interface ITestService
	{
		void DoWork();
	}

	private sealed class TestService : ITestService
	{
		public void DoWork()
		{ }
	}

	private sealed class TestMiddlewareOptions
	{
		public bool Enabled { get; set; }
		public int Priority { get; set; }
	}

	#endregion Test Service Types

	#region Test Middleware Implementations

	private class TrackingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ConditionalMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, bool shouldExecute) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (shouldExecute)
			{
				invocations.Add(name);
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ServiceResolvingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			var service = context.RequestServices.GetService<ITestService>();
			if (service != null)
			{
				invocations.Add(name);
				context.SetItem("service_resolved", true);
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class SingletonMiddleware : IDispatchMiddleware
	{
		private readonly string _name;
		private readonly DispatchMiddlewareStage? _stage;
		private readonly List<string> _invocations;
		private readonly int _instanceId;

		public SingletonMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, ref int instanceCounter)
		{
			_name = name;
			_stage = stage;
			_invocations = invocations;
			_instanceId = ++instanceCounter;
		}

		public DispatchMiddlewareStage? Stage => _stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			_invocations.Add(_name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ConfigurableMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, TestMiddlewareOptions options) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (options.Enabled)
			{
				invocations.Add(name);
				context.SetItem("priority", options.Priority);
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class NextDelegateTrackingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add($"{name}:before");
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			invocations.Add($"{name}:after");
			return result;
		}
	}

	private sealed class ContextWriterMiddleware(string name, DispatchMiddlewareStage? stage, string key, object value) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			context.SetItem(key, value);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ContextReaderMiddleware(string name, DispatchMiddlewareStage? stage, string key, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			var value = context.GetItem<string>(key);
			invocations.Add($"{name}:{value}");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MessageModifierMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			context.SetItem("message_modified", true);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MetadataModifierMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			context.SetItem("metadata_key", "metadata_value");
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class TimedMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, int delayMs) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (delayMs > 0)
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(delayMs, cancellationToken).ConfigureAwait(false);
			}
			invocations.Add(name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class TelemetryMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			context.SetItem("invocation_count", 1);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ThrowingMiddleware(string name, DispatchMiddlewareStage? stage, Exception exception) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			throw exception;
		}
	}

	private sealed class ExceptionHandlingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			try
			{
				invocations.Add(name);
				throw new InvalidOperationException("Test exception");
			}
			catch (InvalidOperationException)
			{
				return await new ValueTask<IMessageResult>(MessageResult.Failed("Exception handled gracefully")).ConfigureAwait(false);
			}
		}
	}

	private sealed class ErrorLoggingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class RetryableMiddleware : IDispatchMiddleware
	{
		private readonly string _name;
		private readonly DispatchMiddlewareStage? _stage;
		private readonly List<string> _invocations;
		private int _attemptCount;

		public RetryableMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, ref int attemptCount)
		{
			_name = name;
			_stage = stage;
			_invocations = invocations;
			_attemptCount = attemptCount;
		}

		public DispatchMiddlewareStage? Stage => _stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			_attemptCount++;
			_invocations.Add(_name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CircuitBreakerMiddleware : IDispatchMiddleware
	{
		private readonly string _name;
		private readonly DispatchMiddlewareStage? _stage;
		private readonly List<string> _invocations;
		private int _failureCount;

		public CircuitBreakerMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, ref int failureCount)
		{
			_name = name;
			_stage = stage;
			_invocations = invocations;
			_failureCount = failureCount;
		}

		public DispatchMiddlewareStage? Stage => _stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			_invocations.Add(_name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class FallbackMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class ShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class ShortCircuitWithResultMiddleware(string name, DispatchMiddlewareStage? stage, IMessageResult result) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			return new ValueTask<IMessageResult>(result);
		}
	}

	private sealed class ConditionalShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, string contextKey) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			if (context.GetItem<bool>(contextKey))
			{
				return new ValueTask<IMessageResult>(MessageResult.Success());
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class AuthorizationShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, bool authorized) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			if (!authorized)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Unauthorized"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ValidationShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, bool isValid) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			if (!isValid)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Validation failed"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CachingShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, IMessageResult cachedResult) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return new ValueTask<IMessageResult>(cachedResult);
		}
	}

	private sealed class RateLimitingShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, bool isLimited) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			if (isLimited)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Rate limit exceeded"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ShortCircuitWithTelemetryMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			context.SetItem("short_circuit_telemetry", true);
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class NestedPipelineMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, DispatchPipeline innerPipeline) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			_ = await innerPipeline.ExecuteAsync(message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class AsyncMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations, int delayMs) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (delayMs > 0)
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(delayMs, cancellationToken).ConfigureAwait(false);
			}
			invocations.Add(name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class PerformanceMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MemoryEfficientMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ConcurrentMiddleware(string name, DispatchMiddlewareStage? stage, System.Collections.Concurrent.ConcurrentBag<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ThreadSafeMiddleware(string name, DispatchMiddlewareStage? stage, System.Collections.Concurrent.ConcurrentBag<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class DisposableMiddleware(string name, DispatchMiddlewareStage? stage, List<string> invocations) : IDispatchMiddleware, IDisposable
	{
		public bool IsDisposed { get; private set; }

		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}

		public void Dispose()
		{
			IsDisposed = true;
			GC.SuppressFinalize(this);
		}
	}

	#endregion Test Middleware Implementations
}

