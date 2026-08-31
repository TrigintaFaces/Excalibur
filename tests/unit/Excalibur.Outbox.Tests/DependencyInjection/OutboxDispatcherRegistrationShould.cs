// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// Regression guard: the real outbox dispatcher and processor must be registered by the
/// outbox subsystem itself (<c>AddOutbox</c> / <c>AddExcaliburOutbox</c>), not only when A3
/// audit is added, and the real dispatcher must win over A3's fail-fast fallback regardless of
/// composition order while still yielding to a consumer-supplied dispatcher.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxDispatcherRegistrationShould
{
	[Fact]
	public async Task RegisterRealDispatcher_WithoutRequiringA3()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		// Resolved rather than read off the descriptor: IOutboxDispatcher is registered by factory
		// (so the container stays constructible when no store provider was chosen), which leaves
		// ImplementationType null. Resolving asserts the strictly stronger property -- the registration
		// actually PRODUCES a MessageOutbox -- instead of trusting a descriptor's declared type.
		await using var provider = BuildResolvableProvider(services);
		provider.GetRequiredService<IOutboxDispatcher>()
			.ShouldBeOfType<MessageOutbox>("AddOutbox(...) must register the real IOutboxDispatcher (MessageOutbox).");
	}

	[Fact]
	public async Task RegisterOutboxProcessor()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		// See RegisterRealDispatcher_WithoutRequiringA3: factory registration, so assert the produced
		// instance rather than the descriptor's declared implementation type.
		await using var provider = BuildResolvableProvider(services);
		provider.GetRequiredService<IOutboxProcessor>()
			.ShouldBeOfType<OutboxProcessor>("AddOutbox(...) must register IOutboxProcessor (OutboxProcessor).");
	}

	[Fact]
	public void RegisterProcessorAsTransient_SoEachPartitionAndDispatcherGetsItsOwn()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		var descriptor = services.Single(d => d.ServiceType == typeof(IOutboxProcessor));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
	}

	[Fact]
	public async Task RealDispatcherWins_WhenA3FallbackStubRegisteredFirst()
	{
		var services = new ServiceCollection();

		// Simulate A3 audit composed BEFORE the outbox: its fail-fast stub is registered first.
		_ = services.AddSingleton<IOutboxDispatcher, Excalibur.A3.Audit.Internal.DefaultOutboxDispatcher>();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		var dispatchers = services.Where(d => d.ServiceType == typeof(IOutboxDispatcher)).ToList();
		_ = dispatchers.ShouldHaveSingleItem();

		// The stub-removal contract is a descriptor-count property and still reads off the collection;
		// WHICH dispatcher survived is asserted by resolving it (factory registration -- see
		// RegisterRealDispatcher_WithoutRequiringA3).
		await using var provider = BuildResolvableProvider(services);
		provider.GetRequiredService<IOutboxDispatcher>()
			.ShouldBeOfType<MessageOutbox>(
				"The real MessageOutbox must win over A3's DefaultOutboxDispatcher stub regardless of order.");
	}

	[Fact]
	public void ConsumerDispatcherWins_WhenRegisteredBeforeOutbox()
	{
		var services = new ServiceCollection();

		// A consumer-supplied dispatcher (not the A3 stub) must be preserved.
		_ = services.AddSingleton<IOutboxDispatcher, CustomConsumerOutboxDispatcher>();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		var dispatchers = services.Where(d => d.ServiceType == typeof(IOutboxDispatcher)).ToList();
		_ = dispatchers.ShouldHaveSingleItem();
		dispatchers[0].ImplementationType.ShouldBe(typeof(CustomConsumerOutboxDispatcher),
			"A consumer-supplied IOutboxDispatcher must not be removed by the outbox registration.");
	}

	/// <summary>
	/// Builds a provider that can actually construct the outbox pipeline: supplies the
	/// <see cref="IOutboxStore"/> a store provider would have registered, plus logging, so the
	/// assertions above can resolve rather than inspect. Deliberately NOT part of the
	/// production surface -- the point of these tests is what <c>AddOutbox</c> registers.
	/// </summary>
	/// <remarks>
	/// Callers must <c>await using</c> the result: the resolved outbox pipeline is
	/// <see cref="IAsyncDisposable"/>-only, and a synchronous container dispose throws.
	/// </remarks>
	private static ServiceProvider BuildResolvableProvider(IServiceCollection services)
	{
		services.AddLogging();
		services.TryAddSingleton(A.Fake<IOutboxStore>());
		return services.BuildServiceProvider();
	}
}

/// <summary>Minimal no-op <see cref="IOutboxDispatcher"/> for registration-shape tests.</summary>
internal abstract class TestOutboxDispatcherBase : IOutboxDispatcher
{
	public Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken) => Task.FromResult(0);

	public Task SaveEventsAsync(IReadOnlyCollection<IIntegrationEvent> integrationEvents, IMessageMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;

	public Task<int> SaveMessagesAsync(ICollection<IOutboxMessage> outboxMessages, CancellationToken cancellationToken) => Task.FromResult(0);

	public Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<IDispatchMessage>());

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Stands in for a consumer-supplied dispatcher.</summary>
internal sealed class CustomConsumerOutboxDispatcher : TestOutboxDispatcherBase;
