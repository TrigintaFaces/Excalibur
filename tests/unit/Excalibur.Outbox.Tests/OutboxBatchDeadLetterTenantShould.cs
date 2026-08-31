// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// The dead-letter queue stores an entry under the AMBIENT tenant, as its own contract states, so whoever
/// files the entry has to have established that tenant. The single-message path did, by wrapping its whole
/// body in a scope. The batch path opened its scope inside the parallel lambda, deferred failures to a list,
/// and drained that list after the scope was disposed, so a dead letter from one tenant was filed under the
/// untenanted sentinel. Silently: the destination column is not nullable and the sentinel satisfies it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxBatchDeadLetterTenantShould
{
	private const string Tenant = "tenant-a";

	[Fact]
	public async Task FileTheDeadLetterUnderTheMessageTenant_WhenDrainedByTheBatchPath()
	{
		var store = new SingleMessageStore(Tenant);
		var dlq = new TenantCapturingDeadLetterQueue();

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 1,

			// > 1 selects ProcessBatchParallelAsync, which is the path that defers dead letters.
			BatchProcessing = { ParallelProcessingDegree = 2 },
		});

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			deadLetterQueue: dlq);

		processor.Init("batch-dlq-tenant-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		// Liveness first: an arm that never reached the queue would satisfy any assertion about what the
		// queue saw, so prove the dead letter was actually filed before judging the tenant it was filed under.
		dlq.EnqueueCount.ShouldBeGreaterThan(
			0,
			"the message must reach the dead-letter queue, or this arm proves nothing about the tenant it carries");

		dlq.ObservedTenant.ShouldBe(
			Tenant,
			"a dead letter drained by the batch path must be filed under the tenant of the message it came from, "
			+ "not under the untenanted sentinel left by a disposed scope");
	}

	/// <summary>Captures the ambient tenant at the moment the entry is filed, which is what the store reads.</summary>
	private sealed class TenantCapturingDeadLetterQueue : IDeadLetterQueue
	{
		public int EnqueueCount { get; private set; }

		public string? ObservedTenant { get; private set; }

		public Task<Guid> EnqueueAsync<T>(
			T message,
			DeadLetterReason reason,
			CancellationToken cancellationToken,
			Exception? exception = null,
			IDictionary<string, string>? metadata = null)
		{
			EnqueueCount++;
			ObservedTenant = TenantContextHolder.Current;
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

	/// <summary>Hands out one tenanted message, then nothing, so the drain terminates.</summary>
	private sealed class SingleMessageStore(string tenantId) : IOutboxStore, IDeadLetterableOutboxStore
	{
		private int _served;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			if (Interlocked.Exchange(ref _served, 1) == 1)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			}

			var message = new OutboundMessage
			{
				Id = "msg-" + Guid.NewGuid().ToString("N"),
				MessageType = "Excalibur.Outbox.Tests.NoSuchType, Excalibur.Outbox.Tests",
				Destination = "test-destination",
				Payload = [1, 2, 3],
				CreatedAt = DateTimeOffset.UtcNow,
				Status = OutboxStatus.Staged,

				// At or past MaxAttempts, so the first dispatch failure dead-letters rather than retries.
				RetryCount = 5,
				TenantId = tenantId,
			};

			return new ValueTask<IEnumerable<OutboundMessage>>([message]);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}
