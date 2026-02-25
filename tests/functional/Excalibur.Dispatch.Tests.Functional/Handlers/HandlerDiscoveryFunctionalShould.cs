// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.Dispatch.Tests.Functional.Handlers;

/// <summary>
/// Functional tests for handler discovery and registration patterns.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Handlers")]
[Trait("Feature", "Discovery")]
public sealed class HandlerDiscoveryFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void DiscoverHandlersFromAssembly()
	{
		// Arrange
		var handlerInterface = typeof(ITestHandler<>);
		var assembly = Assembly.GetExecutingAssembly();

		// Act - Find all types implementing the handler interface
		var handlers = assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => t.GetInterfaces()
				.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
			.ToList();

		// Assert
		handlers.Count.ShouldBeGreaterThanOrEqualTo(2);
		handlers.ShouldContain(typeof(TestMessageHandler));
		handlers.ShouldContain(typeof(AnotherMessageHandler));
	}

	[Fact]
	public void RegisterHandlersInServiceContainer()
	{
		// Arrange
		var registry = new TestHandlerRegistry();

		// Act - Register handlers
		registry.Register<TestMessage, TestMessageHandler>();
		registry.Register<AnotherMessage, AnotherMessageHandler>();

		// Assert
		registry.GetHandler<TestMessage>().ShouldBe(typeof(TestMessageHandler));
		registry.GetHandler<AnotherMessage>().ShouldBe(typeof(AnotherMessageHandler));
	}

	[Fact]
	public void ResolveHandlerForMessageType()
	{
		// Arrange
		var registry = new TestHandlerRegistry();
		registry.Register<TestMessage, TestMessageHandler>();

		// Act
		var handlerType = registry.GetHandler<TestMessage>();
		var handler = Activator.CreateInstance(handlerType) as ITestHandler<TestMessage>;

		// Assert
		_ = handler.ShouldNotBeNull();
		_ = handler.ShouldBeOfType<TestMessageHandler>();
	}

	[Fact]
	public async Task InvokeHandlerForMessage()
	{
		// Arrange
		var handler = new TestMessageHandler();
		var message = new TestMessage { Id = Guid.NewGuid(), Content = "Test" };

		// Act
		await handler.HandleAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		handler.ProcessedMessages.Count.ShouldBe(1);
		handler.ProcessedMessages[0].ShouldBe(message.Id);
	}

	[Fact]
	public void SupportMultipleHandlersPerMessage()
	{
		// Arrange
		var registry = new MultiHandlerRegistry();

		// Act - Register multiple handlers for same message type
		registry.Register<NotificationMessage, EmailNotificationHandler>();
		registry.Register<NotificationMessage, SmsNotificationHandler>();
		registry.Register<NotificationMessage, PushNotificationHandler>();

		// Assert
		var handlers = registry.GetHandlers<NotificationMessage>();
		handlers.Count.ShouldBe(3);
		handlers.ShouldContain(typeof(EmailNotificationHandler));
		handlers.ShouldContain(typeof(SmsNotificationHandler));
		handlers.ShouldContain(typeof(PushNotificationHandler));
	}

	[Fact]
	public async Task ExecuteMultipleHandlersInOrder()
	{
		// Arrange
		var executionOrder = new ConcurrentQueue<string>();
		var handlers = new List<ITestHandler<NotificationMessage>>
		{
			new OrderedHandler("First", executionOrder),
			new OrderedHandler("Second", executionOrder),
			new OrderedHandler("Third", executionOrder),
		};

		var message = new NotificationMessage { Channel = "Test" };

		// Act - Execute handlers sequentially
		foreach (var handler in handlers)
		{
			await handler.HandleAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Assert
		var order = executionOrder.ToArray();
		order.Length.ShouldBe(3);
		order[0].ShouldBe("First");
		order[1].ShouldBe("Second");
		order[2].ShouldBe("Third");
	}

	[Fact]
	public void SupportHandlerDecorators()
	{
		// Arrange
		var innerHandler = new TestMessageHandler();
		var loggingDecorator = new LoggingHandlerDecorator<TestMessage>(innerHandler);

		// Assert
		loggingDecorator.InnerHandler.ShouldBe(innerHandler);
	}

	[Fact]
	public async Task ApplyDecoratorBehavior()
	{
		// Arrange
		var innerHandler = new TestMessageHandler();
		var loggingDecorator = new LoggingHandlerDecorator<TestMessage>(innerHandler);
		var message = new TestMessage { Id = Guid.NewGuid(), Content = "Test" };

		// Act
		await loggingDecorator.HandleAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		loggingDecorator.LogEntries.Count.ShouldBe(2);
		loggingDecorator.LogEntries[0].ShouldContain("Starting");
		loggingDecorator.LogEntries[1].ShouldContain("Completed");
		innerHandler.ProcessedMessages.Count.ShouldBe(1);
	}

	[Fact]
	public void DetectMissingHandlers()
	{
		// Arrange
		var registry = new TestHandlerRegistry();
		registry.Register<TestMessage, TestMessageHandler>();

		// Act
		var missingHandler = registry.GetHandler<UnhandledMessage>();

		// Assert
		missingHandler.ShouldBeNull();
	}

	[Fact]
	public void SupportConditionalHandlerRegistration()
	{
		// Arrange
		var registry = new ConditionalHandlerRegistry();
		var condition = true;

		// Act - Register handler conditionally
		if (condition)
		{
			registry.Register<TestMessage, TestMessageHandler>("production");
		}
		else
		{
			registry.Register<TestMessage, MockMessageHandler>("development");
		}

		// Assert
		var handler = registry.GetHandler<TestMessage>("production");
		handler.ShouldBe(typeof(TestMessageHandler));

		var devHandler = registry.GetHandler<TestMessage>("development");
		devHandler.ShouldBeNull();
	}

	[Fact]
	public void SupportHandlerLifetimeScopes()
	{
		// Arrange
		var lifetimes = new Dictionary<Type, HandlerLifetime>
		{
			[typeof(TestMessageHandler)] = HandlerLifetime.Singleton,
			[typeof(AnotherMessageHandler)] = HandlerLifetime.Scoped,
			[typeof(MockMessageHandler)] = HandlerLifetime.Transient,
		};

		// Assert
		lifetimes[typeof(TestMessageHandler)].ShouldBe(HandlerLifetime.Singleton);
		lifetimes[typeof(AnotherMessageHandler)].ShouldBe(HandlerLifetime.Scoped);
		lifetimes[typeof(MockMessageHandler)].ShouldBe(HandlerLifetime.Transient);
	}

	private interface ITestHandler<in TMessage>
	{
		Task HandleAsync(TMessage message, CancellationToken cancellationToken);
	}

	private sealed record TestMessage
	{
		public Guid Id { get; init; }
		public string Content { get; init; } = string.Empty;
	}

	private sealed record AnotherMessage
	{
		public string Data { get; init; } = string.Empty;
	}

	private sealed record NotificationMessage
	{
		public string Channel { get; init; } = string.Empty;
	}

	private sealed record UnhandledMessage;

	private sealed class TestMessageHandler : ITestHandler<TestMessage>
	{
		public List<Guid> ProcessedMessages { get; } = [];

		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			ProcessedMessages.Add(message.Id);
			return Task.CompletedTask;
		}
	}

	private sealed class AnotherMessageHandler : ITestHandler<AnotherMessage>
	{
		public Task HandleAsync(AnotherMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class MockMessageHandler : ITestHandler<TestMessage>
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class EmailNotificationHandler : ITestHandler<NotificationMessage>
	{
		public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class SmsNotificationHandler : ITestHandler<NotificationMessage>
	{
		public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class PushNotificationHandler : ITestHandler<NotificationMessage>
	{
		public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class OrderedHandler(string name, ConcurrentQueue<string> executionOrder) : ITestHandler<NotificationMessage>
	{
		public Task HandleAsync(NotificationMessage message, CancellationToken cancellationToken)
		{
			executionOrder.Enqueue(name);
			return Task.CompletedTask;
		}
	}

	private sealed class LoggingHandlerDecorator<TMessage>(ITestHandler<TMessage> innerHandler) : ITestHandler<TMessage>
	{
		public ITestHandler<TMessage> InnerHandler { get; } = innerHandler;
		public List<string> LogEntries { get; } = [];

		public async Task HandleAsync(TMessage message, CancellationToken cancellationToken)
		{
			LogEntries.Add($"Starting handler for {typeof(TMessage).Name}");
			await InnerHandler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
			LogEntries.Add($"Completed handler for {typeof(TMessage).Name}");
		}
	}

	private sealed class TestHandlerRegistry
	{
		private readonly Dictionary<Type, Type> _handlers = [];

		public void Register<TMessage, THandler>()
			where THandler : ITestHandler<TMessage>
		{
			_handlers[typeof(TMessage)] = typeof(THandler);
		}

		public Type? GetHandler<TMessage>()
		{
			return _handlers.TryGetValue(typeof(TMessage), out var handler) ? handler : null;
		}
	}

	private sealed class MultiHandlerRegistry
	{
		private readonly Dictionary<Type, List<Type>> _handlers = [];

		public void Register<TMessage, THandler>()
			where THandler : ITestHandler<TMessage>
		{
			if (!_handlers.TryGetValue(typeof(TMessage), out var handlers))
			{
				handlers = [];
				_handlers[typeof(TMessage)] = handlers;
			}

			handlers.Add(typeof(THandler));
		}

		public List<Type> GetHandlers<TMessage>()
		{
			return _handlers.TryGetValue(typeof(TMessage), out var handlers) ? handlers : [];
		}
	}

	private sealed class ConditionalHandlerRegistry
	{
		private readonly Dictionary<(Type, string), Type> _handlers = [];

		public void Register<TMessage, THandler>(string environment)
			where THandler : ITestHandler<TMessage>
		{
			_handlers[(typeof(TMessage), environment)] = typeof(THandler);
		}

		public Type? GetHandler<TMessage>(string environment)
		{
			return _handlers.TryGetValue((typeof(TMessage), environment), out var handler) ? handler : null;
		}
	}

	private enum HandlerLifetime
	{
		Transient,
		Scoped,
		Singleton,
	}
}
