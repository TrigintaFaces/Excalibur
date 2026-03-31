// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Outbox;

/// <summary>
/// Gap-fill tests for <see cref="PartitionedOutboxProcessorService"/> --
/// lifecycle, partition count, cancellation, null guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PartitionedOutboxProcessorShould
{
	[Fact]
	public async Task StartAndStopWithoutError()
	{
		// Arrange
		var partitioner = new HashOutboxPartitioner(4);
		var options = Options.Create(new OutboxPartitionOptions
		{
			PartitionCount = 4,
			ProcessorCountPerPartition = 1
		});
		var sp = new ServiceCollection().BuildServiceProvider();

		var service = new PartitionedOutboxProcessorService(
			partitioner, options, sp,
			NullLogger<PartitionedOutboxProcessorService>.Instance);

		// Act -- start + cancel quickly
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
		await service.StartAsync(cts.Token);
		await Task.Delay(600); // let it run briefly
		await service.StopAsync(CancellationToken.None);

		// Assert -- no exception means success
	}

	[Fact]
	public async Task RespectCancellationToken()
	{
		var partitioner = new HashOutboxPartitioner(2);
		var options = Options.Create(new OutboxPartitionOptions());
		var sp = new ServiceCollection().BuildServiceProvider();

		var service = new PartitionedOutboxProcessorService(
			partitioner, options, sp,
			NullLogger<PartitionedOutboxProcessorService>.Instance);

		using var cts = new CancellationTokenSource();
		await service.StartAsync(cts.Token);
		cts.Cancel();
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public void ThrowOnNullPartitioner()
	{
		var options = Options.Create(new OutboxPartitionOptions());
		var sp = new ServiceCollection().BuildServiceProvider();

		Should.Throw<ArgumentNullException>(() =>
			new PartitionedOutboxProcessorService(
				null!, options, sp,
				NullLogger<PartitionedOutboxProcessorService>.Instance));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		var partitioner = new HashOutboxPartitioner(4);
		var sp = new ServiceCollection().BuildServiceProvider();

		Should.Throw<ArgumentNullException>(() =>
			new PartitionedOutboxProcessorService(
				partitioner, null!, sp,
				NullLogger<PartitionedOutboxProcessorService>.Instance));
	}

	[Fact]
	public void ThrowOnNullServiceProvider()
	{
		var partitioner = new HashOutboxPartitioner(4);
		var options = Options.Create(new OutboxPartitionOptions());

		Should.Throw<ArgumentNullException>(() =>
			new PartitionedOutboxProcessorService(
				partitioner, options, null!,
				NullLogger<PartitionedOutboxProcessorService>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		var partitioner = new HashOutboxPartitioner(4);
		var options = Options.Create(new OutboxPartitionOptions());
		var sp = new ServiceCollection().BuildServiceProvider();

		Should.Throw<ArgumentNullException>(() =>
			new PartitionedOutboxProcessorService(
				partitioner, options, sp, null!));
	}
}
