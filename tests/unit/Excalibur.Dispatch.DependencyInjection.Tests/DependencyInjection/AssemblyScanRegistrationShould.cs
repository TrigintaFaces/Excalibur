// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Locks the registration contract of the assembly-scanning overload,
/// <c>AddDispatch(params Assembly[])</c>, which is a different code path from the builder's
/// <c>AddHandlersFromAssembly</c>: every handler registered for one event runs (liveness), while a
/// message type that takes a single handler still resolves to exactly one -- the consumer's, when
/// they registered one (safety). Both properties are needed together; a fix that satisfies either
/// alone is wrong.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
public sealed class AssemblyScanRegistrationShould : IDisposable
{
	private static readonly System.Reflection.Assembly TestAssembly = typeof(AssemblyScanRegistrationShould).Assembly;

	private ServiceProvider? _serviceProvider;

	public void Dispose() => _serviceProvider?.Dispose();

	[Fact]
	public async Task RunEveryScannedEventHandler_ForASingleEvent()
	{
		var services = NewServices(out var recorder);

		_ = services.AddDispatch(TestAssembly);

		await DispatchAsync(services, new ScanFanOutEvent()).ConfigureAwait(true);

		recorder.Entries.OrderBy(static entry => entry, StringComparer.Ordinal)
			.ShouldBe([nameof(FirstScannedEventHandler), nameof(SecondScannedEventHandler)]);
	}

	[Fact]
	public async Task RunOnlyTheConsumersHandler_ForAMessageTypeThatTakesOne()
	{
		var services = NewServices(out var recorder);

		// The consumer registered a handler for this action; scanning finds a different one for the
		// same action. Exactly one must run, and it must be the consumer's -- fanning out here would
		// run both, and yielding the wrong way would run the scanned one.
		_ = services.AddScoped<IActionHandler<ScanSingleAction>, ConsumerSingleActionHandler<ScanSingleAction>>();
		_ = services.AddDispatch(TestAssembly);

		await DispatchAsync(services, new ScanSingleAction()).ConfigureAwait(true);

		recorder.Entries.ShouldBe([nameof(ConsumerSingleActionHandler<ScanSingleAction>)]);
	}

	[Fact]
	public void AddNoInterfaceDescriptor_WhenTheConsumerAlreadyRegisteredOneForThatMessageType()
	{
		var services = NewServices(out _);

		// Scanning must yield to the existing registration rather than append to it: appending is
		// what fanning a single-handler message type out would do, and it is invisible at dispatch
		// because the registry collapses the descriptors again.
		_ = services.AddScoped<IActionHandler<ScanSingleAction>, ConsumerSingleActionHandler<ScanSingleAction>>();
		_ = services.AddDispatch(TestAssembly);

		services.Count(descriptor => descriptor.ServiceType == typeof(IActionHandler<ScanSingleAction>))
			.ShouldBe(1);
	}

	#region Harness

	private static ServiceCollection NewServices(out ScanRecorder recorder)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		recorder = new ScanRecorder();
		_ = services.AddSingleton(recorder);

		return services;
	}

	private async Task DispatchAsync<TMessage>(ServiceCollection services, TMessage message)
		where TMessage : IDispatchMessage
	{
		_serviceProvider = services.BuildServiceProvider();

		var dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		var result = await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken).ConfigureAwait(true);

		result.Succeeded.ShouldBeTrue(result.ErrorMessage ?? "dispatch failed");
	}

	internal sealed class ScanRecorder
	{
		private readonly ConcurrentQueue<string> _entries = new();

		public IReadOnlyList<string> Entries => [.. _entries];

		public void Record(string handlerName) => _entries.Enqueue(handlerName);
	}

	#endregion Harness

	#region Fixtures

	internal sealed class ScanFanOutEvent : IDispatchEvent;

	internal sealed class ScanSingleAction : IDispatchAction;

	internal sealed class FirstScannedEventHandler(ScanRecorder recorder) : IEventHandler<ScanFanOutEvent>
	{
		public Task HandleAsync(ScanFanOutEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(FirstScannedEventHandler));
			return Task.CompletedTask;
		}
	}

	internal sealed class SecondScannedEventHandler(ScanRecorder recorder) : IEventHandler<ScanFanOutEvent>
	{
		public Task HandleAsync(ScanFanOutEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(SecondScannedEventHandler));
			return Task.CompletedTask;
		}
	}

	internal sealed class ScannedSingleActionHandler(ScanRecorder recorder) : IActionHandler<ScanSingleAction>
	{
		public Task HandleAsync(ScanSingleAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(ScannedSingleActionHandler));
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// The consumer's own handler. Generic on an unused marker so assembly scanning cannot see it
	/// (the scan skips generic type definitions, and a constructed generic is not in
	/// <c>Assembly.GetTypes()</c>), which models a handler the scanned assembly does not contain and
	/// keeps the lock from passing on scan order alone.
	/// </summary>
	internal sealed class ConsumerSingleActionHandler<TUnusedMarker>(ScanRecorder recorder) : IActionHandler<ScanSingleAction>
	{
		public Task HandleAsync(ScanSingleAction action, CancellationToken cancellationToken)
		{
			recorder.Record(nameof(ConsumerSingleActionHandler<TUnusedMarker>));
			return Task.CompletedTask;
		}
	}

	#endregion Fixtures
}
