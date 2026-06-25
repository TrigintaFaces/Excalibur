// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Concurrency regression lock for <see cref="TransportAdapterHostedService"/> (Sprint 846, Lane B, eirmd5).
/// </summary>
/// <remarks>
/// <para>
/// <c>_startedAdapters</c> (a <see cref="List{T}"/>) is guarded by <c>_lock</c> on the
/// <see cref="ITransportLifecycleManager"/> paths (StartTransportAsync/StopTransportAsync/getters) but was
/// mutated WITHOUT the lock on the <see cref="Microsoft.Extensions.Hosting.IHostedService"/> path —
/// <c>StartAsync</c> (<c>_startedAdapters.Add</c>) and <c>StopStartedAdaptersAsync</c> (index iteration +
/// <c>Clear</c>). <see cref="List{T}"/> is not thread-safe, so a consumer driving the lifecycle-manager
/// (graceful degradation / dynamic scaling) concurrently with host startup/shutdown can corrupt the list,
/// throw a collection-corruption exception, or miss adapters on shutdown.
/// </para>
/// <para>
/// These locks are <b>author≠impl</b> (TestsDeveloper authors; PlatformDeveloper owns the fix) and are
/// RED on the pre-fix unlocked host path, GREEN once all <c>_startedAdapters</c> access is synchronized.
/// They are deterministic (no wall-clock <c>sleep</c>): a <see cref="Barrier"/> maximizes thread overlap
/// and a bounded iteration count makes the data race reliably observable.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TransportAdapterHostedServiceConcurrencyShould
{
	private const int AdapterCount = 40;
	private const int WorkerCount = 16;
	private const int IterationsPerWorker = 100;

	private static TransportAdapterHostedService CreateSut(out string[] names)
	{
		var transportNames = new string[AdapterCount];
		var registry = A.Fake<ITransportRegistry>();

		for (var i = 0; i < AdapterCount; i++)
		{
			var name = $"transport-{i}";
			transportNames[i] = name;

			var adapter = A.Fake<ITransportAdapter>(o => o.Implements<ITransportAdapterLifecycle>());
			A.CallTo(() => adapter.IsRunning).Returns(false);
			A.CallTo(() => registry.GetTransportAdapter(name)).Returns(adapter);
		}

		A.CallTo(() => registry.GetTransportNames()).Returns(transportNames);

		names = transportNames;
		return new TransportAdapterHostedService(
			registry,
			Microsoft.Extensions.Options.Options.Create(new TransportAdapterHostedServiceOptions()),
			A.Fake<IServiceProvider>(),
			NullLogger<TransportAdapterHostedService>.Instance);
	}

	[Fact]
	public async Task NotCorruptStartedAdapters_WhenHostStartStopRunConcurrently()
	{
		// Arrange -- many workers driving the IHostedService start/stop paths in lock-step.
		var sut = CreateSut(out _);
		using var barrier = new Barrier(WorkerCount);

		var workers = new Task[WorkerCount];
		for (var w = 0; w < WorkerCount; w++)
		{
			workers[w] = Task.Run(async () =>
			{
				// Maximize overlap of the unlocked Add-loop (StartAsync) with the unlocked
				// index-iteration + Clear (StopAsync -> StopStartedAdaptersAsync) across threads.
				barrier.SignalAndWait();

				for (var i = 0; i < IterationsPerWorker; i++)
				{
					await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
					await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
				}
			});
		}

		// Act + Assert -- concurrent host start/stop must not corrupt the shared list nor throw.
		// RED on the pre-fix unlocked host path (concurrent Add/Clear/index access on List<T> throws
		// ArgumentOutOfRangeException / IndexOutOfRangeException / InvalidOperationException).
		await Should.NotThrowAsync(() => Task.WhenAll(workers)).ConfigureAwait(false);
	}

	[Fact]
	public async Task NotCorruptStartedAdapters_WhenHostPathRacesLifecycleManager()
	{
		// Arrange -- host start/stop racing the ITransportLifecycleManager start/stop on the SAME names.
		// The lifecycle-manager path locks; the host path did not -> the lock provides no mutual
		// exclusion because the other side ignores it (a one-sided lock is no lock).
		var sut = CreateSut(out var names);
		using var barrier = new Barrier(WorkerCount);

		var workers = new Task[WorkerCount];
		for (var w = 0; w < WorkerCount; w++)
		{
			var workerIndex = w;
			workers[w] = Task.Run(async () =>
			{
				barrier.SignalAndWait();

				for (var i = 0; i < IterationsPerWorker; i++)
				{
					if (workerIndex % 2 == 0)
					{
						await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
						await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
					}
					else
					{
						var name = names[(workerIndex + i) % names.Length];
						await sut.StartTransportAsync(name, CancellationToken.None).ConfigureAwait(false);
						await sut.StopTransportAsync(name, CancellationToken.None).ConfigureAwait(false);
					}
				}
			});
		}

		// Act + Assert -- mixed host + lifecycle-manager access must not throw or tear the list.
		await Should.NotThrowAsync(() => Task.WhenAll(workers)).ConfigureAwait(false);
	}
}
