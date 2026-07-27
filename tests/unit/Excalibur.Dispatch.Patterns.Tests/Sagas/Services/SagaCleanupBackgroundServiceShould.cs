// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Services;

/// <summary>
/// WIRE lock for the saga automatic-cleanup background service (ma1jc7). Proves the
/// <see cref="SagaOptions.EnableAutomaticCleanup"/> option is a real, end-to-end driver — the hosted
/// service actually drives <see cref="ISagaStore.PurgeCompletedBeforeAsync"/> against a real store —
/// not merely registered. Time is driven by a <see cref="FakeTimeProvider"/> so the interval loop and
/// retention threshold are deterministic (no wall-clock on the decision path).
/// </summary>
/// <remarks>
/// Pre-wire the service did not exist, so the enabled read-through case is inherently RED before the
/// wiring landed. The enabled-vs-disabled contrast is the structural non-vacuity guard: the same aged
/// saga is purged when enabled and retained when disabled.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Saga)]
public sealed class SagaCleanupBackgroundServiceShould
{
	private static readonly DateTimeOffset Origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task PurgeAgedCompletedSagas_WhenCleanupEnabled()
	{
		// Arrange — a completed saga older than the retention window, in a real in-memory store.
		var timeProvider = new FakeTimeProvider(Origin);
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();
		await store.SaveAsync(
			new TestSaga { SagaId = sagaId, CompletedAt = Origin - TimeSpan.FromDays(40) },
			CancellationToken.None);

		var options = new SagaOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMinutes(5),
			SagaRetentionPeriod = TimeSpan.FromDays(30),
		};
		var storeProvider = new ServiceCollection()
			.AddSingleton<ISagaStore>(store)
			.BuildServiceProvider();
		using var service = new SagaCleanupBackgroundService(
			storeProvider, MsOptions.Create(options), timeProvider, NullLogger<SagaCleanupBackgroundService>.Instance);

		// Act — start the hosted service, then advance past the interval to fire a cleanup cycle. Advance
		// inside the poll loop so a tick registered slightly after StartAsync returns is not missed
		// (the aged saga stays older than the moving threshold, so extra ticks are harmless).
		await service.StartAsync(CancellationToken.None);
		await WaitUntilAsync(async () =>
		{
			timeProvider.Advance(options.CleanupInterval);
			return await store.LoadAsync<TestSaga>(sagaId, CancellationToken.None) is null;
		});

		// Assert — the aged saga is removed by the service reading through to the store's purge primitive.
		(await store.LoadAsync<TestSaga>(sagaId, CancellationToken.None))
			.ShouldBeNull("the cleanup service must purge completed sagas older than the retention period");

		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotPurge_WhenCleanupDisabled()
	{
		// Arrange — same aged saga, but automatic cleanup disabled.
		var timeProvider = new FakeTimeProvider(Origin);
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();
		await store.SaveAsync(
			new TestSaga { SagaId = sagaId, CompletedAt = Origin - TimeSpan.FromDays(40) },
			CancellationToken.None);

		var options = new SagaOptions
		{
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.FromMinutes(5),
			SagaRetentionPeriod = TimeSpan.FromDays(30),
		};
		var storeProvider = new ServiceCollection()
			.AddSingleton<ISagaStore>(store)
			.BuildServiceProvider();
		using var service = new SagaCleanupBackgroundService(
			storeProvider, MsOptions.Create(options), timeProvider, NullLogger<SagaCleanupBackgroundService>.Instance);

		// Act — start and advance well past several intervals.
		await service.StartAsync(CancellationToken.None);
		timeProvider.Advance(TimeSpan.FromHours(1));
		await Task.Yield();

		// Assert — nothing is purged when the option is off (the manual primitive stays the only path).
		(await store.LoadAsync<TestSaga>(sagaId, CancellationToken.None))
			.ShouldNotBeNull("cleanup disabled must leave completed sagas untouched");

		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public void ResolveSagaCleanupService_FromDIContainer_WithAllDependencies()
	{
		// Arrange — wire the PRODUCT DI exactly as a consumer host would: AddExcaliburSaga registers the
		// SagaCleanupBackgroundService as an IHostedService, and AddInMemorySagaStore satisfies its
		// ISagaStore dependency. Deliberately NOT registering TimeProvider — the whole point of this lock
		// is that the product wiring (AddExcaliburSaga) must itself provide every constructor dependency
		// the hosted service needs, including TimeProvider.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburSaga();
		_ = services.AddInMemorySagaStore();

		using var provider = services.BuildServiceProvider();

		// Act + Assert — resolving the hosted-service graph must materialise the cleanup service. This is
		// the load-bearing assertion: real DI resolution of the ctor (IServiceProvider, IOptions<SagaOptions>,
		// TimeProvider, ILogger). Non-vacuity: on committed HEAD (before AddExcaliburSaga registered
		// TimeProvider.System) the container cannot construct the ctor's TimeProvider dependency, so this
		// enumeration throws InvalidOperationException = RED. It goes GREEN once AddExcaliburSaga provides
		// TimeProvider. The 2 direct-construction tests above never exercised the container, so they never
		// caught the missing-registration regression — this test does.
		var cleanupService = provider.GetServices<IHostedService>()
			.OfType<SagaCleanupBackgroundService>()
			.Single();

		cleanupService.ShouldNotBeNull(
			"AddExcaliburSaga must register every dependency the SagaCleanupBackgroundService ctor needs " +
			"(including TimeProvider) so the hosted service resolves from a real container");
	}

	[Fact]
	public async Task ResolveAndRunSagaCleanupService_WithNoStoreRegistered_DoesNotThrow()
	{
		// Arrange — the SUPPORTED no-store shape: AddExcaliburSaga registers the SagaCleanupBackgroundService
		// (as an IHostedService) plus its TimeProvider/SagaOptions/ILogger dependencies, but NO ISagaStore is
		// registered (no AddInMemorySagaStore). The service resolves ISagaStore LAZILY per cleanup cycle, so
		// constructing and starting it without a store is a supported intermediate state — not a crash.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburSaga();

		using var provider = services.BuildServiceProvider();

		// Act + Assert (resolution) — the hosted-service graph materialises even with no store registered,
		// because the ctor takes IServiceProvider (not ISagaStore) and defers store resolution to run time.
		var cleanupService = provider.GetServices<IHostedService>()
			.OfType<SagaCleanupBackgroundService>()
			.Single();

		cleanupService.ShouldNotBeNull(
			"the cleanup service must resolve from a real container even when no ISagaStore is registered — " +
			"AddExcaliburSaga() without a store is a supported state");

		// Act + Assert (enabled-no-store runtime) — drive the enabled path with no store through the SAME
		// store-less container. Per SagaCleanupBackgroundService.ExecuteAsync, an enabled cycle that resolves
		// a null ISagaStore logs a clear error and BREAKS the loop (no throw, no purge). A FakeTimeProvider
		// fires the interval tick deterministically. Constructing directly lets us set EnableAutomaticCleanup
		// and inject the fake clock while still exercising the real no-store GetService<ISagaStore>() -> null
		// path against the container from AddExcaliburSaga.
		var timeProvider = new FakeTimeProvider(Origin);
		var options = new SagaOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMinutes(5),
			SagaRetentionPeriod = TimeSpan.FromDays(30),
		};
		using var enabledNoStoreService = new SagaCleanupBackgroundService(
			provider, MsOptions.Create(options), timeProvider, NullLogger<SagaCleanupBackgroundService>.Instance);

		// Start, fire an interval tick (store resolves to null -> log + stop), then stop — none of this throws.
		await Should.NotThrowAsync(async () =>
		{
			await enabledNoStoreService.StartAsync(CancellationToken.None);
			timeProvider.Advance(options.CleanupInterval);
			await Task.Yield();
			await enabledNoStoreService.StopAsync(CancellationToken.None);
		});
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> condition)
	{
		for (var i = 0; i < 200; i++)
		{
			if (await condition())
			{
				return;
			}

			await Task.Delay(10);
		}
	}

	private sealed class TestSaga : SagaState;
}
