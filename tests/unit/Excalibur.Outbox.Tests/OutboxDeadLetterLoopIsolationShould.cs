// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// One failing dead-letter filing must not strand the dead letters queued behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The batch drain routes retry-exhausted messages to the dead-letter queue in a
/// <c>foreach</c> that ran with no handler. The queue is an EXTERNAL dependency, so a transient fault from
/// it is ordinary rather than exceptional — and one throw out of that loop abandoned every dead letter
/// still queued behind the message that faulted. Those messages stayed claimed and undead-lettered with
/// nothing recording that they had been skipped.
/// </para>
/// <para>
/// <b>WHY AN ARM RATHER THAN A READING.</b> The guard is four lines of <c>try</c>/<c>catch</c> inside a
/// loop, so it is invisible to any structural check and a reviewer confirms it by eye once and never
/// again. Nothing failed when it was absent — the suite was green across the whole defect, because no arm
/// ever put a fault in the one place that distinguishes the two shapes. This arm is that place.
/// </para>
/// <para>
/// <b>WHAT THIS ARM DOES NOT COVER, stated so the coverage is not read as wider than it is.</b> The same
/// unguarded throw also skipped the batch-completion block that follows the loop, so messages the cycle had
/// already DELIVERED were never marked sent and were delivered again on the next drain. That half is not
/// bound here: every message in this arm fails, so the run produces no successful delivery to mark, and
/// binding it needs a publisher that succeeds. It remains unbound — do not read a green here as evidence
/// about it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxDeadLetterLoopIsolationShould
{
	private const int MessageCount = 3;

	/// <summary>
	/// SAFETY. The queue throws once; the two dead letters behind it must still be filed.
	/// </summary>
	[Fact]
	public async Task StillFileTheRemainingDeadLetters_WhenTheQueueThrowsOnOneOfThem()
	{
		var store = new ExhaustedMessageStore(MessageCount);
		var dlq = new ThrowOnFirstEnqueueDeadLetterQueue();

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 16,
			ProducerBatchSize = MessageCount,
			ConsumerBatchSize = MessageCount,
			PerRunTotal = MessageCount,
			MaxAttempts = 1,

			// > 1 selects ProcessBatchParallelAsync, which is the path carrying the deferred dead-letter loop.
			BatchProcessing = { ParallelProcessingDegree = 2 },
		});

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			deadLetterQueue: dlq,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance);

		processor.Init("dead-letter-loop-isolation-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		// The drain itself must not propagate the queue's fault. A throw here is the defect in its loudest
		// form, so this call is part of the assertion rather than merely setup.
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		// LIVENESS, asserted before the safety claim: an arm where the drain never reached the dead-letter
		// path at all would satisfy "nothing was stranded" vacuously. Prove the loop ran the full length.
		dlq.EnqueueAttempts.ShouldBe(
			MessageCount,
			"every retry-exhausted message must be OFFERED to the dead-letter queue. Fewer attempts than "
			+ "messages means the loop stopped early — which is the defect: one failing filing stranding the "
			+ "dead letters queued behind it");

		// SAFETY. The one that threw is not marked; the two after it are. Asserting the exact set rather
		// than a count, because a count of 2 is also satisfied by marking the wrong two.
		store.DeadLetteredIds.Count.ShouldBe(
			MessageCount - 1,
			"the filing that threw must not be marked dead-lettered — it stays claimed so a later cycle "
			+ "retries it — and every filing that succeeded must be");

		store.DeadLetteredIds.ShouldNotContain(
			dlq.FirstMessageOffered!,
			"the message whose external filing threw must NOT be recorded as dead-lettered: the entry was "
			+ "never written, so marking it terminal would lose the message entirely");
	}

	/// <summary>
	/// LIVENESS CONTROL, and it is the arm that stops the safety claim above degenerating. Without a fault,
	/// all three must be filed AND marked — otherwise "the loop stops early" and "the loop never ran" are
	/// indistinguishable, and a processor that dead-lettered nothing at all would pass the safety arm.
	/// </summary>
	[Fact]
	public async Task FileAndMarkEveryDeadLetter_WhenTheQueueDoesNotThrow()
	{
		var store = new ExhaustedMessageStore(MessageCount);
		var dlq = new ThrowOnFirstEnqueueDeadLetterQueue { Armed = false };

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 16,
			ProducerBatchSize = MessageCount,
			ConsumerBatchSize = MessageCount,
			PerRunTotal = MessageCount,
			MaxAttempts = 1,
			BatchProcessing = { ParallelProcessingDegree = 2 },
		});

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			deadLetterQueue: dlq,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance);

		processor.Init("dead-letter-loop-liveness-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		dlq.EnqueueAttempts.ShouldBe(MessageCount, "the undisturbed path must offer every message");
		store.DeadLetteredIds.Count.ShouldBe(MessageCount, "and must mark every message it filed");
	}

	/// <summary>
	/// Throws out of the FIRST enqueue only, then behaves normally. A transient external fault, which is the
	/// ordinary case for a remote queue rather than an exotic one.
	/// </summary>
	private sealed class ThrowOnFirstEnqueueDeadLetterQueue : IDeadLetterQueue
	{
		private int _attempts;

		public bool Armed { get; init; } = true;

		public int EnqueueAttempts => _attempts;

		/// <summary>The message the injected fault was thrown for, so the arm can assert on that one.</summary>
		public string? FirstMessageOffered { get; private set; }

		public Task<Guid> EnqueueAsync<T>(
			T message,
			DeadLetterReason reason,
			CancellationToken cancellationToken,
			Exception? exception = null,
			IDictionary<string, string>? metadata = null)
		{
			var attempt = Interlocked.Increment(ref _attempts);

			if (attempt == 1)
			{
				FirstMessageOffered = (message as IOutboxMessage)?.MessageId;

				if (Armed)
				{
					throw new InvalidOperationException("dead-letter queue unavailable (injected)");
				}
			}

			return Task.FromResult(Guid.NewGuid());
		}

		public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
			CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null, int limit = 100) =>
			Task.FromResult<IReadOnlyList<DeadLetterEntry>>([]);

		public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult<DeadLetterEntry?>(null);

		public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken) => Task.FromResult(false);

		public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
			Task.FromResult(0L);
	}

	/// <summary>
	/// Serves a fixed number of retry-exhausted messages once, then nothing, so the drain terminates. The
	/// message type does not resolve, which is what drives each one down the dead-letter path.
	/// </summary>
	private sealed class ExhaustedMessageStore(int count) : IOutboxStore, IDeadLetterableOutboxStore
	{
		private readonly List<string> _deadLettered = [];
		private readonly Lock _gate = new();
		private int _served;

		public IReadOnlyList<string> DeadLetteredIds
		{
			get
			{
				lock (_gate)
				{
					return [.. _deadLettered];
				}
			}
		}

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			if (Interlocked.Exchange(ref _served, 1) == 1)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			}

			var messages = new List<OutboundMessage>(count);
			for (var i = 0; i < count; i++)
			{
				messages.Add(new OutboundMessage
				{
					Id = $"msg-{i:D2}",
					MessageType = "Excalibur.Outbox.Tests.NoSuchType, Excalibur.Outbox.Tests",
					Destination = "test-destination",
					Payload = [1, 2, 3],
					CreatedAt = DateTimeOffset.UtcNow,
					Status = OutboxStatus.Staged,

					// At or past MaxAttempts, so the first dispatch failure dead-letters rather than retries.
					RetryCount = 5,
				});
			}

			return new ValueTask<IEnumerable<OutboundMessage>>(messages);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				_deadLettered.Add(messageId);
			}

			return ValueTask.CompletedTask;
		}
	}
}
