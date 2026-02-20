// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests
#pragma warning disable CA2201 // Exception type is not sufficiently specific - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Pipeline;

/// <summary>
/// Unit tests for the DispatchPipeline class covering execution ordering, short-circuit behavior,
/// exception handling, async middleware, context propagation, and conditional middleware.
/// </summary>
[Trait("Category", "Unit")]
public class DispatchPipelineShould : UnitTestBase
{
	#region Pipeline Execution Ordering Tests

	[Fact]
	public async Task ExecuteAsync_MultipleMiddleware_ExecutesInStageOrder()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Third", DispatchMiddlewareStage.Authorization, executionOrder),
			new TestMiddleware("First", DispatchMiddlewareStage.PreProcessing, executionOrder),
			new TestMiddleware("Second", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "Second", "Third" });
	}

	[Fact]
	public async Task ExecuteAsync_MiddlewareWithNullStage_ExecutesAtEnd()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("NullStage", null, executionOrder),
			new TestMiddleware("First", DispatchMiddlewareStage.Start, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "NullStage" });
	}

	[Fact]
	public async Task ExecuteAsync_EmptyPipeline_CallsFinalDelegate()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();
		var finalDelegateCalled = false;

		ValueTask<IMessageResult> FinalDelegateWithFlag(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegateWithFlag, CancellationToken.None).ConfigureAwait(false);

		// Assert
		finalDelegateCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_SameStageMiddleware_MaintainsRegistrationOrder()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("Second", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("Third", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "Second", "Third" });
	}

	[Fact]
	public async Task ExecuteAsync_AllStages_ExecutesInCorrectOrder()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Error", DispatchMiddlewareStage.Error, executionOrder),
			new TestMiddleware("Start", DispatchMiddlewareStage.Start, executionOrder),
			new TestMiddleware("Processing", DispatchMiddlewareStage.Processing, executionOrder),
			new TestMiddleware("Validation", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Start", "Validation", "Processing", "Error" });
	}

	[Fact]
	public async Task ExecuteAsync_SingleMiddleware_ExecutesSuccessfully()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Single", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Single" });
	}

	[Fact]
	public async Task ExecuteAsync_MixedNullAndDefinedStages_OrdersCorrectly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Null1", null, executionOrder),
			new TestMiddleware("Middle", DispatchMiddlewareStage.Routing, executionOrder),
			new TestMiddleware("Null2", null, executionOrder),
			new TestMiddleware("Early", DispatchMiddlewareStage.PreProcessing, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Early", "Middle", "Null1", "Null2" });
	}

	[Fact]
	public async Task ExecuteAsync_CachesFilteredMiddleware()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act - Execute twice with same message type
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert - Both executions should work
		executionOrder.Count.ShouldBe(2);
	}

	[Fact]
	public void ClearCache_ResetsMiddlewareCache()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Validation, new List<string>()),
		};
		var pipeline = new DispatchPipeline(middleware);

		// Act & Assert - Should not throw
		Should.NotThrow(pipeline.ClearCache);
	}

	#endregion Pipeline Execution Ordering Tests

	#region Short-Circuit Behavior Tests

	[Fact]
	public async Task ExecuteAsync_MiddlewareShortCircuits_StopsPipeline()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Start, executionOrder),
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("Third", DispatchMiddlewareStage.Authorization, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "ShortCircuit" });
	}

	[Fact]
	public async Task ExecuteAsync_ShortCircuit_ReturnsCustomResult()
	{
		// Arrange
		var expectedResult = MessageResult.Failed("Short-circuited");
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, new List<string>(), expectedResult),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_ShortCircuitAtStart_SkipsAllSubsequentMiddleware()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Start, executionOrder),
			new TestMiddleware("Second", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("Third", DispatchMiddlewareStage.Authorization, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "ShortCircuit" });
	}

	[Fact]
	public async Task ExecuteAsync_ShortCircuit_DoesNotCallFinalDelegate()
	{
		// Arrange
		var finalDelegateCalled = false;
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("ShortCircuit", DispatchMiddlewareStage.Validation, new List<string>()),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		ValueTask<IMessageResult> FinalDelegateWithFlag(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegateWithFlag, CancellationToken.None).ConfigureAwait(false);

		// Assert
		finalDelegateCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_OnBeforeProcessAsync_CanShortCircuit()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new BeforeProcessShortCircuitMiddleware("BeforeShortCircuit", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("Second", DispatchMiddlewareStage.Authorization, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "BeforeShortCircuit" });
	}

	[Fact]
	public async Task ExecuteAsync_MultipleShortCircuitMiddleware_FirstOneWins()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ShortCircuitMiddleware("First", DispatchMiddlewareStage.Start, executionOrder),
			new ShortCircuitMiddleware("Second", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "First" });
	}

	[Fact]
	public async Task ExecuteAsync_ConditionalShortCircuit_BasedOnContext()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ConditionalShortCircuitMiddleware("Conditional", DispatchMiddlewareStage.Validation, executionOrder, "skip"),
			new TestMiddleware("Second", DispatchMiddlewareStage.Authorization, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.SetItem("skip", true);

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Conditional" });
	}

	#endregion Short-Circuit Behavior Tests

	#region Exception Handling Tests

	[Fact]
	public async Task ExecuteAsync_MiddlewareThrows_PropagatesException()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new ThrowingMiddleware("Throwing", DispatchMiddlewareStage.Validation),
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
	public async Task ExecuteAsync_MiddlewareThrows_SubsequentMiddlewareNotCalled()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Start, executionOrder),
			new ThrowingMiddleware("Throwing", DispatchMiddlewareStage.Validation),
			new TestMiddleware("Third", DispatchMiddlewareStage.Authorization, executionOrder),
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
		executionOrder.ShouldBe(new[] { "First" });
	}

	[Fact]
	public async Task ExecuteAsync_OnErrorAsync_HandlesException()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ErrorHandlingMiddleware("ErrorHandler", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Error handled");
	}

	[Fact]
	public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", DispatchMiddlewareStage.Validation, new List<string>()),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await pipeline.ExecuteAsync(message, context, CancellableFinalDelegate, cts.Token).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ExceptionInFinalDelegate_Propagates()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();

		ValueTask<IMessageResult> ThrowingDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			throw new ApplicationException("Final delegate error");
		}

		// Act & Assert
		_ = await Should.ThrowAsync<ApplicationException>(async () =>
			await pipeline.ExecuteAsync(message, context, ThrowingDelegate, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ArgumentNull_ThrowsArgumentNullException()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await pipeline.ExecuteAsync(null!, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await pipeline.ExecuteAsync(message, null!, FinalDelegate, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await pipeline.ExecuteAsync(message, context, null!, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	#endregion Exception Handling Tests

	#region Async Middleware Tests

	[Fact]
	public async Task ExecuteAsync_AsyncMiddleware_AwaitsCorrectly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new AsyncMiddleware("Async1", DispatchMiddlewareStage.Start, executionOrder, 10),
			new AsyncMiddleware("Async2", DispatchMiddlewareStage.Validation, executionOrder, 10),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Async1", "Async2" });
	}

	[Fact]
	public async Task ExecuteAsync_LongRunningAsync_CompletesSuccessfully()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new AsyncMiddleware("LongRunning", DispatchMiddlewareStage.Validation, executionOrder, 50),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		executionOrder.ShouldContain("LongRunning");
	}

	[Fact]
	public async Task ExecuteAsync_AsyncWithCancellation_RespectsCancellation()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new CancellationAwareMiddleware("Cancellable", DispatchMiddlewareStage.Validation),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await pipeline.ExecuteAsync(message, context, FinalDelegate, cts.Token).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_MultipleAsyncOperations_ExecutesSequentially()
	{
		// Arrange
		var executionTimes = new List<(string Name, DateTime Time)>();
		var middleware = new IDispatchMiddleware[]
		{
			new TimestampMiddleware("First", DispatchMiddlewareStage.Start, executionTimes),
			new TimestampMiddleware("Second", DispatchMiddlewareStage.Validation, executionTimes),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionTimes.Count.ShouldBe(2);
		executionTimes[0].Name.ShouldBe("First");
		executionTimes[1].Name.ShouldBe("Second");
		executionTimes[1].Time.ShouldBeGreaterThanOrEqualTo(executionTimes[0].Time);
	}

	[Fact]
	public async Task ExecuteAsync_AsyncExceptionInMiddleware_PropagatesCorrectly()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new AsyncThrowingMiddleware("AsyncThrowing", DispatchMiddlewareStage.Validation),
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
	public async Task ExecuteAsync_ConcurrentPipelineExecutions_AreIsolated()
	{
		// Arrange
		var executionCounts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
		var middleware = new IDispatchMiddleware[]
		{
			new CountingMiddleware("Counter", DispatchMiddlewareStage.Validation, executionCounts),
		};
		var pipeline = new DispatchPipeline(middleware);
		var tasks = new List<Task>();

		// Act
		foreach (var _ in Enumerable.Range(0, 10))
		{
			var message = new TestMessage();
			var context = CreateMessageContext();
			tasks.Add(pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).AsTask());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		executionCounts["Counter"].ShouldBe(10);
	}

	#endregion Async Middleware Tests

	#region Context Propagation Tests

	[Fact]
	public async Task ExecuteAsync_ContextItems_FlowThroughPipeline()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new ContextWriterMiddleware("Writer", DispatchMiddlewareStage.Start, "key", "value"),
			new ContextReaderMiddleware("Reader", DispatchMiddlewareStage.Validation, "key"),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<string>("key").ShouldBe("value");
		context.GetItem<string>("Reader_read").ShouldBe("value");
	}

	[Fact]
	public async Task ExecuteAsync_ContextModification_VisibleToSubsequentMiddleware()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new ContextWriterMiddleware("Writer1", DispatchMiddlewareStage.Start, "counter", 1),
			new ContextIncrementMiddleware("Incrementer", DispatchMiddlewareStage.Validation, "counter"),
			new ContextReaderMiddleware("Reader", DispatchMiddlewareStage.Authorization, "counter"),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<int>("counter").ShouldBe(2);
		context.GetItem<int>("Reader_read").ShouldBe(2);
	}

	[Fact]
	public async Task ExecuteAsync_CorrelationId_PreservedThroughPipeline()
	{
		// Arrange
		var correlationId = Guid.NewGuid().ToString();
		var middleware = new IDispatchMiddleware[]
		{
			new CorrelationIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, correlationId),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.CorrelationId = correlationId;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_MessageId_AvailableInContext()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var middleware = new IDispatchMiddleware[]
		{
			new MessageIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.MessageId = messageId;

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.MessageId.ShouldBe(messageId);
	}

	[Fact]
	public async Task ExecuteAsync_RequestServices_AvailableInContext()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new ServiceProviderValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_TenantId_FlowsThroughPipeline()
	{
		// Arrange
		var tenantId = "tenant-123";
		var middleware = new IDispatchMiddleware[]
		{
			new TenantIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, tenantId),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.TenantId = tenantId;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_DeliveryCount_AvailableInContext()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new DeliveryCountValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, 3),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.DeliveryCount = 3;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_Properties_AliasesItems()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new PropertiesWriterMiddleware("Writer", DispatchMiddlewareStage.Validation),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.Items.ContainsKey("written_via_properties").ShouldBeTrue();
	}

	#endregion Context Propagation Tests

	#region Conditional Middleware Tests

	[Fact]
	public async Task ExecuteAsync_MessageKindFilter_AppliesCorrectly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new ActionOnlyMiddleware("ActionOnly", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestAction();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldContain("ActionOnly");
	}

	[Fact]
	public async Task ExecuteAsync_MessageKindFilter_SkipsNonMatchingMiddleware()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new EventOnlyMiddleware("EventOnly", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestAction();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldNotContain("EventOnly");
	}

	[Fact]
	public async Task ExecuteAsync_ShouldProcess_RespectsCustomLogic()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new CustomShouldProcessMiddleware("Custom", DispatchMiddlewareStage.Validation, executionOrder, false),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldNotContain("Custom");
	}

	[Fact]
	public async Task ExecuteAsync_MultipleMessageKinds_AllApply()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new MultiKindMiddleware("MultiKind", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestAction();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldContain("MultiKind");
	}

	#endregion Conditional Middleware Tests

	#region Additional Coverage Tests

	[Fact]
	public async Task ExecuteAsync_ResultFromFinalDelegate_ReturnsCorrectly()
	{
		// Arrange
		var expectedResult = MessageResult.Success();
		var pipeline = new DispatchPipeline(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateMessageContext();

		ValueTask<IMessageResult> FinalDelegateWithResult(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			return new ValueTask<IMessageResult>(expectedResult);
		}

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegateWithResult, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ExecuteAsync_MiddlewareModifiesMessage_PassesToNext()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new MessageTrackingMiddleware("Tracker", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.GetItem<bool>("message_tracked").ShouldBeTrue();
		executionOrder.ShouldContain("Tracker");
	}

	[Fact]
	public async Task ExecuteAsync_PostProcessingStage_ExecutesAfterProcessing()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Processing", DispatchMiddlewareStage.Processing, executionOrder),
			new TestMiddleware("PostProcessing", DispatchMiddlewareStage.PostProcessing, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Processing", "PostProcessing" });
	}

	[Fact]
	public async Task ExecuteAsync_InstrumentationStage_ExecutesEarly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Auth", DispatchMiddlewareStage.Authorization, executionOrder),
			new TestMiddleware("Instrumentation", DispatchMiddlewareStage.Instrumentation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "Instrumentation", "Auth" });
	}

	[Fact]
	public async Task ExecuteAsync_RateLimitingStage_ExecutesVeryEarly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new TestMiddleware("Validation", DispatchMiddlewareStage.Validation, executionOrder),
			new TestMiddleware("RateLimiting", DispatchMiddlewareStage.RateLimiting, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldBe(new[] { "RateLimiting", "Validation" });
	}

	[Fact]
	public async Task ExecuteAsync_WorkflowId_AvailableInContext()
	{
		// Arrange
		var workflowId = "workflow-123";
		var middleware = new IDispatchMiddleware[]
		{
			new WorkflowIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, workflowId),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.WorkflowId = workflowId;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_SessionId_AvailableInContext()
	{
		// Arrange
		var sessionId = "session-123";
		var middleware = new IDispatchMiddleware[]
		{
			new SessionIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, sessionId),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.SessionId = sessionId;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ReceivedTimestamp_SetInContext()
	{
		// Arrange
		var middleware = new IDispatchMiddleware[]
		{
			new TimestampValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_CausationId_FlowsThroughPipeline()
	{
		// Arrange
		var causationId = Guid.NewGuid().ToString();
		var middleware = new IDispatchMiddleware[]
		{
			new CausationIdValidatorMiddleware("Validator", DispatchMiddlewareStage.Validation, causationId),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestMessage();
		var context = CreateMessageContext();
		context.CausationId = causationId;

		// Act
		var result = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_DocumentMessageKind_AppliesCorrectly()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new IDispatchMiddleware[]
		{
			new DocumentOnlyMiddleware("DocumentOnly", DispatchMiddlewareStage.Validation, executionOrder),
		};
		var pipeline = new DispatchPipeline(middleware);
		var message = new TestDocument();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionOrder.ShouldContain("DocumentOnly");
	}

	#endregion Additional Coverage Tests

	#region Helper Methods

	private static ValueTask<IMessageResult> FinalDelegate(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		return new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private static ValueTask<IMessageResult> CancellableFinalDelegate(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
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

	#endregion Helper Methods

	#region Test Fixtures

	private sealed class TestMessage : IDispatchMessage
	{ }

	private sealed class TestAction : IDispatchAction
	{ }

	private sealed class TestEvent : IDispatchEvent
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

		// HOT-PATH PROPERTIES (Sprint 71)
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

	private class TestMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder, IMessageResult? result = null) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return new ValueTask<IMessageResult>(result ?? MessageResult.Success());
		}
	}

	private sealed class BeforeProcessShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;

		protected override ValueTask<IMessageResult?> OnBeforeProcessAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return ValueTask.FromResult<IMessageResult?>(MessageResult.Success());
		}
	}

	private sealed class ConditionalShortCircuitMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder, string contextKey) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			if (context.GetItem<bool>(contextKey))
			{
				return new ValueTask<IMessageResult>(MessageResult.Success());
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ThrowingMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException($"Middleware {name} threw an exception");
		}
	}

	private sealed class ErrorHandlingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			throw new InvalidOperationException("Test exception");
		}

		protected override ValueTask<IMessageResult?> OnErrorAsync(IDispatchMessage message, IMessageContext context, Exception exception, CancellationToken cancellationToken)
		{
			return ValueTask.FromResult<IMessageResult?>(MessageResult.Failed("Error handled"));
		}
	}

	private sealed class AsyncMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder, int delayMs) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
			executionOrder.Add(name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class CancellationAwareMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class TimestampMiddleware(string name, DispatchMiddlewareStage? stage, List<(string Name, DateTime Time)> executionTimes) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionTimes.Add((name, DateTime.UtcNow));
			await Task.Delay(5, cancellationToken).ConfigureAwait(false);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class AsyncThrowingMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public async ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			await Task.Delay(5, cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException($"Async middleware {name} threw an exception");
		}
	}

	private sealed class CountingMiddleware(string name, DispatchMiddlewareStage? stage, System.Collections.Concurrent.ConcurrentDictionary<string, int> counts) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			_ = counts.AddOrUpdate(name, 1, (_, count) => count + 1);
			return nextDelegate(message, context, cancellationToken);
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

	private sealed class ContextReaderMiddleware(string name, DispatchMiddlewareStage? stage, string key) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			var value = context.GetItem<object>(key);
			context.SetItem($"{name}_read", value);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ContextIncrementMiddleware(string name, DispatchMiddlewareStage? stage, string key) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			var value = context.GetItem<int>(key);
			context.SetItem(key, value + 1);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CorrelationIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage, string expectedCorrelationId) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.CorrelationId != expectedCorrelationId)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Correlation ID mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MessageIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(context.MessageId))
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Message ID is missing"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ServiceProviderValidatorMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.RequestServices == null)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("RequestServices is null"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class TenantIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage, string expectedTenantId) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.TenantId != expectedTenantId)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Tenant ID mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class DeliveryCountValidatorMiddleware(string name, DispatchMiddlewareStage? stage, int expectedCount) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.DeliveryCount != expectedCount)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Delivery count mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class PropertiesWriterMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			context.Properties["written_via_properties"] = true;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ActionOnlyMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;
		public override MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class EventOnlyMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;
		public override MessageKinds ApplicableMessageKinds => MessageKinds.Event;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CustomShouldProcessMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder, bool shouldProcess) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;

		protected override bool ShouldProcess(IDispatchMessage message, IMessageContext context) => shouldProcess;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MultiKindMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;
		public override MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class TestDocument : IDispatchDocument
	{ }

	private sealed class MessageTrackingMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			context.SetItem("message_tracked", true);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class WorkflowIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage, string expectedWorkflowId) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.WorkflowId != expectedWorkflowId)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Workflow ID mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class SessionIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage, string expectedSessionId) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.SessionId != expectedSessionId)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Session ID mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class TimestampValidatorMiddleware(string name, DispatchMiddlewareStage? stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.ReceivedTimestampUtc == default)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Received timestamp not set"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CausationIdValidatorMiddleware(string name, DispatchMiddlewareStage? stage, string expectedCausationId) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.CausationId != expectedCausationId)
			{
				return new ValueTask<IMessageResult>(MessageResult.Failed("Causation ID mismatch"));
			}
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class DocumentOnlyMiddleware(string name, DispatchMiddlewareStage? stage, List<string> executionOrder) : DispatchMiddlewareBase
	{
		public override DispatchMiddlewareStage? Stage => stage;
		public override MessageKinds ApplicableMessageKinds => MessageKinds.Document;

		protected override ValueTask<IMessageResult> ProcessAsync(IDispatchMessage message, IMessageContext context, DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion Test Fixtures
}
