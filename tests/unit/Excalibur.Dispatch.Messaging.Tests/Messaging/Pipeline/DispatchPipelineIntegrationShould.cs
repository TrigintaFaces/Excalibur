// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Integration tests verifying that the canonical dispatch pipeline composes middleware and reaches handler invokers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
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

	[Fact]
	public void ProfileIsCompatible_OnlyForConfiguredMessageKinds()
	{
		// Arrange -- profile configured for Actions only
		var profile = new PipelineProfileBuilder("ActionOnly", "Actions pipeline")
			.ForMessageKinds(MessageKinds.Action)
			.UseMiddleware<RecordingMiddleware>()
			.Build();

		var actionMessage = new TestAction();
		var eventMessage = new TestEvent();

		// Act & Assert
		profile.IsCompatible(actionMessage).ShouldBeTrue("Profile for Actions should be compatible with Action message");
		profile.IsCompatible(eventMessage).ShouldBeFalse("Profile for Actions should NOT be compatible with Event message");
	}

	[Fact]
	public void ProfileIsCompatible_ForAllKinds_WhenNotRestricted()
	{
		// Arrange -- profile with no ForMessageKinds restriction (defaults to All)
		var profile = new PipelineProfileBuilder("AllKinds", "Unrestricted pipeline")
			.UseMiddleware<RecordingMiddleware>()
			.Build();

		var actionMessage = new TestAction();
		var eventMessage = new TestEvent();

		// Act & Assert
		profile.IsCompatible(actionMessage).ShouldBeTrue();
		profile.IsCompatible(eventMessage).ShouldBeTrue();
	}

	[Fact]
	public void RegisterProfile_MakesProfileAvailableViaBuilder()
	{
		// Arrange
		var profile = new PipelineProfileBuilder("TestProfile", "Test pipeline for actions")
			.ForMessageKinds(MessageKinds.Action)
			.UseMiddleware<RecordingMiddleware>()
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.RegisterProfile(profile);
			_ = dispatch.AddHandlersFromAssembly(typeof(DispatchPipelineIntegrationShould).Assembly);
		});

		using var provider = services.BuildServiceProvider();

		// Assert -- profile registered, dispatcher resolvable
		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull();
	}

	[Fact]
	public async Task ApplyMiddlewareOnlyForApplicableMessageKinds_UsingEvents()
	{
		// Arrange -- complementary test: dispatch Event and verify Event middleware runs but Action middleware doesn't
		var actionMiddleware = new RecordingMiddleware(MessageKinds.Action, DispatchMiddlewareStage.Start);
		var eventMiddleware = new RecordingMiddleware(MessageKinds.Event, DispatchMiddlewareStage.Start);

		using var provider = BuildServiceProvider(services =>
		{
			_ = services.AddSingleton<IDispatchMiddleware>(actionMiddleware);
			_ = services.AddSingleton<IDispatchMiddleware>(eventMiddleware);
		});

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestEvent();
		var context = new MessageContext(message, provider);

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert -- event middleware runs, action middleware does NOT
		eventMiddleware.InvocationCount.ShouldBe(1, "Event middleware should run for Event messages");
		actionMiddleware.InvocationCount.ShouldBe(0, "Action middleware should NOT run for Event messages");
	}

	[Fact]
	public async Task RouteActionThroughGlobalAndActionPipeline_NotEventPipeline()
	{
		// Arrange -- mirrors the canonical ConfigurePipeline pattern:
		//   dispatch.UseMiddleware<LoggingMiddleware>();           // global
		//   dispatch.ConfigurePipeline("Actions", p => p
		//       .ForMessageKinds(MessageKinds.Action)
		//       .Use<ValidationMiddleware>());
		//   dispatch.ConfigurePipeline("Events", p => p
		//       .ForMessageKinds(MessageKinds.Event)
		//       .Use<EventAuditMiddleware>());

		using var provider = BuildServiceProvider(services =>
		{
			_ = services.AddSingleton<IDispatchMiddleware>(GlobalLoggingMiddleware.Instance);
			_ = services.AddSingleton<IDispatchMiddleware>(ActionValidationMiddleware.Instance);
			_ = services.AddSingleton<IDispatchMiddleware>(EventAuditMiddleware.Instance);
		});

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestAction();
		var context = new MessageContext(message, provider);

		GlobalLoggingMiddleware.Instance.Reset();
		ActionValidationMiddleware.Instance.Reset();
		EventAuditMiddleware.Instance.Reset();

		// Act -- dispatch an Action
		var result = await dispatcher.DispatchAsync<TestAction, string>(message, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		GlobalLoggingMiddleware.Instance.InvocationCount.ShouldBe(1,
			"Global middleware should run for Action messages");
		ActionValidationMiddleware.Instance.InvocationCount.ShouldBe(1,
			"Action pipeline middleware should run for Action messages");
		EventAuditMiddleware.Instance.InvocationCount.ShouldBe(0,
			"Event pipeline middleware should NOT run for Action messages");
	}

	[Fact]
	public async Task RouteEventThroughGlobalAndEventPipeline_NotActionPipeline()
	{
		// Arrange -- same setup, but dispatch an Event instead
		using var provider = BuildServiceProvider(services =>
		{
			_ = services.AddSingleton<IDispatchMiddleware>(GlobalLoggingMiddleware.Instance);
			_ = services.AddSingleton<IDispatchMiddleware>(ActionValidationMiddleware.Instance);
			_ = services.AddSingleton<IDispatchMiddleware>(EventAuditMiddleware.Instance);
		});

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var message = new TestEvent();
		var context = new MessageContext(message, provider);

		GlobalLoggingMiddleware.Instance.Reset();
		ActionValidationMiddleware.Instance.Reset();
		EventAuditMiddleware.Instance.Reset();

		// Act -- dispatch an Event
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		GlobalLoggingMiddleware.Instance.InvocationCount.ShouldBe(1,
			"Global middleware should run for Event messages");
		EventAuditMiddleware.Instance.InvocationCount.ShouldBe(1,
			"Event pipeline middleware should run for Event messages");
		ActionValidationMiddleware.Instance.InvocationCount.ShouldBe(0,
			"Action pipeline middleware should NOT run for Event messages");
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

	private sealed class TestEvent : IDispatchEvent
	{
		public object Body => this;
		public Guid Id { get; init; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind { get; init; } = MessageKinds.Event;
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

	/// <summary>
	/// Global middleware -- runs for ALL message kinds (like LoggingMiddleware in consumer code).
	/// Configured via: dispatch.UseMiddleware&lt;LoggingMiddleware&gt;()
	/// </summary>
	private sealed class GlobalLoggingMiddleware : IDispatchMiddleware
	{
		public static readonly GlobalLoggingMiddleware Instance = new();
		private int _invocationCount;

		public int InvocationCount => _invocationCount;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

		public void Reset() => Interlocked.Exchange(ref _invocationCount, 0);

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate next, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _invocationCount);
			return next(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Action-only middleware -- runs only for Action messages (like ValidationMiddleware).
	/// Configured via: dispatch.ConfigurePipeline("Actions", p =&gt; p.ForMessageKinds(Action).Use&lt;T&gt;())
	/// </summary>
	private sealed class ActionValidationMiddleware : IDispatchMiddleware
	{
		public static readonly ActionValidationMiddleware Instance = new();
		private int _invocationCount;

		public int InvocationCount => _invocationCount;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public void Reset() => Interlocked.Exchange(ref _invocationCount, 0);

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate next, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _invocationCount);
			return next(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Event-only middleware -- runs only for Event messages (like EventAuditMiddleware).
	/// Configured via: dispatch.ConfigurePipeline("Events", p =&gt; p.ForMessageKinds(Event).Use&lt;T&gt;())
	/// </summary>
	private sealed class EventAuditMiddleware : IDispatchMiddleware
	{
		public static readonly EventAuditMiddleware Instance = new();
		private int _invocationCount;

		public int InvocationCount => _invocationCount;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Event;
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

		public void Reset() => Interlocked.Exchange(ref _invocationCount, 0);

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate next, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _invocationCount);
			return next(message, context, cancellationToken);
		}
	}
}
