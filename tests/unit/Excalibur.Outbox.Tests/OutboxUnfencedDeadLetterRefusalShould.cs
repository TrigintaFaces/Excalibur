// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// A dead-letter transition the UNFENCED store refused must withdraw the dead-letter entry the drain
/// already wrote.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this prevents is silent and it corrupts an operator's own recovery tool.</b> The drain
/// writes the dead-letter entry FIRST and marks the outbox row second. When the store refuses that mark —
/// the row is gone, already terminal, or leased to another drain — and the refusal is not visible, the
/// message is left simultaneously delivered by whoever holds the claim and sitting unreplayed in the
/// queue. The shipped redrive selects pending entries by filter, so draining the queue re-executes a
/// message that already succeeded, from our own bookkeeping rather than any fault of the operator's.
/// </para>
/// <para>
/// <b>Why it could not be caught before.</b> The unfenced contract returned a bare <c>ValueTask</c>, so
/// "I refused" and "I applied it" were the same observation and the processor returned <c>Applied</c>
/// unconditionally. The fenced branch had carried the compensation, and a comment describing this exact
/// hazard, for as long as the unfenced branch had been unable to see it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxUnfencedDeadLetterRefusalShould
{
	/// <summary>
	/// SAFETY: every refusal withdraws the entry.
	/// </summary>
	/// <remarks>
	/// Theory rather than a single arm because the refusals arrive by different routes — a vanished row, a
	/// row another drain finished, a row a sibling re-claimed — and a fix that handled only the one the
	/// author pictured would pass a single-value arm.
	/// </remarks>
	[Theory]
	[InlineData(OutboxCompletionOutcome.MessageNotFound)]
	[InlineData(OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.Unknown)]
	public async Task Withdraw_the_dead_letter_entry_when_the_store_refuses(OutboxCompletionOutcome refusal)
	{
		var (queue, entryId) = CapturingQueue();

		await DrainAsync(refusal, queue);

		A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(entryId, A<CancellationToken>._))
			.MustHaveHappened();
	}

	/// <summary>
	/// LIVENESS: an applied transition keeps the entry.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above and not optional. Without it the cheapest way to pass that theory is a
	/// drain that withdraws every entry it ever writes, which would delete the dead-letter records this
	/// subsystem exists to produce — a worse defect than the one being fixed, and invisible to a
	/// refusal-only suite.
	/// </remarks>
	[Fact]
	public async Task Keep_the_dead_letter_entry_when_the_store_applies_the_transition()
	{
		var (queue, entryId) = CapturingQueue();

		await DrainAsync(OutboxCompletionOutcome.Applied, queue);

		A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(entryId, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A dead-letter queue that also answers the admin surface, so the withdrawal has somewhere to land.
	/// </summary>
	private static (IDeadLetterQueue Queue, Guid EntryId) CapturingQueue()
	{
		var entryId = Guid.NewGuid();
		var queue = A.Fake<IDeadLetterQueue>(o => o.Implements<IDeadLetterQueueAdmin>());

		A.CallTo(queue)
			.Where(call => call.Method.Name == nameof(IDeadLetterQueue.EnqueueAsync))
			.WithReturnType<Task<Guid>>()
			.Returns(entryId);

		A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(true);

		return (queue, entryId);
	}

	private static async Task DrainAsync(OutboxCompletionOutcome outcome, IDeadLetterQueue queue)
	{
		MessageTypeRegistry.RegisterType<UndispatchableProbe>();

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,

			// One attempt, so the single undispatchable message exhausts its budget on this drain and
			// reaches the dead-letter path rather than being scheduled for a retry.
			MaxAttempts = 1,
			BatchProcessing = { ParallelProcessingDegree = 1 },
		});

		await using var processor = new OutboxProcessor(
			options,
			new RefusingDeadLetterStore(outcome),
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance,
			deadLetterQueue: queue);

		processor.Init("unfenced-dead-letter-refusal-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token);
	}

	/// <summary>
	/// Hands out one undispatchable message, then nothing, and answers the UNFENCED dead-letter report
	/// with whatever outcome the arm is driving.
	/// </summary>
	/// <remarks>
	/// Deliberately does NOT implement <c>IFencedDeadLetterableOutboxStore</c>. Capability discovery is by
	/// instance type, so a fixture that also advertised the fenced surface would send the drain down the
	/// fenced branch — which already compensated — and the arm would pass without ever reaching the code
	/// under test.
	/// </remarks>
	private sealed class RefusingDeadLetterStore(OutboxCompletionOutcome outcome)
		: IOutboxStore, IDeadLetterableOutboxStore
	{
		private int _served;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, CancellationToken cancellationToken)
		{
			if (Interlocked.Increment(ref _served) > 1)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			}

			var message = new OutboundMessage
			{
				Id = "msg-" + Guid.NewGuid().ToString("N"),
				MessageType = typeof(UndispatchableProbe).FullName!,
				Destination = "test-destination",
				Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
				CreatedAt = DateTimeOffset.UtcNow,
				Status = OutboxStatus.Staged,
				RetryCount = 0,
				DispatcherId = "claim-alpha",
			};

			return new ValueTask<IEnumerable<OutboundMessage>>([message]);
		}

		public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
			string messageId, string reason, int attemptCount, CancellationToken cancellationToken)
			=> ValueTask.FromResult(outcome);

		public ValueTask MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(
			IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}
