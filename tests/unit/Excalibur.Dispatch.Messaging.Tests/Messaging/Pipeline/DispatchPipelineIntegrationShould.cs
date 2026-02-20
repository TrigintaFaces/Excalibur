// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Integration tests verifying that the canonical dispatch pipeline composes middleware and reaches handler invokers.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchPipelineIntegrationShould
{
	public DispatchPipelineIntegrationShould() => TestActionHandler.Reset();

	[Fact]
	public async Task ApplyMiddlewareOnlyForApplicableMessageKinds()
	{
		// Arrange
		var actionMiddleware = new RecordingMiddleware(MessageKinds.Action, DispatchMiddlewareStage.Start);
		var eventMiddleware = new RecordingMiddleware(MessageKinds.Event, DispatchMiddlewareStage.Start);

		using var provider = BuildServiceProvider(services =>
		{
			_ = services.AddSingleton<IDispatchMiddleware>(actionMiddleware);
			_ = services.AddSingleton<IDispatchMiddleware>(eventMiddleware);
		});

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestAction();
		var context = new MessageContext(message, provider);

		// Act
		var result = await dispatcher.DispatchAsync<TestAction, string>(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue($"ErrorMessage: '{result.ErrorMessage}', ProblemDetails: '{result.ProblemDetails?.Detail}', ReturnValue: '{result.ReturnValue}', Type: '{result.GetType().Name}'");
		result.ReturnValue.ShouldBe(TestActionHandler.Response);
		actionMiddleware.InvocationCount.ShouldBe(1);
		eventMiddleware.InvocationCount.ShouldBe(0);
	}

	[Fact]
	public async Task InvokeRegisteredHandlerAndSurfaceResult()
	{
		// Arrange
		using var provider = BuildServiceProvider(services => _ = services.AddSingleton<IDispatchMiddleware>(new RecordingMiddleware(MessageKinds.All, DispatchMiddlewareStage.Start)));

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestAction();
		var context = new MessageContext(message, provider);

		// Act
		var result = await dispatcher.DispatchAsync<TestAction, string>(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(TestActionHandler.Response);
		TestActionHandler.InvocationCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateFailureFromMiddleware()
	{
		// Arrange
		var failure = new MessageProblemDetails
		{
			Type = "middleware-failure",
			Title = "Middleware Failed",
			Status = 500,
			Detail = "Pipeline short-circuit",
			Instance = Guid.NewGuid().ToString()
		};
		var failingMiddleware = new FailingMiddleware(failure);

		using var provider = BuildServiceProvider(services => _ = services.AddSingleton<IDispatchMiddleware>(failingMiddleware));

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestAction();
		var context = new MessageContext(message, provider);

		// Act
		var result = await dispatcher.DispatchAsync<TestAction, string>(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(failure.Type);
		TestActionHandler.InvocationCount.ShouldBe(0);
	}

	private static ServiceProvider BuildServiceProvider(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		configure?.Invoke(services);

		// Ensure the middleware applicability strategy is registered for proper middleware filtering.
		// This must be registered before AddDispatchPipeline() so middleware filtering works correctly
		// when custom middleware is added via the configure callback.
		_ = services.AddSingleton<IMiddlewareApplicabilityStrategy, DefaultMiddlewareApplicabilityStrategy>();

		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(typeof(DispatchPipelineIntegrationShould).Assembly);

		var provider = services.BuildServiceProvider();

		// Trigger LocalMessageBus registration by requesting the keyed IMessageBus
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

		return provider;
	}

	private sealed class RecordingMiddleware(MessageKinds applicableKinds, DispatchMiddlewareStage stage) : IDispatchMiddleware
	{
		private readonly MessageKinds _applicableKinds = applicableKinds;
		private readonly DispatchMiddlewareStage _stage = stage;
		private int _invocationCount;

		public int InvocationCount => _invocationCount;

		public MessageKinds ApplicableMessageKinds => _applicableKinds;

		public DispatchMiddlewareStage? Stage => _stage;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _invocationCount);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class FailingMiddleware(MessageProblemDetails problemDetails) : IDispatchMiddleware
	{
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken) =>
			new(MessageResult.Failed(problemDetails));
	}

	private sealed class TestAction : IDispatchAction<string>
	{
		public object Body => this;
		public Guid Id { get; init; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind { get; init; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();
	}

	private sealed class TestActionHandler : IActionHandler<TestAction, string>
	{
		public const string Response = "Handled";

		public static int InvocationCount { get; private set; }

		public static void Reset() => InvocationCount = 0;

		public Task<string> HandleAsync(TestAction action, CancellationToken cancellationToken)
		{
			InvocationCount++;
			return Task.FromResult(Response);
		}
	}
}
