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
		// Arrange
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
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

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await service.StartAsync(cts.Token);
		// Let it run briefly
		await Task.Delay(300);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- no exception, service stopped cleanly
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecutePartitions_CreatesScopedProcessors()
	{
		// Arrange -- track Init calls to verify per-partition scoping
		var initIds = new List<string>();
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.Init(A<string>._))
			.Invokes((string id) => { lock (initIds) { initIds.Add(id); } });
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
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await service.StartAsync(cts.Token);
		await Task.Delay(500);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- Init called with partition IDs
		initIds.Count.ShouldBeGreaterThanOrEqualTo(3);
		initIds.ShouldContain("partitioned-0-0");
		initIds.ShouldContain("partitioned-1-0");
		initIds.ShouldContain("partitioned-2-0");
	}

	[Fact]
	public async Task PartitionedMode_DispatchesMessages()
	{
		// Arrange
		var dispatchCount = 0;
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				Interlocked.Increment(ref dispatchCount);
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
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await service.StartAsync(cts.Token);
		await Task.Delay(500);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- dispatching happened across partitions
		dispatchCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task PartitionedMode_ContinuesAfterException()
	{
		// Arrange -- first call throws, subsequent succeed
		var callCount = 0;
		var processor = A.Fake<IOutboxProcessor>();
		_ = A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				if (n == 1) throw new InvalidOperationException("Test failure");
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
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		await service.StartAsync(cts.Token);
		await Task.Delay(1000);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- service recovered after the error
		callCount.ShouldBeGreaterThan(1);
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
		// Arrange -- gate says "don't process"
		var gate = A.Fake<IProcessingGate>();
		A.CallTo(() => gate.ShouldProcess).Returns(false);

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
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		await service.StartAsync(cts.Token);
		await Task.Delay(300);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- processor never called because gate blocked
		A.CallTo(() => processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}