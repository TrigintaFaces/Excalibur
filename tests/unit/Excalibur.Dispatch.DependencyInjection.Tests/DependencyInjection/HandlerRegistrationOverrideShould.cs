// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Locks the registration contract of <c>AddHandlersFromAssembly</c>:
/// a consumer's explicit handler registration wins over an assembly-scanned one in BOTH
/// orderings (safety), every event handler registered for one event still runs (liveness),
/// and <c>registerWithContainer: false</c> suppresses every registration the call would
/// otherwise make (safety) without preventing the consumer's own registration from
/// dispatching (liveness).
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
public sealed class HandlerRegistrationOverrideShould : IDisposable
{
	private static readonly System.Reflection.Assembly TestAssembly = typeof(HandlerRegistrationOverrideShould).Assembly;

	private ServiceProvider? _serviceProvider;

	public void Dispose() => _serviceProvider?.Dispose();

	#region Ordering — a consumer override wins either way

	[Fact]
	public async Task RunConsumerOverride_WhenItIsRegisteredBeforeAddDispatch()
	{
		var services = NewServices(out var recorder);

		// Consumer registers their override FIRST, then Dispatch scans the assembly.
		_ = services.AddScoped<IActionHandler<OverriddenAction>, ManualOverrideHandler<OverriddenAction>>();
		_ = services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));

		await DispatchAsync(services, new OverriddenAction()).ConfigureAwait(true);

		recorder.Entries.ShouldBe([nameof(ManualOverrideHandler<OverriddenAction>)]);
	}

	[Fact]
	public async Task RunConsumerOverride_WhenItIsRegisteredAfterAddDispatch()
	{
		var services = NewServices(out var recorder);

		// The same two lines, reversed.
		_ = services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));
		_ = services.AddScoped<IActionHandler<OverriddenAction>, ManualOverrideHandler<OverriddenAction>>();

		await DispatchAsync(services, new OverriddenAction()).ConfigureAwait(true);

		recorder.Entries.ShouldBe([nameof(ManualOverrideHandler<OverriddenAction>)]);
	}

	#endregion Ordering — a consumer override wins either way

	#region Liveness — multiple handlers for one event all still run

	[Fact]
	public async Task RunEveryScannedEventHandler_ForASingleEvent()
	{
		var services = NewServices(out var recorder);

		_ = services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));

		await DispatchAsync(services, new FannedOutEvent()).ConfigureAwait(true);

		recorder.Entries.OrderBy(static entry => entry, StringComparer.Ordinal)
			.ShouldBe([nameof(FirstFanOutHandler), nameof(SecondFanOutHandler)]);
	}

	[Fact]
	public async Task RunEveryEventHandlerExactlyOnce_WhenOneOfThemIsAlsoRegisteredManually()
	{
		var services = NewServices(out var recorder);

		// A manual registration of one event handler must neither displace its siblings
		// nor cause itself to run twice.
		_ = services.AddScoped<IEventHandler<FannedOutEvent>, FirstFanOutHandler>();
		_ = services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));

		await DispatchAsync(services, new FannedOutEvent()).ConfigureAwait(true);

		recorder.Entries.OrderBy(static entry => entry, StringComparer.Ordinal)
			.ShouldBe([nameof(FirstFanOutHandler), nameof(SecondFanOutHandler)]);
	}

	#endregion Liveness — multiple handlers for one event all still run

	#region registerWithContainer

	[Fact]
	public void RegisterNeitherConcreteNorInterface_WhenRegisterWithContainerIsFalse()
	{
		var services = NewServices(out _);

		_ = services.AddDispatch(dispatch =>
			dispatch.AddHandlersFromAssembly(TestAssembly, registerWithContainer: false));

		DescriptorsFor(services, typeof(OptOutHandler)).ShouldBeEmpty();
		DescriptorsFor(services, typeof(IActionHandler<OptOutAction>)).ShouldBeEmpty();
	}

	[Fact]
	public void RegisterBothConcreteAndInterface_WhenRegisterWithContainerIsDefaulted()
	{
		var services = NewServices(out _);

		_ = services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(TestAssembly));

		DescriptorsFor(services, typeof(OptOutHandler)).ShouldHaveSingleItem();
		DescriptorsFor(services, typeof(IActionHandler<OptOutAction>))
			.ShouldContain(descriptor => descriptor.ImplementationType == typeof(OptOutHandler));
	}

	[Fact]
	public async Task StillDispatchToTheConsumersOwnHandler_WhenRegisterWithContainerIsFalse()
	{
		var services = NewServices(out var recorder);

		_ = services.AddDispatch(dispatch =>
			dispatch.AddHandlersFromAssembly(TestAssembly, registerWithContainer: false));
		_ = services.AddScoped<IActionHandler<OptOutAction>, ManualOptOutHandler<OptOutAction>>();

		await DispatchAsync(services, new OptOutAction()).ConfigureAwait(true);

		recorder.Entries.ShouldBe([nameof(ManualOptOutHandler<OptOutAction>)]);
	}

	#endregion registerWithContainer

	#region Harness

	private static ServiceCollection NewServices(out HandlerRecorder recorder)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		recorder = new HandlerRecorder();
		_ = services.AddSingleton(recorder);

		return services;
	}

	private static IEnumerable<ServiceDescriptor> DescriptorsFor(IServiceCollection services, Type serviceType) =>
		services.Where(descriptor => descriptor.ServiceType == serviceType);

	private async Task DispatchAsync<TMessage>(ServiceCollection services, TMessage message)
		where TMessage : IDispatchMessage
	{
		_serviceProvider = services.BuildServiceProvider();

		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var result = await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken).ConfigureAwait(true);

		result.Succeeded.ShouldBeTrue(result.ErrorMessage ?? "dispatch failed");
	}

	internal sealed class HandlerRecorder
	{
		private readonly ConcurrentQueue<string> _entries = new();

		public IReadOnlyList<string> Entries => [.. _entries];

		public void Record(string handlerName) => _entries.Enqueue(handlerName);
	}

	#endregion Harness

	#region Fixtures

	internal sealed class OverriddenAction : IDispatchAction;

	internal sealed class OptOutAction : IDispatchAction;

	internal sealed class FannedOutEvent : IDispatchEvent;

	/// <summary>The handler assembly scanning discovers; it must lose to a consumer registration.</summary>
	internal sealed class ScannedOverriddenHandler(HandlerRecorder recorder) : IActionHandler<OverriddenAction>
	{
		public Task HandleAsync(OverriddenAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(ScannedOverriddenHandler));
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// The consumer's explicit override; it must win in both orderings.
	/// </summary>
	/// <remarks>
	/// Declared generic on a marker that is never used, so that assembly scanning cannot see it
	/// (the scan skips generic type definitions, and a constructed generic is not in
	/// <c>Assembly.GetTypes()</c>). That models the real case — a handler the consumer registers
	/// which the scanned assembly does not contain — and, more importantly, keeps the lock
	/// deterministic: were this handler itself scannable, it would be re-registered by the scan and
	/// could win on scan order alone rather than because the override was honored.
	/// </remarks>
	internal sealed class ManualOverrideHandler<TUnusedMarker>(HandlerRecorder recorder) : IActionHandler<OverriddenAction>
	{
		public Task HandleAsync(OverriddenAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(ManualOverrideHandler<TUnusedMarker>));
			return Task.CompletedTask;
		}
	}

	internal sealed class FirstFanOutHandler(HandlerRecorder recorder) : IEventHandler<FannedOutEvent>
	{
		public Task HandleAsync(FannedOutEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(FirstFanOutHandler));
			return Task.CompletedTask;
		}
	}

	internal sealed class SecondFanOutHandler(HandlerRecorder recorder) : IEventHandler<FannedOutEvent>
	{
		public Task HandleAsync(FannedOutEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(SecondFanOutHandler));
			return Task.CompletedTask;
		}
	}

	internal sealed class OptOutHandler(HandlerRecorder recorder) : IActionHandler<OptOutAction>
	{
		public Task HandleAsync(OptOutAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(OptOutHandler));
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// The consumer's own opt-out-scenario handler. Generic for the same reason as
	/// <see cref="ManualOverrideHandler{TUnusedMarker}"/> — invisible to the scan, so the
	/// scanned-descriptor assertions name exactly one candidate implementation.
	/// </summary>
	internal sealed class ManualOptOutHandler<TUnusedMarker>(HandlerRecorder recorder) : IActionHandler<OptOutAction>
	{
		public Task HandleAsync(OptOutAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(ManualOptOutHandler<TUnusedMarker>));
			return Task.CompletedTask;
		}
	}

	#endregion Fixtures
}
