// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;
using PubSubBatchOptions = Excalibur.Dispatch.Transport.Google.BatchOptions;
using PubSubMessageBatch = Excalibur.Dispatch.Transport.Google.MessageBatch;
using PubSubReceivedMessage = Google.Cloud.PubSub.V1.ReceivedMessage;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

/// <summary>
/// Binds the promise of <see cref="PubSubBatchOptions.ConcurrentBatchProcessors"/> for the parallel processor:
/// the consumer's downstream must never see more concurrent handler invocations than it configured,
/// however large the batch is.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ParallelBatchProcessorConcurrencyShould
{
	private const int ConfiguredConcurrency = 4;

	// Comfortably larger than the bound, so a fan-out that ignores the option cannot pass by accident.
	private const int BatchSize = 64;

	[Fact]
	public async Task NeverExceedTheConfiguredConcurrencyWhileProcessingABatch()
	{
		// Arrange
		var inFlight = 0;
		var peakInFlight = 0;

		using var metricsCollector = new BatchMetricsCollector(meterFactory: null, meterName: Guid.NewGuid().ToString("N"));
		using var processor = new ParallelBatchProcessor(
			MsOptions.Create(new PubSubBatchOptions { ConcurrentBatchProcessors = ConfiguredConcurrency }),
			async (message, ct) =>
			{
				var current = Interlocked.Increment(ref inFlight);

				// Record the high-water mark without a lock: retry until our sample is not higher
				// than the value already recorded.
				var observedPeak = Volatile.Read(ref peakInFlight);
				while (current > observedPeak)
				{
					var previous = Interlocked.CompareExchange(ref peakInFlight, current, observedPeak);
					if (previous == observedPeak)
					{
						break;
					}

					observedPeak = previous;
				}

				try
				{
					// Hold the slot long enough that an unbounded fan-out overlaps every message.
					await Task.Delay(25, ct).ConfigureAwait(false);
					return new object();
				}
				finally
				{
					_ = Interlocked.Decrement(ref inFlight);
				}
			},
			NullLogger<ParallelBatchProcessor>.Instance,
			metricsCollector);

		var batch = CreateBatch(BatchSize);

		// Act
		var result = await processor.ProcessAsync(batch, TestContext.Current.CancellationToken);

		// Assert
		peakInFlight.ShouldBeLessThanOrEqualTo(
			ConfiguredConcurrency,
			$"the batch held {BatchSize} messages and ConcurrentBatchProcessors was {ConfiguredConcurrency}, " +
			$"but {peakInFlight} handler invocations overlapped");

		// The bound must not cost the consumer any message: every one is still processed exactly once.
		result.SuccessfulMessages.Count.ShouldBe(BatchSize);
		result.FailedMessages.ShouldBeEmpty();
		result.SuccessfulMessages.Select(static m => m.MessageId).Distinct(StringComparer.Ordinal).Count().ShouldBe(BatchSize);
	}

	[Fact]
	public async Task ActuallyOverlapWorkRatherThanSerialiseIt()
	{
		// Liveness arm: a processor that simply ran the batch one message at a time would pass the
		// bound above vacuously. It must genuinely use the concurrency it was given.
		var inFlight = 0;
		var peakInFlight = 0;

		using var metricsCollector = new BatchMetricsCollector(meterFactory: null, meterName: Guid.NewGuid().ToString("N"));
		using var processor = new ParallelBatchProcessor(
			MsOptions.Create(new PubSubBatchOptions { ConcurrentBatchProcessors = ConfiguredConcurrency }),
			async (message, ct) =>
			{
				var current = Interlocked.Increment(ref inFlight);

				var observedPeak = Volatile.Read(ref peakInFlight);
				while (current > observedPeak)
				{
					var previous = Interlocked.CompareExchange(ref peakInFlight, current, observedPeak);
					if (previous == observedPeak)
					{
						break;
					}

					observedPeak = previous;
				}

				try
				{
					await Task.Delay(25, ct).ConfigureAwait(false);
					return new object();
				}
				finally
				{
					_ = Interlocked.Decrement(ref inFlight);
				}
			},
			NullLogger<ParallelBatchProcessor>.Instance,
			metricsCollector);

		_ = await processor.ProcessAsync(CreateBatch(BatchSize), TestContext.Current.CancellationToken);

		peakInFlight.ShouldBeGreaterThan(1, "the parallel processor must overlap messages, not run them one at a time");
	}

	private static PubSubMessageBatch CreateBatch(int messageCount)
	{
		var messages = new List<PubSubReceivedMessage>(messageCount);

		for (var i = 0; i < messageCount; i++)
		{
			messages.Add(new PubSubReceivedMessage
			{
				AckId = $"ack-{i}",
				Message = new PubsubMessage { MessageId = $"message-{i}" },
			});
		}

		return new PubSubMessageBatch(messages, "projects/test/subscriptions/test", messageCount);
	}
}
