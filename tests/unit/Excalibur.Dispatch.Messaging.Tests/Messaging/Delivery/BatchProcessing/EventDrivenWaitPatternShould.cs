// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
///     Tests for event-driven wait patterns using SemaphoreSlim as used in
///     MessageOutbox and OutboxProcessor for efficient signaling between
///     producers and consumers without polling.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class EventDrivenWaitPatternShould
{
	[Fact]
	public async Task SignalBetweenProducerAndConsumer()
	{
		// Arrange - verify the signaling pattern used in MessageOutbox
		var semaphore = new SemaphoreSlim(0, int.MaxValue);
		var signalReceived = false;
		var consumerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		// No deadline. The consumer below waits on this token, and the wait after it allows 30 seconds
		// scaled -- 90 on CI -- for the consumer to become ready. A 5-second deadline here therefore
		// cancelled the consumer while the test was still waiting for it, faulting consumerTask and
		// failing on the final await. The inner deadline wins and the outer wait becomes decoration.
		//
		// The comment below already notes this test was made robust "to avoid flakiness under load" by
		// replacing a Task.Delay. That fixed one source and left this one, which is the same defect
		// wearing a different primitive.
		//
		// Nothing needs a deadline: semaphore.Release() frees the consumer deterministically, and the
		// harness's --blame-hang-timeout catches a genuine hang with a dump that names what is stuck.
		using var cts = new CancellationTokenSource();

		// Act - consumer waits, producer signals
		var consumerTask = Task.Run(async () =>
		{
			consumerReady.TrySetResult();
			await semaphore.WaitAsync(cts.Token).ConfigureAwait(false);
			signalReceived = true;
		});

		// Wait until consumer task is actually started and ready to receive
		// Uses SemaphoreSlim.WaitAsync with timeout instead of Task.Delay to avoid flakiness under load
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			consumerReady.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

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
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act & Assert - should cancel when timeout token fires
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await semaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public void QueueMultipleSignals()
	{
		// Arrange - verify multiple signals can be queued (as used in MessageOutbox)
		var semaphore = new SemaphoreSlim(0, int.MaxValue);

		// Act - send multiple signals
		_ = semaphore.Release();
		_ = semaphore.Release();
		_ = semaphore.Release();

		// Consume all signals
		var consumed = 0;
		#pragma warning disable RS0030 // bd-c36hwe: sync-over-async debt (migrate to await/poll)
		while (semaphore.Wait(0))
		#pragma warning restore RS0030
		{
			consumed++;
		}

		// Assert
		consumed.ShouldBe(3);
	}
}
