// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Health;
using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;
using Excalibur.Outbox.Processing;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.Partitioning;

/// <summary>
/// Tests for <see cref="OutboxBackgroundService"/> in partitioned mode
/// (when an <see cref="IOutboxPartitioner"/> is injected).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxBackgroundServicePartitionedShould
{
	[Fact]
	public async Task StartAndStop_InPartitionedMode_WithoutError()
	{
		// Arrange -- signal on first dispatch so we know the partition loop actually ran.
		var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() => dispatched.TrySetResult())
			.Returns(Task.FromResult(0));

		var services = new ServiceCollection();
		services.AddSingleton(processor);
		var sp = services.BuildServiceProvider();

		var partitioner = new HashOutboxPartitioner(2);
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var service = new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(), options, sp,
			NullLogger<OutboxBackgroundService>.Instance,
			partitioner: partitioner);

		// Act -- start with a non-expiring token; StopAsync drives shutdown deterministically.
		await service.StartAsync(CancellationToken.None);
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				dispatched.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- no exception, service ran and stopped cleanly
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecutePartitions_CreatesScopedProcessors()
	{
		// Arrange -- track Init calls to verify per-partition scoping; signal when all 3 are seen.
		var initIds = new List<string>();
		var allPartitionsInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.Init(A<string>._))
			.Invokes((string id) =>
			{
				lock (initIds)
				{
					initIds.Add(id);
					if (initIds.Count >= 3)
					{
						_ = allPartitionsInit.TrySetResult();
					}
				}
			});
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(0));

		var services = new ServiceCollection();
		services.AddTransient<IOutboxProcessor>(_ => processor);
		var sp = services.BuildServiceProvider();

		var partitioner = new HashOutboxPartitioner(3);
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var service = new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(), options, sp,
			NullLogger<OutboxBackgroundService>.Instance,
			partitioner: partitioner);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				allPartitionsInit.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- Init called once per partition
		List<string> snapshot;
		lock (initIds)
		{
			snapshot = [.. initIds];
		}

		snapshot.Count.ShouldBeGreaterThanOrEqualTo(3);
		snapshot.ShouldContain("partitioned-0-0");
		snapshot.ShouldContain("partitioned-1-0");
		snapshot.ShouldContain("partitioned-2-0");
	}

	[Fact]
	public async Task PartitionedMode_DispatchesMessages()
	{
		// Arrange -- signal on first dispatch.
		var dispatchCount = 0;
		var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = Interlocked.Increment(ref dispatchCount);
				_ = dispatched.TrySetResult();
				return Task.FromResult(1);
			});

		var services = new ServiceCollection();
		services.AddTransient<IOutboxProcessor>(_ => processor);
		var sp = services.BuildServiceProvider();

		var partitioner = new HashOutboxPartitioner(2);
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var service = new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(), options, sp,
			NullLogger<OutboxBackgroundService>.Instance,
			partitioner: partitioner);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				dispatched.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- dispatching happened across partitions
		Volatile.Read(ref dispatchCount).ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task PartitionedMode_ContinuesAfterException()
	{
		// Arrange -- first call throws, subsequent succeed; signal once recovered (2nd call).
		var callCount = 0;
		var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				if (n == 1) throw new InvalidOperationException("Test failure");
				_ = recovered.TrySetResult();
				return Task.FromResult(0);
			});

		var services = new ServiceCollection();
		services.AddTransient<IOutboxProcessor>(_ => processor);
		var sp = services.BuildServiceProvider();

		var partitioner = new HashOutboxPartitioner(1);
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var service = new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(), options, sp,
			NullLogger<OutboxBackgroundService>.Instance,
			partitioner: partitioner);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				recovered.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- service recovered after the error (>= 2 calls)
		Volatile.Read(ref callCount).ShouldBeGreaterThan(1);
	}

	[Fact]
	public void Constructor_ThrowsOnNullServiceProvider()
	{
		Should.Throw<ArgumentNullException>(() => new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(),
			Options.Create(new OutboxProcessingOptions()),
			null!,
			NullLogger<OutboxBackgroundService>.Instance));
	}

	[Fact]
	public async Task PartitionedMode_RespectsProcessingGate()
	{
		// Arrange -- gate says "don't process". Signal when the gate is actually consulted so
		// the negative assertion is meaningful (the loop ran and checked the gate, rather than
		// the assertion passing vacuously because the loop never started).
		var gateChecked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var gate = A.Fake<IProcessingGate>();
		_ = A.CallTo(() => gate.ShouldProcess)
			.Invokes(() => gateChecked.TrySetResult())
			.Returns(false);

		var processor = A.Fake<IOutboxProcessor>();
		var services = new ServiceCollection();
		services.AddTransient<IOutboxProcessor>(_ => processor);
		var sp = services.BuildServiceProvider();

		var partitioner = new HashOutboxPartitioner(1);
		var options = Options.Create(new OutboxProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var service = new OutboxBackgroundService(
			A.Fake<IOutboxPublisher>(), options, sp,
			NullLogger<OutboxBackgroundService>.Instance,
			gate: gate,
			partitioner: partitioner);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				gateChecked.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- processor never called because gate blocked
		A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}