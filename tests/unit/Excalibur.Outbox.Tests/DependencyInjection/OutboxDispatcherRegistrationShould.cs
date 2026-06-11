// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

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
	public void RegisterRealDispatcher_WithoutRequiringA3()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		services.Any(d =>
			d.ServiceType == typeof(IOutboxDispatcher) &&
			d.ImplementationType == typeof(MessageOutbox))
			.ShouldBeTrue("AddOutbox(...) must register the real IOutboxDispatcher (MessageOutbox).");
	}

	[Fact]
	public void RegisterOutboxProcessor()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		services.Any(d =>
			d.ServiceType == typeof(IOutboxProcessor) &&
			d.ImplementationType == typeof(OutboxProcessor))
			.ShouldBeTrue("AddOutbox(...) must register IOutboxProcessor (OutboxProcessor).");
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
	public void RealDispatcherWins_WhenA3FallbackStubRegisteredFirst()
	{
		var services = new ServiceCollection();

		// Simulate A3 audit composed BEFORE the outbox: its fail-fast stub is registered first.
		_ = services.AddSingleton<IOutboxDispatcher, Excalibur.A3.Audit.Internal.DefaultOutboxDispatcher>();

		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		var dispatchers = services.Where(d => d.ServiceType == typeof(IOutboxDispatcher)).ToList();
		_ = dispatchers.ShouldHaveSingleItem();
		dispatchers[0].ImplementationType.ShouldBe(typeof(MessageOutbox),
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
