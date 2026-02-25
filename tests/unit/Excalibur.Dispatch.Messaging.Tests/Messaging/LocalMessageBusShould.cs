// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for LocalMessageBus class covering send, publish, document operations.
/// </summary>
/// <remarks>
/// Sprint 411 - Core Pipeline Coverage (T411.2).
/// Target: Increase LocalMessageBus coverage from 39.1% to 70%.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class LocalMessageBusShould
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IHandlerRegistry _registry;
	private readonly IHandlerActivator _activator;
	private readonly IHandlerInvoker _invoker;
	private readonly ILogger<LocalMessageBus> _logger;
	private readonly LocalMessageBus _bus;

	public LocalMessageBusShould()
	{
		_serviceProvider = A.Fake<IServiceProvider>();
		_registry = A.Fake<IHandlerRegistry>();
		_activator = A.Fake<IHandlerActivator>();
		_invoker = A.Fake<IHandlerInvoker>();
		_logger = A.Fake<ILogger<LocalMessageBus>>();
		_bus = new LocalMessageBus(_serviceProvider, _registry, _activator, _invoker, _logger);
	}

	#region SendAsync Tests

	[Fact]
	public async Task SendAsync_Should_Throw_For_Null_Action()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.SendAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_Should_Throw_For_Null_Context()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.SendAsync(action, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_Should_Throw_When_No_Handler_Registered()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		HandlerRegistryEntry? entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out entry))
			.Returns(false);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_bus.SendAsync(action, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendAsync_Should_Invoke_Handler_When_Registered()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);

		var handlerType = typeof(TestActionHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchAction), handlerType, false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestActionHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.SendAsync(action, context, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SendAsync_Should_Store_Result_When_Handler_Expects_Response()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		object? capturedResult = null;
		_ = A.CallTo(() => context.Result).ReturnsLazily(() => capturedResult);
		A.CallToSet(() => context.Result)
			.Invokes((object? value) => capturedResult = value);

		var handlerType = typeof(TestActionHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchAction), handlerType, expectsResponse: true);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestActionHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		var result = new { Success = true };
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(result));

		// Act
		await _bus.SendAsync(action, context, CancellationToken.None);

		// Assert
		capturedResult.ShouldBe(result);
	}

	[Fact]
	public async Task SendAsync_Should_Skip_Handler_When_Cache_Hit()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		_ = A.CallTo(() => context.GetItem("Dispatch:CacheHit", false)).Returns(true);
		_ = A.CallTo(() => context.Result).Returns("cached-result");

		// Act
		await _bus.SendAsync(action, context, CancellationToken.None);

		// Assert - Handler should not be invoked when cache hit
		// Note: Cannot use A<HandlerRegistryEntry>._ in out parameter position,
		// so we verify via the invoker not being called instead
		A.CallTo(() => _invoker.InvokeAsync(A<object>._, A<IDispatchMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SendAsync_Should_Respect_CancellationToken()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);

		var handlerType = typeof(TestActionHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchAction), handlerType, false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestActionHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, token))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.SendAsync(action, context, token);

		// Assert - Verify the correct token was passed
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region PublishAsync (Event) Tests

	[Fact]
	public async Task PublishAsync_Event_Should_Throw_For_Null_Event()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.PublishAsync((IDispatchEvent)null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task PublishAsync_Event_Should_Throw_For_Null_Context()
	{
		// Arrange
		var evt = A.Fake<IDispatchEvent>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.PublishAsync(evt, null!, CancellationToken.None));
	}

	[Fact]
	public async Task PublishAsync_Event_Should_Return_When_No_Handlers_Registered()
	{
		// Arrange
		var evt = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		_ = A.CallTo(() => _registry.GetAll())
			.Returns(Array.Empty<HandlerRegistryEntry>());

		// Act - Should not throw
		await _bus.PublishAsync(evt, context, CancellationToken.None);

		// Assert - No handler activation should occur
		A.CallTo(() => _activator.ActivateHandler(A<Type>._, A<IMessageContext>._, A<IServiceProvider>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PublishAsync_Event_Should_Invoke_All_Matching_Handlers()
	{
		// Arrange
		var evt = new TestEvent();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		var handler1Type = typeof(TestEventHandler1);
		var handler2Type = typeof(TestEventHandler2);
		var entries = new HandlerRegistryEntry[]
		{
			new(typeof(TestEvent), handler1Type, false),
			new(typeof(TestEvent), handler2Type, false)
		};

		_ = A.CallTo(() => _registry.GetAll())
			.Returns(entries);

		var handler1 = new TestEventHandler1();
		var handler2 = new TestEventHandler2();

		_ = A.CallTo(() => _activator.ActivateHandler(handler1Type, context, _serviceProvider))
			.Returns(handler1);
		_ = A.CallTo(() => _activator.ActivateHandler(handler2Type, context, _serviceProvider))
			.Returns(handler2);

		_ = A.CallTo(() => _invoker.InvokeAsync(A<object>._, evt, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.PublishAsync(evt, context, CancellationToken.None);

		// Assert - Both handlers should be invoked
		_ = A.CallTo(() => _invoker.InvokeAsync(handler1, evt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _invoker.InvokeAsync(handler2, evt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region SendDocumentAsync Tests

	[Fact]
	public async Task SendDocumentAsync_Should_Throw_For_Null_Document()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.SendDocumentAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendDocumentAsync_Should_Throw_For_Null_Context()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_bus.SendDocumentAsync(doc, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SendDocumentAsync_Should_Throw_When_No_Handler_Registered()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		HandlerRegistryEntry? entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out entry))
			.Returns(false);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_bus.SendDocumentAsync(doc, context, CancellationToken.None));
	}

	[Fact]
	public async Task SendDocumentAsync_Should_Invoke_Handler_When_Registered()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		var handlerType = typeof(TestDocumentHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchDocument), handlerType, false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestDocumentHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		_ = A.CallTo(() => _invoker.InvokeAsync(handler, doc, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.SendDocumentAsync(doc, context, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, doc, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region PublishAsync Delegation Tests

	[Fact]
	public async Task PublishAsync_Action_Should_Delegate_To_SendAsync()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);

		var handlerType = typeof(TestActionHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchAction), handlerType, false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestActionHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.PublishAsync(action, context, CancellationToken.None);

		// Assert - Verify it delegates to SendAsync path
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishAsync_Document_Should_Delegate_To_SendDocumentAsync()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);

		var handlerType = typeof(TestDocumentHandler);
		var entry = new HandlerRegistryEntry(typeof(IDispatchDocument), handlerType, false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => _registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);

		var handler = new TestDocumentHandler();
		_ = A.CallTo(() => _activator.ActivateHandler(handlerType, context, _serviceProvider))
			.Returns(handler);

		_ = A.CallTo(() => _invoker.InvokeAsync(handler, doc, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await _bus.PublishAsync(doc, context, CancellationToken.None);

		// Assert - Verify it delegates to SendDocumentAsync path
		_ = A.CallTo(() => _invoker.InvokeAsync(handler, doc, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Singleton Fast Path Tests

	[Fact]
	public async Task TryInvokeUltraLocal_Should_Use_Typed_Direct_Handler_Path_Without_Runtime_Invoker()
	{
		// Arrange
		var action = new TypedUltraLocalAction();
		var services = new ServiceCollection();
		_ = services.AddSingleton<TypedUltraLocalHandler>();
		var provider = services.BuildServiceProvider();
		var registry = A.Fake<IHandlerRegistry>();
		var invoker = A.Fake<IHandlerInvoker>();
		var logger = A.Fake<ILogger<LocalMessageBus>>();
		var bus = new LocalMessageBus(provider, registry, new HandlerActivator(), invoker, logger);

		var entry = new HandlerRegistryEntry(typeof(TypedUltraLocalAction), typeof(TypedUltraLocalHandler), expectsResponse: false);
		_ = A.CallTo(() => registry.GetAll()).Returns([entry]);

		// Recreate bus after registry setup so direct plan cache precomputes entry.
		bus = new LocalMessageBus(provider, registry, new HandlerActivator(), invoker, logger);

		// Act
		var invoked = bus.TryInvokeUltraLocal(action, CancellationToken.None, out var invocation);
		_ = await invocation.ConfigureAwait(true);

		// Assert
		invoked.ShouldBeTrue();
		var handler = provider.GetRequiredService<TypedUltraLocalHandler>();
		handler.CallCount.ShouldBe(1);
		A.CallTo(() => invoker.InvokeAsync(A<object>._, A<IDispatchMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SendAsync_Should_Use_Singleton_NoContext_Instance_Cache_When_Eligible()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);
		_ = A.CallTo(() => context.GetItem("Dispatch:CacheHit", false)).Returns(false);

		var handler = new SingletonNoContextHandler();
		var provider = new CountingServiceProvider(typeof(SingletonNoContextHandler), handler);
		_ = A.CallTo(() => context.RequestServices).Returns(provider);
		var registry = A.Fake<IHandlerRegistry>();
		var invoker = A.Fake<IHandlerInvoker>();
		var logger = A.Fake<ILogger<LocalMessageBus>>();
		var bus = new LocalMessageBus(provider, registry, new HandlerActivator(), invoker, logger);

		var entry = new HandlerRegistryEntry(action.GetType(), typeof(SingletonNoContextHandler), false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);
		_ = A.CallTo(() => invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await bus.SendAsync(action, context, CancellationToken.None);
		await bus.SendAsync(action, context, CancellationToken.None);
		await bus.SendAsync(action, context, CancellationToken.None);

		// Assert
		provider.ResolutionCount.ShouldBe(2);
	}

	[Fact]
	public async Task SendAsync_Should_Not_Use_Singleton_NoContext_Cache_When_Handler_Requires_Context()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => context.Items).Returns(items);
		_ = A.CallTo(() => context.GetItem("Dispatch:CacheHit", false)).Returns(false);

		var handler = new SingletonWithContextHandler();
		var provider = new CountingServiceProvider(typeof(SingletonWithContextHandler), handler);
		_ = A.CallTo(() => context.RequestServices).Returns(provider);
		var registry = A.Fake<IHandlerRegistry>();
		var invoker = A.Fake<IHandlerInvoker>();
		var logger = A.Fake<ILogger<LocalMessageBus>>();
		var bus = new LocalMessageBus(provider, registry, new HandlerActivator(), invoker, logger);

		var entry = new HandlerRegistryEntry(action.GetType(), typeof(SingletonWithContextHandler), false);
		HandlerRegistryEntry? outEntry = entry;
		_ = A.CallTo(() => registry.TryGetHandler(A<Type>._, out outEntry))
			.Returns(true)
			.AssignsOutAndRefParameters(entry);
		_ = A.CallTo(() => invoker.InvokeAsync(handler, action, A<CancellationToken>._))
			.Returns(Task.FromResult<object?>(null));

		// Act
		await bus.SendAsync(action, context, CancellationToken.None);
		await bus.SendAsync(action, context, CancellationToken.None);
		await bus.SendAsync(action, context, CancellationToken.None);

		// Assert
		provider.ResolutionCount.ShouldBe(3);
		handler.Context.ShouldBe(context);
	}

	[Fact]
	public async Task SendAsync_Should_Fallback_To_RootProvider_When_RequestServices_Cannot_Resolve_Handler_Dependencies()
	{
		// Arrange
		string? capturedCorrelationId = null;
		var services = new ServiceCollection();
		_ = services.AddSingleton<Action<string>>(value => capturedCorrelationId = value);
		_ = services.AddTransient<RequestServicesFallbackHandler>();
		var rootProvider = services.BuildServiceProvider();
		var registry = new HandlerRegistry();
		registry.Register(typeof(RequestServicesFallbackAction), typeof(RequestServicesFallbackHandler), expectsResponse: false);
		var bus = new LocalMessageBus(
			rootProvider,
			registry,
			new HandlerActivator(),
			new HandlerInvoker(),
			A.Fake<ILogger<LocalMessageBus>>());
		var context = new MessageContext
		{
			CorrelationId = Guid.NewGuid().ToString(),
			RequestServices = new NullServiceProvider(),
		};
		var action = new RequestServicesFallbackAction();

		// Act
		await bus.SendAsync(action, context, CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedCorrelationId.ShouldBe(context.CorrelationId);
	}

	#endregion

	#region Test Helpers

	private sealed class TestActionHandler { }
	private sealed class TestDocumentHandler { }
	private sealed class TestEventHandler1 { }
	private sealed class TestEventHandler2 { }
	private sealed class SingletonNoContextHandler { }
	private sealed class SingletonWithContextHandler
	{
		public IMessageContext? Context { get; set; }
	}
	private sealed class TypedUltraLocalAction : IDispatchAction
	{
		public string MessageId => nameof(TypedUltraLocalAction);
		public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(TypedUltraLocalAction);
		public IMessageFeatures Features => A.Fake<IMessageFeatures>();
		public Guid Id => Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
	}

	private sealed class TypedUltraLocalHandler : IActionHandler<TypedUltraLocalAction>
	{
		public int CallCount { get; private set; }

		public Task HandleAsync(TypedUltraLocalAction action, CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.CompletedTask;
		}
	}

	private sealed class CountingServiceProvider(Type trackedType, object instance) : IServiceProvider, IServiceProviderIsService
	{
		public int ResolutionCount { get; private set; }

		public object? GetService(Type serviceType)
		{
			if (serviceType == typeof(IServiceProviderIsService))
			{
				return this;
			}

			if (serviceType == trackedType)
			{
				ResolutionCount++;
				return instance;
			}

			return null;
		}

	public bool IsService(Type serviceType)
		=> serviceType == typeof(IServiceProviderIsService) || serviceType == trackedType;
	}

	private sealed class NullServiceProvider : IServiceProvider
	{
		public object? GetService(Type serviceType) => null;
	}

	private sealed class RequestServicesFallbackAction : IDispatchAction
	{
		public string MessageId => Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(RequestServicesFallbackAction);
		public IMessageFeatures Features => A.Fake<IMessageFeatures>();
		public Guid Id => Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
	}

	private sealed class RequestServicesFallbackHandler(Action<string> captureCorrelationId) : IActionHandler<RequestServicesFallbackAction>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(RequestServicesFallbackAction action, CancellationToken cancellationToken)
		{
			if (Context?.CorrelationId is { } correlationId)
			{
				captureCorrelationId(correlationId);
			}

			return Task.CompletedTask;
		}
	}

	private sealed class TestEvent : IDispatchEvent
	{
		public string MessageId => "test-message-id";
		public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(TestEvent);
		public IMessageFeatures Features => A.Fake<IMessageFeatures>();
		public Guid Id => Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Event;
	}

	#endregion
}


