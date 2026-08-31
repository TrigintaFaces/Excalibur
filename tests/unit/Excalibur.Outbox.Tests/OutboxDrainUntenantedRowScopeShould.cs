// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// A drain re-establishes each row's own tenant before marking it. The term it re-establishes is read back
/// off the row, so it must go through the total store-read conversion rather than being passed raw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect.</b> The drain passed the stored tenant column straight to
/// <c>TenantContextHolder.BeginScope</c>. For an untenanted row that column is <see langword="null"/>, and
/// a null argument CLEARS the ambient rather than binding the reserved untenanted term. A cleared ambient
/// does not mean "this row has no tenant" — it means "no tenant was established", which is the state a
/// multi-tenant deployment fails closed on. So the mark that follows a successful dispatch threw
/// <see cref="TenantRequiredException"/> with the handler already run, and the batch was discarded. Three
/// call sites carried a comment asserting the opposite ("BeginScope(null) is the prior non-tenant
/// behaviour"); one asserted it as a reason for choosing null over the sentinel.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The store here does not inspect the ambient string and judge it. It calls the
/// framework's own <see cref="TenantScope.FromContext(ITenantContext)"/> — the conversion every
/// tenant-aware store routes through — over a context that reports the raw ambient, so the arm fails the
/// way production fails rather than the way a hand-written assertion decides to. The two arms fail under
/// different mutations: <see cref="BindTheRowsOwnTenant_WhenTheRowIsTenanted"/> is RED for a fix that
/// always binds the sentinel, and <see cref="BindTheUntenantedTerm_WhenTheStoredTenantIsAbsent"/> is RED
/// for the raw-null original. Neither alone is sufficient.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxDrainUntenantedRowScopeShould
{
	[Fact]
	public async Task BindTheUntenantedTerm_WhenTheStoredTenantIsAbsent()
	{
		var store = await DrainAsync(storedTenantId: null).ConfigureAwait(false);

		// Liveness first: an arm that never reached the mark would satisfy any assertion about what the mark
		// observed, so prove the drain actually got there before judging the tenant it got there under.
		store.MarkCount.ShouldBeGreaterThan(
			0,
			"the drain must reach the store mark, or this arm proves nothing about the scope it marks under");

		store.FailedClosed.ShouldBeNull(
			"a row that belongs to no tenant must bind the reserved untenanted term. Cleared ambient is "
			+ "'no tenant was established', which a multi-tenant store fails closed on — after the handler ran");

		store.ObservedScope.ShouldBe(
			TenantScope.Untenanted,
			"an untenanted row resolves the untenanted partition, not an absent one");
	}

	[Fact]
	public async Task BindTheRowsOwnTenant_WhenTheRowIsTenanted()
	{
		var store = await DrainAsync(storedTenantId: "tenant-a").ConfigureAwait(false);

		store.MarkCount.ShouldBeGreaterThan(0, "the drain must reach the store mark");

		store.FailedClosed.ShouldBeNull("a tenanted row resolves its own tenant and never fails closed");

		store.ObservedScope.ShouldBe(
			TenantScope.Scoped("tenant-a"),
			"the mark must land under the row's own tenant — a fix that folded every row onto the untenanted "
			+ "term would file every tenant's marks in the untenanted partition");
	}

	private static async Task<TenantObservingStore> DrainAsync(string? storedTenantId)
	{
		var store = new TenantObservingStore(storedTenantId);

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 5,
			BatchProcessing = { ParallelProcessingDegree = 1 },
		});

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null);

		processor.Init("drain-untenanted-scope-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		return store;
	}

	/// <summary>
	/// Reports the raw ambient tenant, exactly as the framework's default context does. Deliberately not the
	/// conformance context, which folds an absent ambient onto the sentinel and would hide the defect.
	/// </summary>
	private sealed class RawAmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	/// <summary>
	/// Hands out one message, then nothing so the drain terminates, and resolves its tenant partition at the
	/// moment of the mark the way a real multi-tenant store does.
	/// </summary>
	private sealed class TenantObservingStore(string? tenantId) : IOutboxStore, IDeadLetterableOutboxStore
	{
		private int _served;

		public int MarkCount { get; private set; }

		public TenantScope? ObservedScope { get; private set; }

		public TenantRequiredException? FailedClosed { get; private set; }

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
				RetryCount = 0,
				TenantId = tenantId,
			};

			return new ValueTask<IEnumerable<OutboundMessage>>([message]);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
		{
			Observe();
			return ValueTask.CompletedTask;
		}

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			Observe();
			return ValueTask.CompletedTask;
		}

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		/// <summary>
		/// The drain refuses to dead-letter through a store that cannot record the terminal status, so the
		/// fixture implements it: an unresolvable message type is poison and reaches this path before the
		/// retry ceiling. Observed like the other marks, since it is a store write under the same scope.
		/// </summary>
		public ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
		{
			Observe();
			return ValueTask.CompletedTask;
		}

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		/// <summary>
		/// Resolves the tenant partition through the framework's own conversion, which is what every
		/// tenant-aware store does on every statement it emits — including the fail-closed rejection.
		/// </summary>
		private void Observe()
		{
			MarkCount++;
			try
			{
				ObservedScope = TenantScope.FromContext(new RawAmbientTenantContext());
			}
			catch (TenantRequiredException ex)
			{
				FailedClosed = ex;
			}
		}
	}
}
