// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
///     Tests for event-driven wait patterns using SemaphoreSlim as used in
///     MessageOutbox and OutboxProcessor for efficient signaling between
///     producers and consumers without polling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventDrivenWaitPatternShould
{
	[Fact]
	public async Task SignalBetweenProducerAndConsumer()
	{
		// Arrange - verify the signaling pattern used in MessageOutbox
		var semaphore = new SemaphoreSlim(0, int.MaxValue);
		var signalReceived = false;
		var consumerReady = new SemaphoreSlim(0, 1);
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		// Act - consumer waits, producer signals
		var consumerTask = Task.Run(async () =>
		{
			consumerReady.Release();
			await semaphore.WaitAsync(cts.Token).ConfigureAwait(false);
			signalReceived = true;
		});

		// Wait until consumer task is actually started and ready to receive
		// Uses SemaphoreSlim.WaitAsync with timeout instead of Task.Delay to avoid flakiness under load
		var ready = await consumerReady.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		ready.ShouldBeTrue("Consumer task should have signaled readiness");

		// Signal (as MessageOutbox.SignalNewMessage does)
		_ = semaphore.Release();

		await consumerTask.ConfigureAwait(false);

		// Assert
		signalReceived.ShouldBeTrue();
	}

	[Fact]
	public async Task RespectTimeoutWhenNoSignalReceived()
	{
		// Arrange - verify timeout behavior used in dispatch loop
		var semaphore = new SemaphoreSlim(0, int.MaxValue);

		// Act
		var result = await semaphore.WaitAsync(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		// Assert - should return false on timeout
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task QueueMultipleSignals()
	{
		// Arrange - verify multiple signals can be queued (as used in MessageOutbox)
		var semaphore = new SemaphoreSlim(0, int.MaxValue);

		// Act - send multiple signals
		_ = semaphore.Release();
		_ = semaphore.Release();
		_ = semaphore.Release();

		// Consume all signals
		var consumed = 0;
		while (await semaphore.WaitAsync(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false))
		{
			consumed++;
		}

		// Assert
		consumed.ShouldBe(3);
	}
}
