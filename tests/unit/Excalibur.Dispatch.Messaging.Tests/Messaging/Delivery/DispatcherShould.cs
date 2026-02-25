// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA2012: FakeItEasy .Returns(ValueTask.FromResult(...)) is test setup plumbing.
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
public sealed class DispatcherShould
{
	private readonly IDispatchMiddlewareInvoker _middlewareInvoker = A.Fake<IDispatchMiddlewareInvoker>();
	private readonly IMessageBusProvider _busProvider = A.Fake<IMessageBusProvider>();
	private readonly ILogger<FinalDispatchHandler> _finalLogger = A.Fake<ILogger<FinalDispatchHandler>>();
	private readonly IDictionary<string, IMessageBusOptions> _busOptionsMap = new Dictionary<string, IMessageBusOptions>();
	private readonly FinalDispatchHandler _final;
	private readonly Dispatcher _sut;
	private readonly IMessageContext _context;
	private readonly IDictionary<string, object> _contextItems = new Dictionary<string, object>();

	public DispatcherShould()
	{
		_context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => _context.Items).Returns(_contextItems);

		_final = new FinalDispatchHandler(_busProvider, _finalLogger, null, _busOptionsMap);
		_sut = new Dispatcher(_middlewareInvoker, _final);
	}

	[Fact]
	public async Task Set_Message_On_Context_And_Return_Result_For_Message()
	{
		var message = A.Fake<IDispatchMessage>();
		var expected = MessageResult.Success();
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				_context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(expected);

		var result = await _sut.DispatchAsync(message, _context, cancellationToken: default).ConfigureAwait(true);

		result.ShouldBe(expected);
		_context.Message.ShouldBe(message);
	}

	[Fact]
	public async Task Propagate_Exception_From_Pipeline()
	{
		var message = A.Fake<IDispatchMessage>();
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				_context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("boom"));

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await _sut.DispatchAsync(message, _context, cancellationToken: default).ConfigureAwait(true));

		exception.Message.ShouldBe("boom");
	}

	[Fact]
	public async Task Return_Typed_Result_When_Pipeline_Produces_It()
	{
		var msg = A.Fake<IDispatchAction<string>>();
		var typed = MessageResult.Success("test-value");
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				msg,
				_context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(typed);

		var result = await _sut.DispatchAsync<IDispatchAction<string>, string>(msg, _context, cancellationToken: default)
			.ConfigureAwait(true);

		result.ShouldBeSameAs(typed);
	}

	#region Sprint 70 - Context Management Tests

	/// <summary>
	/// Verifies that Dispatcher sets MessageContextHolder.Current to the context during dispatch.
	/// This was previously done by MessageContextMiddleware, now handled at Dispatcher level.
	/// </summary>
	[Fact]
	public async Task Set_Ambient_Context_During_Dispatch()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var realContext = new MessageContext();
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				realContext,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() => capturedContext = MessageContextHolder.Current)
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, realContext, cancellationToken: default).ConfigureAwait(true);

		// Assert
		capturedContext.ShouldBe(realContext);
	}

	/// <summary>
	/// Verifies that Dispatcher restores the previous ambient context after dispatch completes.
	/// This was previously done by MessageContextMiddleware, now handled at Dispatcher level.
	/// </summary>
	[Fact]
	public async Task Restore_Previous_Context_After_Dispatch()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var previousContext = new MessageContext { MessageId = "previous" };
		var newContext = new MessageContext { MessageId = "new" };
		MessageContextHolder.Current = previousContext;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				newContext,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, newContext, cancellationToken: default).ConfigureAwait(true);

		// Assert
		MessageContextHolder.Current.ShouldBe(previousContext);
	}

	/// <summary>
	/// Verifies that Dispatcher restores the previous context even when an exception is thrown.
	/// This ensures proper cleanup in error scenarios.
	/// </summary>
	[Fact]
	public async Task Restore_Context_Even_When_Exception_Thrown()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var previousContext = new MessageContext { MessageId = "previous" };
		var newContext = new MessageContext { MessageId = "new" };
		MessageContextHolder.Current = previousContext;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				newContext,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("test exception"));

		// Act
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			async () => await _sut.DispatchAsync(message, newContext, cancellationToken: default).ConfigureAwait(true));

		// Assert
		exception.Message.ShouldBe("test exception");
		MessageContextHolder.Current.ShouldBe(previousContext);
	}

	/// <summary>
	/// Verifies that Dispatcher generates a CorrelationId if not already set.
	/// This was previously done by MessageContextMiddleware, now handled at Dispatcher level.
	/// </summary>
	[Fact]
	public async Task Generate_CorrelationId_When_Not_Set()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext { CorrelationId = null };
		string? capturedCorrelationId = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() => capturedCorrelationId = context.CorrelationId)
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, cancellationToken: default).ConfigureAwait(true);

		// Assert
		capturedCorrelationId.ShouldNotBeNullOrEmpty();
		context.CorrelationId.ShouldNotBeNullOrEmpty();
	}

	/// <summary>
	/// Verifies that Dispatcher preserves existing CorrelationId if already set.
	/// </summary>
	[Fact]
	public async Task Preserve_CorrelationId_When_Already_Set()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var existingCorrelationId = "existing-correlation-id";
		var context = new MessageContext { CorrelationId = existingCorrelationId };

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, cancellationToken: default).ConfigureAwait(true);

		// Assert
		context.CorrelationId.ShouldBe(existingCorrelationId);
	}

	/// <summary>
	/// Verifies that Dispatcher sets CausationId to CorrelationId when CausationId is not set.
	/// This was previously done by MessageContextMiddleware, now handled at Dispatcher level.
	/// </summary>
	[Fact]
	public async Task Set_CausationId_From_CorrelationId_When_Not_Set()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext { CorrelationId = null, CausationId = null };
		string? capturedCausationId = null;
		string? capturedCorrelationId = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() =>
			{
				capturedCorrelationId = context.CorrelationId;
				capturedCausationId = context.CausationId;
			})
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, cancellationToken: default).ConfigureAwait(true);

		// Assert
		capturedCausationId.ShouldNotBeNullOrEmpty();
		capturedCausationId.ShouldBe(capturedCorrelationId);
	}

	/// <summary>
	/// Verifies that Dispatcher preserves existing CausationId if already set.
	/// </summary>
	[Fact]
	public async Task Preserve_CausationId_When_Already_Set()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var existingCausationId = "existing-causation-id";
		var context = new MessageContext { CausationId = existingCausationId };

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, cancellationToken: default).ConfigureAwait(true);

		// Assert
		context.CausationId.ShouldBe(existingCausationId);
	}

	/// <summary>
	/// Verifies that Dispatcher sets MessageType on the context from the message type name.
	/// </summary>
	[Fact]
	public async Task Set_MessageType_On_Context()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext { MessageType = null };

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, cancellationToken: default).ConfigureAwait(true);

		// Assert - MessageType should be set to the type name
		context.MessageType.ShouldNotBeNullOrEmpty();
	}

	#endregion Sprint 70 - Context Management Tests

	#region Sprint 411 - Edge Case Tests

	/// <summary>
	/// Verifies that Dispatcher throws when message is null.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Throw_ArgumentNullException_When_Message_Is_Null()
	{
		// Arrange
		var context = new MessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.DispatchAsync<IDispatchMessage>(null!, context, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that Dispatcher throws when context is null.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Throw_ArgumentNullException_When_Context_Is_Null()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.DispatchAsync(message, null!, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that Dispatcher throws when not configured (no middleware invoker).
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Throw_InvalidOperationException_When_Not_Configured()
	{
		// Arrange
		var unconfiguredDispatcher = new Dispatcher();
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await unconfiguredDispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that cancellation token is propagated to middleware.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Propagate_CancellationToken_To_Middleware()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		CancellationToken capturedToken = default;
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes((IDispatchMessage _, IMessageContext _, Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>> _, CancellationToken ct) =>
				capturedToken = ct)
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, token).ConfigureAwait(true);

		// Assert
		capturedToken.ShouldBe(token);
	}

	/// <summary>
	/// Verifies that generic dispatch throws when not configured.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Generic_Dispatch_Throw_When_Not_Configured()
	{
		// Arrange
		var unconfiguredDispatcher = new Dispatcher();
		var message = A.Fake<IDispatchAction<string>>();
		var context = new MessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await unconfiguredDispatcher.DispatchAsync<IDispatchAction<string>, string>(message, context, CancellationToken.None).ConfigureAwait(true));
	}

	/// <summary>
	/// Verifies that Dispatcher stores message in context Items.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Store_Message_In_Context_Items()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		context.Message.ShouldBeSameAs(message);
	}

	/// <summary>
	/// Verifies that MessageContextHolder.Current is null after dispatch completes when there was no previous.
	/// Sprint 411 T411.4 - Dispatcher edge cases.
	/// </summary>
	[Fact]
	public async Task Clear_Ambient_Context_After_Dispatch_When_No_Previous()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		MessageContextHolder.Current = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await _sut.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public async Task Precompute_RoutingDecision_Before_Middleware_When_Router_Registered()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var dispatcher = new Dispatcher(_middlewareInvoker, _final, null, null, null, _busOptionsMap, router);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		var decision = RoutingDecision.Success("rabbitmq", ["orders"]);
		RoutingDecision? capturedDecision = null;

		_ = A.CallTo(() => router.RouteAsync(message, context, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(decision));

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() => capturedDecision = context.RoutingDecision)
			.Returns(MessageResult.Success());

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedDecision.ShouldNotBeNull();
		capturedDecision.Transport.ShouldBe("rabbitmq");
		A.CallTo(() => router.RouteAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_RoutingFailure_Without_Invoking_Middleware_When_PreRouting_Fails()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var dispatcher = new Dispatcher(_middlewareInvoker, _final, null, null, null, _busOptionsMap, router);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		var failure = RoutingDecision.Failure("Route not found");

		_ = A.CallTo(() => router.RouteAsync(message, context, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(failure));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeFalse();
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(404);
		A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Reuse_Precomputed_RoutingDecision_Without_Calling_Router()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var dispatcher = new Dispatcher(_middlewareInvoker, _final, null, null, null, _busOptionsMap, router);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("local", ["local"]),
		};

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(MessageResult.Success());

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		A.CallTo(() => router.RouteAsync(message, context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task Use_DirectLocal_FastPath_When_NoRouter_And_NoRoutingDecision()
	{
		// Arrange
		var (dispatcher, localInvoker, remoteBus) = CreateTransportAwareDispatcherForFastPath();
		var context = new MessageContext();
		var message = new LocalTransportAction();

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => localInvoker.InvokeAsync(A<object>._, message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => remoteBus.PublishAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Restore_Ambient_Context_After_DirectLocal_FastPath()
	{
		// Arrange
		var (dispatcher, localInvoker, _) = CreateTransportAwareDispatcherForFastPath();
		var context = new MessageContext();
		var message = new LocalTransportAction();
		var previousAmbient = new MessageContext { MessageId = "existing-ambient" };
		IMessageContext? capturedAmbient = null;
		MessageContextHolder.Current = previousAmbient;

		_ = A.CallTo(() => localInvoker.InvokeAsync(A<object>._, message, A<CancellationToken>._))
			.Invokes(() => capturedAmbient = MessageContextHolder.Current)
			.Returns(Task.FromResult<object?>(null));

		try
		{
			// Act
			var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

			// Assert
			result.Succeeded.ShouldBeTrue();
			capturedAmbient.ShouldNotBeNull();
			capturedAmbient.ShouldNotBe(previousAmbient);
			MessageContextHolder.Current.ShouldBe(previousAmbient);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task DispatchLocalAsync_Should_Use_UltraLocal_ValueTask_Path()
	{
		// Arrange
		var (dispatcher, localInvoker, _) = CreateTransportAwareDispatcherForFastPath();
		var message = new LocalTransportAction();

		// Act
		await dispatcher.DispatchLocalAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => localInvoker.InvokeAsync(A<object>._, message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchLocalAsync_With_Response_Should_Return_Handler_Output()
	{
		// Arrange
		var (dispatcher, localInvoker) = CreateTransportAwareTypedDispatcherForFastPath();
		var message = new LocalTransportQuery { Value = 11 };

		// Act
		var result = await dispatcher.DispatchLocalAsync<LocalTransportQuery, int>(message, CancellationToken.None);

		// Assert
		result.ShouldBe(22);
		A.CallTo(() => localInvoker.InvokeAsync(A<object>._, message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchLocalAsync_Should_Create_Context_Lazily_Without_Ambient_Mutation_When_Handler_Requires_Context()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registry = A.Fake<IHandlerRegistry>();
		var activator = A.Fake<IHandlerActivator>();
		var localInvoker = A.Fake<IHandlerInvoker>();
		var localLogger = A.Fake<ILogger<LocalMessageBus>>();
		var busProvider = A.Fake<IMessageBusProvider>();
		var finalLogger = A.Fake<ILogger<FinalDispatchHandler>>();
		var busOptionsMap = new Dictionary<string, IMessageBusOptions>();
		var contextFactory = A.Fake<IMessageContextFactory>();
		var rentedContext = new MessageContext();
		var handler = new LocalTransportContextActionHandler();
		var message = new LocalTransportContextAction();
		var previousAmbient = new MessageContext { MessageId = "existing-ambient" };
		IMessageContext? capturedAmbient = null;

		var handlerEntry = new HandlerRegistryEntry(
			typeof(LocalTransportContextAction),
			typeof(LocalTransportContextActionHandler),
			expectsResponse: false);
		HandlerRegistryEntry outEntry = handlerEntry;
		_ = A.CallTo(() => registry.TryGetHandler(typeof(LocalTransportContextAction), out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(handlerEntry);
		_ = A.CallTo(() => registry.GetAll())
			.Returns([handlerEntry]);

		_ = A.CallTo(() => serviceProvider.GetService(typeof(IMessageContextFactory)))
			.Returns(contextFactory);
		_ = A.CallTo(() => contextFactory.CreateContext())
			.Returns(rentedContext);

		_ = A.CallTo(() => activator.ActivateHandler(typeof(LocalTransportContextActionHandler), A<IMessageContext>._, serviceProvider))
			.Invokes((Type _, IMessageContext context, IServiceProvider _) => handler.Context = context)
			.Returns(handler);
		_ = A.CallTo(() => localInvoker.InvokeAsync(handler, message, A<CancellationToken>._))
			.Invokes(() => capturedAmbient = MessageContextHolder.Current)
			.Returns(Task.FromResult<object?>(null));

		var localMessageBus = new LocalMessageBus(serviceProvider, registry, activator, localInvoker, localLogger);
		IMessageBus? outLocalBus = localMessageBus;
		_ = A.CallTo(() => busProvider.TryGet("local", out outLocalBus))
			.Returns(true)
			.AssignsOutAndRefParameters(localMessageBus);

		var finalHandler = new FinalDispatchHandler(busProvider, finalLogger, retryPolicy: null, busOptionsMap);
		var dispatcher = new Dispatcher(
			middlewareInvoker: new DispatchMiddlewareInvoker([]),
			finalHandler: finalHandler,
			transportContextProvider: null,
			serviceProvider: serviceProvider,
			localMessageBus: localMessageBus,
			busOptionsMap: busOptionsMap,
			dispatchRouter: null,
			dispatchOptions: null);

		MessageContextHolder.Current = previousAmbient;
		try
		{
			// Act
			await dispatcher.DispatchLocalAsync(message, CancellationToken.None);

			// Assert
			MessageContextHolder.Current.ShouldBe(previousAmbient);
			if (capturedAmbient is not null)
			{
				capturedAmbient.ShouldBe(previousAmbient);
			}
			A.CallTo(() => contextFactory.CreateContext()).MustHaveHappenedOnceExactly();
			A.CallTo(() => contextFactory.Return(rentedContext)).MustHaveHappenedOnceExactly();
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task Initialize_Full_DirectLocal_Context_When_Profile_Is_Full()
	{
		// Arrange
		var options = new DispatchOptions();
		options.CrossCutting.Performance.DirectLocalContextInitialization = DirectLocalContextInitializationProfile.Full;
		var (dispatcher, _, _) = CreateTransportAwareDispatcherForFastPath(Microsoft.Extensions.Options.Options.Create(options));
		var context = new MessageContext();
		var message = new LocalTransportAction();

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		context.Message.ShouldBeSameAs(message);
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
		context.CausationId.ShouldBe(context.CorrelationId);
		context.MessageType.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task DispatchLocalAsync_Should_Not_Create_Context_When_No_Local_Handler_Is_Registered()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var contextFactory = A.Fake<IMessageContextFactory>();
		_ = A.CallTo(() => serviceProvider.GetService(typeof(IMessageContextFactory)))
			.Returns(contextFactory);
		var dispatcher = CreateDispatcherWithNoLocalHandlers(serviceProvider);

		// Act
		var ex = await Should.ThrowAsync<InvalidOperationException>(
				async () => await dispatcher.DispatchLocalAsync(new MissingLocalAction(), CancellationToken.None).ConfigureAwait(true))
			.ConfigureAwait(true);

		// Assert
		ex.Message.ShouldContain(nameof(MissingLocalAction));
		A.CallTo(() => contextFactory.CreateContext()).MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchLocalAsync_With_Response_Should_Not_Create_Context_When_No_Local_Handler_Is_Registered()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var contextFactory = A.Fake<IMessageContextFactory>();
		_ = A.CallTo(() => serviceProvider.GetService(typeof(IMessageContextFactory)))
			.Returns(contextFactory);
		var dispatcher = CreateDispatcherWithNoLocalHandlers(serviceProvider);

		// Act
		var ex = await Should.ThrowAsync<InvalidOperationException>(
				async () => await dispatcher.DispatchLocalAsync<MissingLocalQuery, int>(
						new MissingLocalQuery { Value = 7 },
						CancellationToken.None)
					.ConfigureAwait(true))
			.ConfigureAwait(true);

		// Assert
		ex.Message.ShouldContain(nameof(MissingLocalQuery));
		A.CallTo(() => contextFactory.CreateContext()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Return_Minimal_Typed_Result_Metadata_On_DirectLocal_Path_By_Default()
	{
		// Arrange
		var (dispatcher, _) = CreateTransportAwareTypedDispatcherForFastPath();
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("local", ["local"]),
		};
		var message = new LocalTransportQuery { Value = 21 };

		// Act
		var result = await dispatcher.DispatchAsync<LocalTransportQuery, int>(message, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		var routingDecision = result.GetType().GetProperty("RoutingDecision")?.GetValue(result);
		routingDecision.ShouldBeNull();
	}

	[Fact]
	public async Task Emit_Typed_Result_Metadata_On_DirectLocal_Path_When_Enabled()
	{
		// Arrange
		var options = new DispatchOptions();
		options.CrossCutting.Performance.EmitDirectLocalResultMetadata = true;
		var (dispatcher, _) = CreateTransportAwareTypedDispatcherForFastPath(Microsoft.Extensions.Options.Options.Create(options));
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("local", ["local"]),
		};
		var message = new LocalTransportQuery { Value = 21 };

		// Act
		var result = await dispatcher.DispatchAsync<LocalTransportQuery, int>(message, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		var routingDecision = result.GetType().GetProperty("RoutingDecision")?.GetValue(result) as RoutingDecision;
		routingDecision.ShouldNotBeNull();
		routingDecision.Transport.ShouldBe("local");
	}

	[Fact]
	public async Task Use_Remote_Transport_Path_When_RoutingDecision_Targets_Remote_Bus()
	{
		// Arrange
		var (dispatcher, localInvoker, remoteBus) = CreateTransportAwareDispatcherForFastPath();
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("rabbitmq", ["rabbitmq"]),
		};
		var message = new LocalTransportAction();

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => remoteBus.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => localInvoker.InvokeAsync(A<object>._, A<IDispatchMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion Sprint 411 - Edge Case Tests

	private static (Dispatcher Dispatcher, IHandlerInvoker LocalInvoker, IMessageBus RemoteBus) CreateTransportAwareDispatcherForFastPath(
		IOptions<DispatchOptions>? dispatchOptions = null)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		var registry = A.Fake<IHandlerRegistry>();
		var activator = A.Fake<IHandlerActivator>();
		var localInvoker = A.Fake<IHandlerInvoker>();
		var localLogger = A.Fake<ILogger<LocalMessageBus>>();
		var busProvider = A.Fake<IMessageBusProvider>();
		var finalLogger = A.Fake<ILogger<FinalDispatchHandler>>();
		var busOptionsMap = new Dictionary<string, IMessageBusOptions>();

		var handlerEntry = new HandlerRegistryEntry(typeof(LocalTransportAction), typeof(LocalTransportActionHandler), expectsResponse: false);
		HandlerRegistryEntry outEntry = handlerEntry;
		_ = A.CallTo(() => registry.TryGetHandler(typeof(LocalTransportAction), out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(handlerEntry);

		var handler = new LocalTransportActionHandler();
		_ = A.CallTo(() => activator.ActivateHandler(typeof(LocalTransportActionHandler), A<IMessageContext>._, serviceProvider))
			.Returns(handler);
		_ = A.CallTo(() => localInvoker.InvokeAsync(A<object>._, A<IDispatchMessage>._, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		var localMessageBus = new LocalMessageBus(serviceProvider, registry, activator, localInvoker, localLogger);
		var remoteBus = A.Fake<IMessageBus>();

		IMessageBus? outLocalBus = localMessageBus;
		IMessageBus? outRemoteBus = remoteBus;
		_ = A.CallTo(() => busProvider.TryGet("local", out outLocalBus))
			.Returns(true)
			.AssignsOutAndRefParameters(localMessageBus);
		_ = A.CallTo(() => busProvider.TryGet("rabbitmq", out outRemoteBus))
			.Returns(true)
			.AssignsOutAndRefParameters(remoteBus);

		var finalHandler = new FinalDispatchHandler(busProvider, finalLogger, retryPolicy: null, busOptionsMap);
		var middlewareInvoker = new DispatchMiddlewareInvoker([]);
		var dispatcher = new Dispatcher(
			middlewareInvoker: middlewareInvoker,
			finalHandler: finalHandler,
			transportContextProvider: null,
			serviceProvider: serviceProvider,
			localMessageBus: localMessageBus,
			busOptionsMap: busOptionsMap,
			dispatchRouter: null,
			dispatchOptions: dispatchOptions);

		return (dispatcher, localInvoker, remoteBus);
	}

	private static (Dispatcher Dispatcher, IHandlerInvoker LocalInvoker) CreateTransportAwareTypedDispatcherForFastPath(
		IOptions<DispatchOptions>? dispatchOptions = null)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		var registry = A.Fake<IHandlerRegistry>();
		var activator = A.Fake<IHandlerActivator>();
		var localInvoker = A.Fake<IHandlerInvoker>();
		var localLogger = A.Fake<ILogger<LocalMessageBus>>();
		var busProvider = A.Fake<IMessageBusProvider>();
		var finalLogger = A.Fake<ILogger<FinalDispatchHandler>>();
		var busOptionsMap = new Dictionary<string, IMessageBusOptions>();

		var handlerEntry = new HandlerRegistryEntry(typeof(LocalTransportQuery), typeof(LocalTransportQueryHandler), expectsResponse: true);
		HandlerRegistryEntry outEntry = handlerEntry;
		_ = A.CallTo(() => registry.TryGetHandler(typeof(LocalTransportQuery), out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(handlerEntry);

		var handler = new LocalTransportQueryHandler();
		_ = A.CallTo(() => activator.ActivateHandler(typeof(LocalTransportQueryHandler), A<IMessageContext>._, serviceProvider))
			.Returns(handler);
		_ = A.CallTo(() => localInvoker.InvokeAsync(A<object>._, A<IDispatchMessage>._, A<CancellationToken>._))
			.ReturnsLazily((object _, IDispatchMessage message, CancellationToken _) =>
				Task.FromResult<object?>(((LocalTransportQuery)message).Value * 2));

		var localMessageBus = new LocalMessageBus(serviceProvider, registry, activator, localInvoker, localLogger);
		var remoteBus = A.Fake<IMessageBus>();

		IMessageBus? outLocalBus = localMessageBus;
		IMessageBus? outRemoteBus = remoteBus;
		_ = A.CallTo(() => busProvider.TryGet("local", out outLocalBus))
			.Returns(true)
			.AssignsOutAndRefParameters(localMessageBus);
		_ = A.CallTo(() => busProvider.TryGet("rabbitmq", out outRemoteBus))
			.Returns(true)
			.AssignsOutAndRefParameters(remoteBus);

		var finalHandler = new FinalDispatchHandler(busProvider, finalLogger, retryPolicy: null, busOptionsMap);
		var middlewareInvoker = new DispatchMiddlewareInvoker([]);
		var dispatcher = new Dispatcher(
			middlewareInvoker: middlewareInvoker,
			finalHandler: finalHandler,
			transportContextProvider: null,
			serviceProvider: serviceProvider,
			localMessageBus: localMessageBus,
			busOptionsMap: busOptionsMap,
			dispatchRouter: null,
			dispatchOptions: dispatchOptions);

		return (dispatcher, localInvoker);
	}

	private static Dispatcher CreateDispatcherWithNoLocalHandlers(IServiceProvider serviceProvider)
	{
		var registry = A.Fake<IHandlerRegistry>();
		var activator = A.Fake<IHandlerActivator>();
		var localInvoker = A.Fake<IHandlerInvoker>();
		var localLogger = A.Fake<ILogger<LocalMessageBus>>();
		var busProvider = A.Fake<IMessageBusProvider>();
		var finalLogger = A.Fake<ILogger<FinalDispatchHandler>>();
		var busOptionsMap = new Dictionary<string, IMessageBusOptions>();

		HandlerRegistryEntry? missingEntry;
		_ = A.CallTo(() => registry.TryGetHandler(A<Type>._, out missingEntry))
			.Returns(false);
		_ = A.CallTo(() => registry.GetAll()).Returns([]);

		var localMessageBus = new LocalMessageBus(serviceProvider, registry, activator, localInvoker, localLogger);
		IMessageBus? outLocalBus = localMessageBus;
		_ = A.CallTo(() => busProvider.TryGet("local", out outLocalBus))
			.Returns(true)
			.AssignsOutAndRefParameters(localMessageBus);

		var finalHandler = new FinalDispatchHandler(busProvider, finalLogger, retryPolicy: null, busOptionsMap);
		return new Dispatcher(
			middlewareInvoker: new DispatchMiddlewareInvoker([]),
			finalHandler: finalHandler,
			transportContextProvider: null,
			serviceProvider: serviceProvider,
			localMessageBus: localMessageBus,
			busOptionsMap: busOptionsMap,
			dispatchRouter: null,
			dispatchOptions: null);
	}

	private sealed record LocalTransportAction : IDispatchAction;
	private sealed record LocalTransportContextAction : IDispatchAction;
	private sealed record MissingLocalAction : IDispatchAction;
	private sealed record MissingLocalQuery : IDispatchAction<int>
	{
		public int Value { get; init; }
	}

	private sealed record LocalTransportQuery : IDispatchAction<int>
	{
		public int Value { get; init; }
	}

	private sealed class LocalTransportActionHandler
	{
		public Task HandleAsync(LocalTransportAction action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class LocalTransportContextActionHandler
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(LocalTransportContextAction action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class LocalTransportQueryHandler
	{
		public Task<int> HandleAsync(LocalTransportQuery action, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			return Task.FromResult(action.Value * 2);
		}
	}
}
