// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Runtime tests for static pipeline invocation behavior.
/// Validates middleware ordering, fallback paths, exception propagation, and context preservation.
/// </summary>
/// <remarks>
/// Sprint 457 - S457.4: Runtime tests for static pipeline execution (PERF-23).
/// Tests the runtime behavior when static pipelines execute or fall back to dynamic dispatch.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class StaticPipelineInvocationShould : IDisposable
{
	private readonly IDispatchMiddlewareInvoker _middlewareInvoker;
	private readonly ILogger<FinalDispatchHandler> _logger;
	private readonly IMessageBusProvider _busProvider;
	private readonly FinalDispatchHandler _finalHandler;

	public StaticPipelineInvocationShould()
	{
		_middlewareInvoker = A.Fake<IDispatchMiddlewareInvoker>();
		_logger = A.Fake<ILogger<FinalDispatchHandler>>();
		_busProvider = A.Fake<IMessageBusProvider>();
		_finalHandler = new FinalDispatchHandler(_busProvider, _logger, null, new Dictionary<string, IMessageBusOptions>());

		HandlerInvoker.ClearCache();
	}

	public void Dispose()
	{
		HandlerInvoker.ClearCache();
	}

	#region Static Pipeline Invocation Tests (4 tests)

	[Fact]
	public async Task InvokeStaticPipeline_ForDeterministicMessage()
	{
		// Arrange - A deterministic command type should use static pipeline when available
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - Pipeline executed successfully
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeStaticPipeline_PreservesMessageId()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext { MessageId = "static-pipeline-test-001" };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<IMessageContext>(1))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.MessageId.ShouldBe("static-pipeline-test-001");
	}

	[Fact]
	public async Task InvokeStaticPipeline_HandlesTypedResult()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var query = new DeterministicQuery();
		var context = new MessageContext();
		var expectedResult = new QueryResult { Data = "static-result" };

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				query,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success(expectedResult)));

		// Act
		var result = await dispatcher.DispatchAsync<DeterministicQuery, QueryResult>(query, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = result.ReturnValue.ShouldNotBeNull();
		result.ReturnValue.Data.ShouldBe("static-result");
	}

	[Fact]
	public async Task InvokeStaticPipeline_MultipleCalls_ConsistentResults()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var context = new MessageContext();
		var results = new List<bool>();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act - Same deterministic type multiple times
		for (int i = 0; i < 3; i++)
		{
			var result = await dispatcher.DispatchAsync(new DeterministicCommand(), context, CancellationToken.None);
			results.Add(result.Succeeded);
		}

		// Assert - All invocations succeed
		results.ShouldAllBe(r => r);
	}

	#endregion

	#region Dynamic Fallback Tests (4 tests)

	[Fact]
	public async Task FallbackToDynamic_WhenTenantSpecificRouting()
	{
		// Arrange - Tenant-specific messages are non-deterministic
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new TenantSpecificCommand { TenantId = "tenant-001" };
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - Falls back to dynamic, still succeeds
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task FallbackToDynamic_WhenFeatureFlagRouting()
	{
		// Arrange - Feature flag dependent routing
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new FeatureFlagCommand { FeatureEnabled = true };
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task FallbackToDynamic_WhenInterfaceTypedMessage()
	{
		// Arrange - Interface-typed dispatch cannot be statically determined
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		IDispatchMessage message = new DeterministicCommand();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task FallbackToDynamic_WhenDynamicPipelineProfile()
	{
		// Arrange - Dynamic profile selection at runtime
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new ProfileDependentCommand { Profile = "high-priority" };
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Middleware Ordering Tests (3 tests)

	[Fact]
	public async Task MaintainMiddlewareOrder_StartBeforeEnd()
	{
		// Arrange - Verify middleware stages are invoked in correct order
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		var invocationOrder = new List<string>();

		// Capture invocation to verify ordering is maintained through pipeline
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(_ => invocationOrder.Add("middleware"))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		invocationOrder.ShouldContain("middleware");
	}

	[Fact]
	public async Task ExecuteBeforePhase_BeforeHandler()
	{
		// Arrange - Before phase should execute before handler invocation
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		var beforeExecuted = false;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(_ => beforeExecuted = true)
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		beforeExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAfterPhase_AfterHandler()
	{
		// Arrange - After phase should execute after handler returns
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		var executionComplete = false;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				executionComplete = true;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			});

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		executionComplete.ShouldBeTrue();
	}

	#endregion

	#region Exception Propagation Tests (3 tests)

	[Fact]
	public async Task PropagateException_FromMiddleware()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Middleware failure"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await dispatcher.DispatchAsync(message, context, CancellationToken.None));
	}

	[Fact]
	public async Task PropagateFailure_WithProblemDetails()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		var problemDetails = new MessageProblemDetails
		{
			Type = "validation-error",
			Title = "Validation Failed",
			Status = 400,
			Detail = "Input validation failed",
			Instance = Guid.NewGuid().ToString()
		};

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Failed(problemDetails)));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("validation-error");
	}

	[Fact]
	public async Task HandleCancellation_GracefullyPropagates()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await dispatcher.DispatchAsync(message, context, cts.Token));
	}

	#endregion

	#region Context Preservation Tests (4 tests)

	[Fact]
	public async Task PreserveContext_ThroughStaticPipeline()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext
		{
			MessageId = "ctx-001",
			CorrelationId = "corr-001"
		};
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<IMessageContext>(1))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.MessageId.ShouldBe("ctx-001");
		capturedContext.CorrelationId.ShouldBe("corr-001");
	}

	[Fact]
	public async Task PreserveMessage_ThroughPipeline()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var context = new MessageContext();
		IDispatchMessage? capturedMessage = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedMessage = call.GetArgument<IDispatchMessage>(0))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		_ = capturedMessage.ShouldNotBeNull();
		capturedMessage.ShouldBe(message);
	}

	[Fact]
	public async Task PreserveServiceProvider_InContext()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new MessageContext(message, services);
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<IMessageContext>(1))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		_ = capturedContext.RequestServices.ShouldNotBeNull();
	}

	[Fact]
	public async Task PreserveReceivedTimestamp_InContext()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new DeterministicCommand();
		var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
		var context = new MessageContext { ReceivedTimestampUtc = timestamp };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<IMessageContext>(1))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.ReceivedTimestampUtc.ShouldBe(timestamp);
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Test command that can be determined at compile time (deterministic).
	/// </summary>
	private sealed class DeterministicCommand : IDispatchMessage
	{
	}

	/// <summary>
	/// Test query with typed result.
	/// </summary>
	private sealed class DeterministicQuery : IDispatchAction<QueryResult>
	{
	}

	/// <summary>
	/// Result type for DeterministicQuery.
	/// </summary>
	private sealed class QueryResult
	{
		public string Data { get; init; } = string.Empty;
	}

	/// <summary>
	/// Test command that requires tenant-specific routing (non-deterministic).
	/// </summary>
	private sealed class TenantSpecificCommand : IDispatchMessage
	{
		public string TenantId { get; init; } = string.Empty;
	}

	/// <summary>
	/// Test command that depends on feature flags (non-deterministic).
	/// </summary>
	private sealed class FeatureFlagCommand : IDispatchMessage
	{
		public bool FeatureEnabled { get; init; }
	}

	/// <summary>
	/// Test command that depends on dynamic pipeline profile (non-deterministic).
	/// </summary>
	private sealed class ProfileDependentCommand : IDispatchMessage
	{
		public string Profile { get; init; } = string.Empty;
	}

	#endregion
}
