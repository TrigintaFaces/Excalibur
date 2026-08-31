// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests
#pragma warning disable CA1034 // Nested types should not be visible - needed for test handler types

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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

	// ---- Mutable instance state disqualifies a handler on EVERY branch, not only the branch that ----
	// ---- happened to reach the check.                                                            ----
	//
	// The eligibility rule has always been a conjunction: the handler must take nothing injectable that
	// outlives a request, AND it must carry no mutable instance state. Two branches returned true before
	// reaching the second half — the no-public-constructor branch and the parameterless-constructor branch,
	// the latter commented "parameterless constructor = stateless = safe singleton". That is not what a
	// parameterless constructor means. It means nothing is INJECTED. A type with no constructor arguments
	// can still declare a counter, and promoting it hands every dispatch in the process one instance of it:
	// state leaks between unrelated messages and increments race under concurrency.
	//
	// These arms assert the observable outcome — the lifetime the registration ends up with — rather than
	// which branch produced it, so a rewrite that reaches the same rule differently stays green.

	[Fact]
	public void NotPromoteStatefulHandlerWithParameterlessConstructor()
	{
		// Arrange — the shape the false comment described as safe: nothing injected, but stateful.
		var services = new ServiceCollection();
		services.AddTransient<IActionHandler<TestCommand>, StatefulParameterlessHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBe(
			0,
			"a handler with a parameterless constructor and a mutable instance field was promoted. Taking no "
			+ "constructor arguments means nothing is injected, not that the type holds no state");

		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(
			ServiceLifetime.Transient,
			"the consumer registered this handler Transient and it carries mutable state, so one shared "
			+ "instance would leak that state across every dispatch in the process and race under concurrency");
	}

	[Fact]
	public void NotPromoteStatefulHandlerWithNoPublicConstructor()
	{
		// Arrange — the second branch that skipped the state check. A type with only a non-public
		// constructor cannot be activated by the container at all, so the lifetime its registration ends up
		// with is the only thing an observer can see here; there is no instance to compare.
		var services = new ServiceCollection();
		services.AddTransient<IActionHandler<TestCommand>, StatefulNoPublicConstructorHandler>();

		// Act
		var promoted = HandlerLifetimeAnalyzer.PromoteEligibleHandlers(services);

		// Assert
		promoted.ShouldBe(
			0,
			"a handler with no public constructor and a mutable instance field was promoted. Having nothing "
			+ "to inject is not the same as having nothing to share");

		var descriptor = services.First(d => d.ServiceType == typeof(IActionHandler<TestCommand>));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	// ---- The rule has to hold where a consumer actually meets it: through AddDispatch. ----
	//
	// Every arm above calls the analyzer directly, so all of them would still pass if AddDispatch never
	// invoked it — the advertised-but-unwired shape. These two go through the public entry point with the
	// option set, and register the handler BEFORE AddDispatch so the descriptor is present when the option
	// is acted on.

	[Fact]
	public void NotPromoteAStatefulHandlerThroughAddDispatch()
	{
		// SAFETY, wired.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddTransient<IActionHandler<TestCommand>, StatefulParameterlessHandler>();

		_ = services.AddDispatch(dispatch =>
			dispatch.WithOptions(options =>
				options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true));

		// Assert on every descriptor naming this implementation, so a second registration added by
		// discovery cannot hide a promoted one behind a Transient one.
		services
			.Where(d => d.ImplementationType == typeof(StatefulParameterlessHandler))
			.ShouldAllBe(
				d => d.Lifetime == ServiceLifetime.Transient,
				"a stateful handler was promoted to Singleton by opting into the optimisation. The consumer "
				+ "asked for a faster dispatch, not for their handler's state to become process-wide");
	}

	[Fact]
	public void StillPromoteAStatelessHandlerThroughAddDispatch()
	{
		// LIVENESS, wired. Without this arm, tightening the rule is satisfiable by never promoting
		// anything — which turns a correctness fix into a silent performance regression, and would leave
		// the option above doing nothing whether it is set or not.
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddTransient<IActionHandler<TestCommand>, StatelessHandler>();

		_ = services.AddDispatch(dispatch =>
			dispatch.WithOptions(options =>
				options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true));

		services
			.Where(d => d.ImplementationType == typeof(StatelessHandler))
			.ShouldContain(
				d => d.Lifetime == ServiceLifetime.Singleton,
				"a genuinely stateless handler was not promoted with the optimisation enabled, so opting in "
				+ "now buys nothing. Excluding handlers that carry state must narrow what is promoted, not "
				+ "stop promotion happening at all");
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

	/// <summary>
	/// Nothing to inject, but not stateless: <c>_handled</c> is mutable instance state, and one shared
	/// instance would make it a process-wide counter incremented from every dispatch.
	/// </summary>
	public sealed class StatefulParameterlessHandler : IActionHandler<TestCommand>
	{
		private int _handled;

		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			_handled++;
			return Task.FromResult(_handled);
		}
	}

	/// <summary>
	/// The same state, reached through the other branch: a non-public constructor means
	/// <see cref="Type.GetConstructors()"/> reports none, which used to short-circuit the state check.
	/// </summary>
	public sealed class StatefulNoPublicConstructorHandler : IActionHandler<TestCommand>
	{
		private int _handled;

		private StatefulNoPublicConstructorHandler()
		{
		}

		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			_handled++;
			return Task.FromResult(_handled);
		}
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

	#pragma warning disable CS9113
	public sealed class HandlerWithSingletonDep(ISingletonDep _dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#pragma warning disable CS9113
	public sealed class HandlerWithScopedDep(IScopedDep _dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#pragma warning disable CS9113
	public sealed class HandlerWithTransientDep(ITransientDep _dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#pragma warning disable CS9113
	public sealed class HandlerWithUnknownDep(IUnknownDep _dep) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#pragma warning disable CS9113
	public sealed class HandlerWithLoggerDep(ILogger<HandlerWithLoggerDep> _logger) : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	#endregion
}
