// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests
#pragma warning disable CA1034 // Nested types should not be visible - needed for test handler types

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerLifetimeAnalyzerShould
{
	[Fact]
	public void PromoteStatelessHandlerWithNoDependencies()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTransient<IActionHandler<TestCommand>, StatelessHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBeGreaterThanOrEqualTo(1);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void PromoteHandlerWithSingletonDependencies()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ISingletonDep, SingletonDep>();
		services.AddTransient<IActionHandler<TestCommand>, HandlerWithSingletonDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBeGreaterThanOrEqualTo(1);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void NotPromoteHandlerWithScopedDependency()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<IScopedDep, ScopedDep>();
		services.AddTransient<IActionHandler<TestCommand>, HandlerWithScopedDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBe(0);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void NotPromoteHandlerWithTransientDependency()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTransient<ITransientDep, TransientDep>();
		services.AddTransient<IActionHandler<TestCommand>, HandlerWithTransientDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBe(0);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public void NotPromoteAlreadySingletonHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IActionHandler<TestCommand>, StatelessHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — already singleton, nothing to promote
		promoted.ShouldBe(0);
	}

	[Fact]
	public void NotPromoteHandlerWithUnknownDependency()
	{
		// Arrange
		var services = new ServiceCollection();
		// Register handler with a dependency that is NOT registered in DI
		services.AddTransient<IActionHandler<TestCommand>, HandlerWithUnknownDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — conservative: unknown dep → don't promote
		promoted.ShouldBe(0);
	}

	[Fact]
	public void PromoteEventHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTransient<IEventHandler<TestEvent>, StatelessEventHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBeGreaterThanOrEqualTo(1);
		var descriptor = services.First(d => d.ServiceType == typeof(IEventHandler<TestEvent>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void PromoteActionHandlerWithResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTransient<IActionHandler<TestQuery, string>, StatelessQueryHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBeGreaterThanOrEqualTo(1);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestQuery, string>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void PromoteHandlerWithILoggerDependency()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddTransient<IActionHandler<TestCommand>, HandlerWithLoggerDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — ILogger<T> is singleton via ILoggerFactory
		promoted.ShouldBeGreaterThanOrEqualTo(1);
		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => HandlerLifetimeAnalyzer.PromoteEligibleHandlers(null!));
	}

	[Fact]
	public void NotPromoteNonHandlerServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddTransient<ISingletonDep, SingletonDep>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert — ISingletonDep is not a handler interface
		promoted.ShouldBe(0);
	}

	[Fact]
	public void HandleEmptyServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBe(0);
	}

	#region Test Types

	public sealed class TestCommand : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string Type { get; set; } = "TestCommand";
		public string MessageType { get; set; } = "TestCommand";
		public MessageKinds Kind { get; set; } = MessageKinds.Action;
		public object Body { get; set; } = new object();
		public ReadOnlyMemory<byte> Payload { get; set; }
		public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
		public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
	}

	public sealed class TestQuery : IDispatchAction<string>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string Type { get; set; } = "TestQuery";
		public string MessageType { get; set; } = "TestQuery";
		public MessageKinds Kind { get; set; } = MessageKinds.Action;
		public object Body { get; set; } = new object();
		public ReadOnlyMemory<byte> Payload { get; set; }
		public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
		public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
	}

	public sealed class TestEvent : IDispatchEvent
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string Type { get; set; } = "TestEvent";
		public string MessageType { get; set; } = "TestEvent";
		public MessageKinds Kind { get; set; } = MessageKinds.Event;
		public object Body { get; set; } = new object();
		public ReadOnlyMemory<byte> Payload { get; set; }
		public IReadOnlyDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
		public IMessageFeatures Features { get; set; } = new DefaultMessageFeatures();
	}

	public interface ISingletonDep;
	public interface IScopedDep;
	public interface ITransientDep;
	public interface IUnknownDep;

	public sealed class SingletonDep : ISingletonDep;
	public sealed class ScopedDep : IScopedDep;
	public sealed class TransientDep : ITransientDep;

	public sealed class StatelessHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class StatelessEventHandler : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class StatelessQueryHandler : IActionHandler<TestQuery, string>
	{
		public Task<string> HandleAsync(TestQuery action, CancellationToken cancellationToken) =>
			Task.FromResult("result");
	}

	public sealed class HandlerWithSingletonDep(ISingletonDep dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class HandlerWithScopedDep(IScopedDep dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class HandlerWithTransientDep(ITransientDep dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class HandlerWithUnknownDep(IUnknownDep dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	public sealed class HandlerWithLoggerDep(ILogger<HandlerWithLoggerDep> logger) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
