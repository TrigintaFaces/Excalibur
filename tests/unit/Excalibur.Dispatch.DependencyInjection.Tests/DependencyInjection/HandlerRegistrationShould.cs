// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Unit tests for handler registration via ServiceCollection extensions covering
/// single handler, multiple handlers, handler discovery, and handler lifecycle scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatcher")]
[Trait("Priority", "0")]
public sealed class HandlerRegistrationShould : UnitTestBase
{
	#region Single Handler Registration Tests

	[Fact]
	public void RegisterSingleHandler_ViaAddDispatchHandlers_SuccessfullyRegistersHandler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var result = registry.TryGetHandler(typeof(TestCommand), out var entry);
		result.ShouldBeTrue();
		entry.MessageType.ShouldBe(typeof(TestCommand));
		entry.HandlerType.ShouldBe(typeof(TestCommandHandler));
	}

	[Fact]
	public void RegisterSingleHandler_ViaAddDispatch_SuccessfullyRegistersHandler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var result = registry.TryGetHandler(typeof(TestCommand), out var entry);
		result.ShouldBeTrue();
		entry.HandlerType.ShouldBe(typeof(TestCommandHandler));
	}

	[Fact]
	public void RegisterSingleHandlerWithResponse_SuccessfullySetsExpectsResponse()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestQuery, TestQueryResult>, TestQueryHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var result = registry.TryGetHandler(typeof(TestQuery), out var entry);
		result.ShouldBeTrue();
		entry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void RegisterSingleHandlerWithoutResponse_SetsExpectsResponseFalse()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var result = registry.TryGetHandler(typeof(TestCommand), out var entry);
		result.ShouldBeTrue();
		entry.ExpectsResponse.ShouldBeFalse();
	}

	[Fact]
	public void RegisterSingleHandler_CanResolveHandlerFromDI()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();

		// Act
		var handler = provider.GetService<IActionHandler<TestCommand>>();

		// Assert
		_ = handler.ShouldNotBeNull();
		_ = handler.ShouldBeOfType<TestCommandHandler>();
	}

	#endregion Single Handler Registration Tests

	#region Multiple Handler Registration Tests

	[Fact]
	public void RegisterMultipleHandlers_AllHandlersRegisteredSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestQuery, TestQueryResult>, TestQueryHandler>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var all = registry.GetAll();
		all.Count.ShouldBeGreaterThanOrEqualTo(3);

		registry.TryGetHandler(typeof(TestCommand), out var commandEntry).ShouldBeTrue();
		registry.TryGetHandler(typeof(TestQuery), out var queryEntry).ShouldBeTrue();
		registry.TryGetHandler(typeof(TestEvent), out var eventEntry).ShouldBeTrue();
	}

	[Fact]
	public void RegisterMultipleHandlers_MixedResponseTypes_CorrectlyConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestQuery, TestQueryResult>, TestQueryHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		_ = registry.TryGetHandler(typeof(TestCommand), out var commandEntry);
		commandEntry.ExpectsResponse.ShouldBeFalse();

		_ = registry.TryGetHandler(typeof(TestQuery), out var queryEntry);
		queryEntry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void RegisterMultipleHandlers_AllResolvableFromDI()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestQuery, TestQueryResult>, TestQueryHandler>();

		var provider = services.BuildServiceProvider();

		// Act
		var commandHandler = provider.GetService<IActionHandler<TestCommand>>();
		var queryHandler = provider.GetService<IActionHandler<TestQuery, TestQueryResult>>();

		// Assert
		_ = commandHandler.ShouldNotBeNull();
		_ = queryHandler.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterMultipleHandlers_SameMessageTypeTwice_LastRegistrationWins()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, AlternateCommandHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		_ = registry.TryGetHandler(typeof(TestCommand), out var entry);
		entry.HandlerType.ShouldBe(typeof(AlternateCommandHandler));
	}

	[Fact]
	public void RegisterMultipleHandlers_DifferentMessageTypes_MaintainsSeparateEntries()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<AnotherCommand>, AnotherCommandHandler>();

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		_ = registry.TryGetHandler(typeof(TestCommand), out var entry1);
		_ = registry.TryGetHandler(typeof(AnotherCommand), out var entry2);

		entry1.HandlerType.ShouldBe(typeof(TestCommandHandler));
		entry2.HandlerType.ShouldBe(typeof(AnotherCommandHandler));
	}

	#endregion Multiple Handler Registration Tests

	#region Handler Discovery Tests

	[Fact]
	public void AutoDiscoverHandlers_FromAssembly_SuccessfullyRegistersAllHandlers()
	{
		// Arrange
		var services = new ServiceCollection();
		var testAssembly = typeof(TestCommand).Assembly;

		// Act
		_ = services.AddDispatchHandlers(testAssembly);

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var all = registry.GetAll();
		all.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void AutoDiscoverHandlers_FromMultipleAssemblies_SuccessfullyRegistersAllHandlers()
	{
		// Arrange
		var services = new ServiceCollection();
		var testAssembly = typeof(TestCommand).Assembly;
		var dispatchAssembly = typeof(IDispatcher).Assembly;

		// Act
		_ = services.AddDispatchHandlers(testAssembly, dispatchAssembly);

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert
		var all = registry.GetAll();
		all.ShouldNotBeEmpty();
	}

	[Fact]
	public void AutoDiscoverHandlers_NullAssemblies_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var exception = Record.Exception(() => services.AddDispatchHandlers(null));

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void AutoDiscoverHandlers_EmptyAssemblyArray_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var exception = Record.Exception(() => services.AddDispatchHandlers());

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void AutoDiscoverHandlers_IgnoresAbstractHandlers()
	{
		// Arrange
		var services = new ServiceCollection();
		var testAssembly = typeof(AbstractTestHandler).Assembly;

		// Act
		_ = services.AddDispatchHandlers(testAssembly);

		var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<IHandlerRegistry>();

		// Assert - AbstractTestHandler should not be registered
		// Only concrete handlers should be discovered
		var all = registry.GetAll();
		all.ShouldNotContain(e => e.HandlerType == typeof(AbstractTestHandler));
	}

	#endregion Handler Discovery Tests

	#region Handler Lifecycle Tests

	[Fact]
	public void RegisterHandler_AsTransient_CreatesNewInstanceEachTime()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();

		// Act
		var handler1 = provider.GetService<IActionHandler<TestCommand>>();
		var handler2 = provider.GetService<IActionHandler<TestCommand>>();

		// Assert
		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public void RegisterHandler_AsSingleton_ReturnsSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddSingleton<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();

		// Act
		var handler1 = provider.GetService<IActionHandler<TestCommand>>();
		var handler2 = provider.GetService<IActionHandler<TestCommand>>();

		// Assert
		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		handler1.ShouldBeSameAs(handler2);
	}

	[Fact]
	public void RegisterHandler_AsScoped_ReturnsSameInstanceWithinScope()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddScoped<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();

		// Act & Assert
		using (var scope1 = provider.CreateScope())
		{
			var handler1 = scope1.ServiceProvider.GetService<IActionHandler<TestCommand>>();
			var handler2 = scope1.ServiceProvider.GetService<IActionHandler<TestCommand>>();

			_ = handler1.ShouldNotBeNull();
			_ = handler2.ShouldNotBeNull();
			handler1.ShouldBeSameAs(handler2);
		}
	}

	[Fact]
	public void RegisterHandler_AsScoped_CreatesDifferentInstancesAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddDispatchHandlers();
		_ = services.AddScoped<IActionHandler<TestCommand>, TestCommandHandler>();

		var provider = services.BuildServiceProvider();

		// Act
		IActionHandler<TestCommand>? handler1;
		IActionHandler<TestCommand>? handler2;

		using (var scope1 = provider.CreateScope())
		{
			handler1 = scope1.ServiceProvider.GetService<IActionHandler<TestCommand>>();
		}

		using (var scope2 = provider.CreateScope())
		{
			handler2 = scope2.ServiceProvider.GetService<IActionHandler<TestCommand>>();
		}

		// Assert
		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public void AddDispatchHandlers_CanBeCalledMultipleTimes_WithoutError()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var exception = Record.Exception(() =>
		{
			_ = services.AddDispatchHandlers();
			_ = services.AddDispatchHandlers();
			_ = services.AddDispatchHandlers();
		});

		// Assert
		exception.ShouldBeNull();
	}

	#endregion Handler Lifecycle Tests

	#region Test Fixtures

	// Test message types
	private sealed class TestCommand : IDispatchAction
	{ }

	private sealed class AnotherCommand : IDispatchAction
	{ }

	private sealed class TestQuery : IDispatchAction<TestQueryResult>
	{ }

	private sealed class TestEvent : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TestEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	// Test result type
	private sealed class TestQueryResult
	{ }

	// Test handler implementations
	private sealed class TestCommandHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class AlternateCommandHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class AnotherCommandHandler : IActionHandler<AnotherCommand>
	{
		public Task HandleAsync(AnotherCommand action, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class TestQueryHandler : IActionHandler<TestQuery, TestQueryResult>
	{
		public Task<TestQueryResult> HandleAsync(TestQuery action, CancellationToken cancellationToken) =>
			Task.FromResult(new TestQueryResult());
	}

	private sealed class TestEventHandler : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private abstract class AbstractTestHandler : IActionHandler<TestCommand>
	{
		public abstract Task HandleAsync(TestCommand action, CancellationToken cancellationToken);
	}

	#endregion Test Fixtures
}
