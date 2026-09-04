// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Locks that the documented zero-config startup freeze actually happens in a consumer's container.
/// </summary>
/// <remarks>
/// <para>
/// <c>PerformanceOptions.AutoFreezeOnStart</c> defaults to <c>true</c> and three handler doc comments tell
/// consumers that freezing happens automatically at application startup. Nothing registered the hosted
/// service that reads that option, and nothing registered the cache manager it needs, so the promise was
/// unbacked: a consumer got no freeze and no error.
/// </para>
/// <para>
/// <b>This resolves from a real container built by the production registration call.</b> It does not
/// hand-construct the service and does not supply test doubles, because the defect was invisible to a test
/// that did: the old unit test constructed the service directly and passed while nothing in any consumer's
/// application ever ran it.
/// </para>
/// <para>
/// <b>The freeze is process-wide.</b> <c>FreezeAll</c> flips static caches shared by every test in this
/// assembly, so this fixture restores them on dispose. Without that, a test asserting a real freeze would
/// silently change the behaviour of unrelated tests that run after it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
// Freezing runs against process-wide cache state shared with the handler-invoker registry, so this
// class must not run beside the other tests that mutate it. Without this it ran in parallel with
// them and the freeze intermittently did not take, failing whichever of the two classes lost the
// race -- a different test each run.
[Collection("HandlerInvokerRegistry")]
public sealed class DispatchAutoFreezeIsWiredShould : IDisposable
{
	/// <summary>
	/// Establishes the precondition instead of inheriting it.
	/// </summary>
	/// <remarks>
	/// The caches these arms observe are STATIC and process-wide, and three sibling classes in this
	/// assembly freeze and clear the same four. Clearing on dispose alone only guarantees what this class
	/// leaves behind; it guarantees nothing about what this class STARTS from, so each arm was asserting
	/// against whatever the previously-run class happened to leave. That is why the second arm failed in
	/// one shard and passed in another at the same commit, minutes apart, with both shards running these
	/// arms in the same order: the difference was never here, it was what ran before. A test that reads
	/// process-wide state must put that state into a known condition itself, not assume a predecessor was
	/// tidy. This does not soften either arm — a freeze that stops happening still fails them both.
	/// </remarks>
	public DispatchAutoFreezeIsWiredShould() => ClearAllFrozenCaches();

	public void Dispose() => ClearAllFrozenCaches();

	/// <summary>
	/// ALL FOUR caches <c>FreezeAll</c> touches. Clearing a subset leaves the rest of the assembly running
	/// against frozen handler caches, and unrelated tests then fail in ways that point nowhere near here.
	/// </summary>
	private static void ClearAllFrozenCaches()
	{
		HandlerInvoker.ClearCache();
		HandlerInvokerRegistry.ClearCache();
		HandlerActivator.ClearCache();
		FinalDispatchHandler.ClearResultFactoryCache();
	}

	[Fact]
	public async Task FreezeTheCachesWhenTheApplicationStarts()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchPipeline();

		var lifetime = new TestApplicationLifetime();
		_ = services.AddSingleton<IHostApplicationLifetime>(lifetime);

		using var provider = services.BuildServiceProvider();

		// The service must be reachable as a hosted service, which is the only way a host will run it.
		var hosted = provider.GetServices<IHostedService>().ToList();
		hosted.ShouldContain(
			s => s is DispatchCacheOptimizationHostedService,
			"the documented automatic freeze can only happen if the host is given the service to run");

		var manager = provider.GetRequiredService<IDispatchCacheManager>();
		manager.IsFrozen.ShouldBeFalse("nothing has started yet, so nothing should be frozen");

		foreach (var service in hosted)
		{
			await service.StartAsync(CancellationToken.None);
		}

		lifetime.RaiseApplicationStarted();

		// The observable effect on the REAL manager, not a recorded call on a fake.
		manager.IsFrozen.ShouldBeTrue(
			"the shipped default promises the caches are frozen once the application has started");

		foreach (var service in hosted)
		{
			await service.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task StillDispatchAfterTheFreeze()
	{
		// LIVENESS: a freeze that broke dispatch would satisfy the arm above and brick every consumer.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchPipeline();

		var lifetime = new TestApplicationLifetime();
		_ = services.AddSingleton<IHostApplicationLifetime>(lifetime);

		using var provider = services.BuildServiceProvider();

		foreach (var service in provider.GetServices<IHostedService>())
		{
			await service.StartAsync(CancellationToken.None);
		}

		lifetime.RaiseApplicationStarted();

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		_ = dispatcher.ShouldNotBeNull("the dispatcher must still resolve and be usable after the freeze");

		provider.GetRequiredService<IDispatchCacheManager>().IsFrozen.ShouldBeTrue(
			"this arm is only meaningful while the freeze actually happened");
	}

	/// <summary>A lifetime whose ApplicationStarted the test raises, since no host is running here.</summary>
	private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
	{
		private readonly CancellationTokenSource _started = new();
		private readonly CancellationTokenSource _stopping = new();
		private readonly CancellationTokenSource _stopped = new();

		public CancellationToken ApplicationStarted => _started.Token;

		public CancellationToken ApplicationStopping => _stopping.Token;

		public CancellationToken ApplicationStopped => _stopped.Token;

		public void RaiseApplicationStarted() => _started.Cancel();

		public void StopApplication() => _stopping.Cancel();

		public void Dispose()
		{
			_started.Dispose();
			_stopping.Dispose();
			_stopped.Dispose();
		}
	}
}
