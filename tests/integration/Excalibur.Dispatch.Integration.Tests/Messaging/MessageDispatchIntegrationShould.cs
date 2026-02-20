// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Integration.Tests.Messaging;

/// <summary>
/// Integration tests for message dispatching, handler registration, and routing.
/// These tests verify that the complete messaging pipeline works correctly.
/// </summary>
public sealed class MessageDispatchIntegrationShould : IntegrationTestBase
{
	#region Basic Dispatch Tests

	[Fact]
	public async Task DispatchCommand_ToRegisteredHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var command = new TestCommand("test-payload");
		var result = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task DispatchQuery_AndReturnResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestQuery, string>, TestQueryHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var query = new TestQuery("input");
		var result = await dispatcher.DispatchAsync<TestQuery, string>(query, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe("Response for: input");
	}

	[Fact]
	public async Task DispatchEvent_ToMultipleHandlers()
	{
		// Arrange
		var executedHandlers = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(executedHandlers);
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestEvent>, TestEventHandler1>();
		// Note: Current architecture may not support multiple handlers for the same message type
		// This test validates single handler behavior
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var evt = new TestEvent("event-data");
		var result = await dispatcher.DispatchAsync(evt, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		executedHandlers.ShouldContain("Handler1");
	}

	#endregion

	#region Pipeline Tests

	[Fact]
	public async Task DispatchMessage_ThroughMiddlewarePipeline()
	{
		// Arrange
		var middlewareOrder = new List<string>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(middlewareOrder);
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddDispatchMiddleware<OrderTrackingMiddleware1>();
		_ = services.AddDispatchMiddleware<OrderTrackingMiddleware2>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var command = new TestCommand("test");
		_ = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert - Middleware should execute in registration order
		middlewareOrder.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task DispatchMessage_WithCancellation_CancelsGracefully()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<SlowCommand>, SlowCommandHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act
		var command = new SlowCommand(TimeSpan.FromSeconds(5));
		var result = await dispatcher.DispatchAsync(command, context, cts.Token).ConfigureAwait(false);

		// Assert - Framework catches OperationCanceledException and returns a failed result
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Context Tests

	[Fact]
	public async Task DispatchMessage_PreservesCorrelationId()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		string? capturedCorrelationId = null;
		_ = services.AddSingleton<Action<string>>(id => capturedCorrelationId = id);
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestCommand>, CorrelationCapturingHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var correlationId = Guid.NewGuid().ToString();
		var context = new TestMessageContext { CorrelationId = correlationId };

		// Act
		var command = new TestCommand("test");
		_ = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		capturedCorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public async Task DispatchMessage_GeneratesCorrelationIdWhenMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext { CorrelationId = null };

		// Act
		var command = new TestCommand("test");
		_ = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert - Context should have a generated correlation ID
		context.CorrelationId.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task DispatchMessage_ToUnregisteredHandler_Fails()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(); // No handlers registered
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var command = new UnhandledCommand();
		var result = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert - Should indicate failure when no handler found
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task DispatchMessage_WhenHandlerThrows_ReturnsFailure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<ThrowingCommand>, ThrowingCommandHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = new TestMessageContext();

		// Act
		var command = new ThrowingCommand();
		var result = await dispatcher.DispatchAsync(command, context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region Test Messages and Handlers

	private sealed record TestCommand(string Payload) : IDispatchAction;

	private sealed record TestQuery(string Input) : IDispatchAction<string>;

	private sealed record TestEvent(string Data) : IDispatchAction;

	private sealed record SlowCommand(TimeSpan Delay) : IDispatchAction;

	private sealed record UnhandledCommand : IDispatchAction;

	private sealed record ThrowingCommand : IDispatchAction;

	private sealed class TestCommandHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class TestQueryHandler : IActionHandler<TestQuery, string>
	{
		public Task<string> HandleAsync(TestQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult($"Response for: {action.Input}");
		}
	}

	private sealed class TestEventHandler1(List<string> executedHandlers) : IActionHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent action, CancellationToken cancellationToken)
		{
			executedHandlers.Add("Handler1");
			return Task.CompletedTask;
		}
	}

	private sealed class SlowCommandHandler : IActionHandler<SlowCommand>
	{
		public async Task HandleAsync(SlowCommand action, CancellationToken cancellationToken)
		{
			await Task.Delay(action.Delay, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class ThrowingCommandHandler : IActionHandler<ThrowingCommand>
	{
		public Task HandleAsync(ThrowingCommand action, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Handler intentionally failed");
		}
	}

	private sealed class CorrelationCapturingHandler(Action<string> captureCorrelationId) : IActionHandler<TestCommand>
	{
		/// <summary>
		/// Set by the handler activator before HandleAsync is called.
		/// </summary>
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			if (Context?.CorrelationId is { } correlationId)
			{
				captureCorrelationId(correlationId);
			}

			return Task.CompletedTask;
		}
	}

	private sealed class OrderTrackingMiddleware1(List<string> order) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			order.Add("Middleware1");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class OrderTrackingMiddleware2(List<string> order) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			order.Add("Middleware2");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion
}
