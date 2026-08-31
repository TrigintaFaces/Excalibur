// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Drain-level locks for the retry drain's own invariants. The drain is a second, independent processor of
/// the same rows as the receive path, so the store's conformance kit does not cover it: the kit's
/// estate-wide-read arm proves the store's <em>read</em> ignores the ambient tenant, and says nothing about
/// the scope the drain performs its <em>writes</em> under, or whether it performs them at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>S2 — exactly one committed finalize, under the entry's own key AND its own tenant.</b> The drain
/// enters a per-entry tenant scope around the dispatch and disposes it when the dispatch returns; every
/// mark runs after that. On a multi-tenant host the ambient tenant is then unresolved, which every
/// tenant-aware store fails closed on, so the mark raises a tenant-required error with the handler already
/// run — and the batch is discarded. <see cref="TenantResolvingInboxStore"/> reproduces that by resolving
/// the partition through the framework's own <see cref="TenantScope.FromContext(ITenantContext)"/>, the
/// conversion a real provider routes every statement through, rather than by inspecting a string and
/// deciding for itself what should have happened.
/// </para>
/// <para>
/// <b>L2 — every failed entry reaches a terminal state within a bounded number of attempts.</b> The drain
/// selects between two consumer paths on its parallel-processing degree. The shipped default of <c>1</c>
/// selects a path that dispatches and then writes nothing at all — no processed mark, no failure mark, no
/// attempt, no dead-letter. <see cref="RecordAnOutcome_OnTheDefaultParallelProcessingDegree"/> leaves the
/// degree at its default deliberately: an arm that raises it tests the branch a default deployment does
/// not run.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxProcessorDrainInvariantsShould
{
	private const string ProbeMessageId = "inbox-drain-invariants";
	private const string EntryTenant = "tenant-a";

	private static readonly string HandlerTypeName =
		typeof(DrainProbeMessage).FullName ?? typeof(DrainProbeMessage).Name;

	private static readonly string MessageTypeName = typeof(DrainProbeMessage).Name;

	[Fact]
	public void UseANamespacedProbeType_SoTheTwoKeyColumnsActuallyDiffer()
	{
		// Positive control, mirroring the finalize-key lock: a global-namespace probe has FullName == Name,
		// which would let a finalize under the wrong column pass every arm below.
		HandlerTypeName.ShouldNotBe(
			MessageTypeName,
			"the probe must be namespaced, or the two key columns cannot diverge and the arms are vacuous");
	}

	[Fact]
	public async Task RecordAnOutcome_OnTheDefaultParallelProcessingDegree()
	{
		// L2. Degree left at its shipped default (1) ON PURPOSE — this is the path a default deployment runs.
		await using var harness = await DrainHarness
			.CreateAsync(entryTenantId: EntryTenant, parallelProcessingDegree: null, dispatchSucceeds: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		// Liveness first: an arm that never dispatched would satisfy any claim about what was recorded.
		A.CallTo(() => harness.Dispatcher.DispatchAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened();

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a drain that dispatches a message and records no outcome re-selects the same entry on every "
			+ "pass, so the handler runs an unbounded number of times and the entry never reaches a terminal "
			+ "state. The re-admission age floor cannot bound it either: it compares a timestamp this path "
			+ "never writes, so it is permanently satisfied");
	}

	[Fact]
	public async Task FinalizeUnderTheEntrysOwnTenant_WhenDrainedOnAMultiTenantHost()
	{
		// S2. Degree above 1 so this arm reaches the finalizing branch — it is isolating the tenant the
		// finalize runs under, not whether one happens (that is the arm above).
		await using var harness = await DrainHarness
			.CreateAsync(entryTenantId: EntryTenant, parallelProcessingDegree: 2, dispatchSucceeds: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.Store.MarkCount.ShouldBeGreaterThan(
			0,
			"the drain must reach a store mark, or this arm proves nothing about the scope it marks under");

		harness.Store.FailedClosed.ShouldBeNull(
			"the mark must run inside the entry's own tenant scope. Run after that scope is disposed, the "
			+ "ambient tenant is unresolved and a multi-tenant store fails closed — with the handler already "
			+ "run, and the rest of the batch discarded, on every pass");

		harness.Store.ObservedScopes.ShouldAllBe(
			scope => scope == TenantScope.Scoped(EntryTenant),
			"every mark for this entry must be committed under the tenant its dispatch ran under");

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processed, "a successfully dispatched entry must reach its terminal state");
	}

	[Fact]
	public async Task FinalizeUnderTheUntenantedTerm_WhenTheEntryBelongsToNoTenant()
	{
		// The second direction. A fix that bound the entry's tenant only when one is present would leave an
		// untenanted row clearing the ambient, which fails closed for the same reason.
		await using var harness = await DrainHarness
			.CreateAsync(entryTenantId: null, parallelProcessingDegree: 2, dispatchSucceeds: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.Store.MarkCount.ShouldBeGreaterThan(0, "the drain must reach a store mark");

		harness.Store.FailedClosed.ShouldBeNull(
			"an untenanted row binds the reserved untenanted term. A cleared ambient means 'no tenant was "
			+ "established', which is the state a multi-tenant store refuses");

		harness.Store.ObservedScopes.ShouldAllBe(
			scope => scope == TenantScope.Untenanted,
			"an untenanted entry resolves the untenanted partition, not an absent one");
	}

	[Fact]
	public async Task CountADeliveryFailureAgainstTheBreakerExactlyOnce()
	{
		// The breaker records the outcome itself, inside ExecuteAsync, and rethrows. A caller that also
		// records it counts one delivery failure twice, so a breaker configured to open after N consecutive
		// failures opens after N/2 and sheds a healthy dependency at half the tolerance its consumer
		// configured. This cannot be found by asserting on a failure total: a harness that mirrors the
		// double-count agrees with production and passes. The arm therefore separates the two recorders and
		// asserts on WHO recorded, not on how many were recorded in aggregate.
		var breaker = new OutcomeAttributingCircuitBreaker();

		await using var harness = await DrainHarness
			.CreateAsync(
				entryTenantId: EntryTenant,
				parallelProcessingDegree: 2,
				dispatchSucceeds: false,
				circuitBreaker: breaker)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		// Liveness first, and it is the arm that catches the over-correction: a drain that stopped routing
		// dispatch through the breaker at all, or stopped dispatching, would satisfy every safety claim
		// below while counting nothing. The breaker must have seen exactly one failed execution.
		breaker.FailuresObservedInsideExecute.ShouldBe(
			1,
			"the drain must route its dispatch through the breaker, and one failed delivery is one failed "
			+ "execution -- a breaker that observes none has been bypassed, not fixed");

		// Safety.
		breaker.OutcomesRecordedByTheCaller.ShouldBe(
			0,
			"ExecuteAsync has already recorded this outcome by the time control returns to the drain. "
			+ "Recording it again double-counts the failure, and also overrides the breaker's own decision "
			+ "about which exceptions count -- the explicit recorders are for outcomes observed OUTSIDE "
			+ "ExecuteAsync, not a supplement to it");
	}

	[Fact]
	public async Task CountASuccessfulDeliveryAgainstTheBreakerExactlyOnce()
	{
		// The success direction. A fix that removed only the failure recorder would leave the success path
		// double-counting, which resets a half-open circuit's probe accounting twice.
		var breaker = new OutcomeAttributingCircuitBreaker();

		await using var harness = await DrainHarness
			.CreateAsync(
				entryTenantId: EntryTenant,
				parallelProcessingDegree: 2,
				dispatchSucceeds: true,
				circuitBreaker: breaker)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		breaker.SuccessesObservedInsideExecute.ShouldBe(
			1, "the drain must route its dispatch through the breaker");

		breaker.OutcomesRecordedByTheCaller.ShouldBe(
			0, "ExecuteAsync has already recorded the success by the time control returns to the drain");
	}

	/// <summary>
	/// An <see cref="ICircuitBreakerPolicy"/> that records the outcome inside <c>ExecuteAsync</c> -- as both
	/// shipped implementations do -- while counting the explicit recorders separately, so an arm can tell
	/// WHICH of the two recorded a given outcome. A fake that merged them into one total would agree with
	/// the double-count and pass.
	/// </summary>
	private sealed class OutcomeAttributingCircuitBreaker : ICircuitBreakerPolicy
	{
		public int SuccessesObservedInsideExecute { get; private set; }

		public int FailuresObservedInsideExecute { get; private set; }

		public int OutcomesRecordedByTheCaller { get; private set; }

		public CircuitState State => CircuitState.Closed;

		public async Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			CancellationToken cancellationToken)
		{
			try
			{
				var result = await action(cancellationToken).ConfigureAwait(false);
				SuccessesObservedInsideExecute++;
				return result;
			}
			catch (Exception)
			{
				FailuresObservedInsideExecute++;
				throw;
			}
		}

		public void RecordSuccess() => OutcomesRecordedByTheCaller++;

		public void RecordFailure(Exception? exception = null) => OutcomesRecordedByTheCaller++;

		public void Reset()
		{
		}
	}

	/// <summary>Hands the processor one breaker, whatever message type it asks for.</summary>
	private sealed class SingleBreakerRegistry(ICircuitBreakerPolicy breaker) : ITransportCircuitBreakerRegistry
	{
		public ICircuitBreakerPolicy GetOrCreate(string transportName) => breaker;

		public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options) => breaker;

		public ICircuitBreakerPolicy? TryGet(string transportName) => breaker;
	}

	/// <summary>Reports the raw ambient tenant, exactly as the framework's default context does.</summary>
	private sealed class RawAmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	/// <summary>
	/// Delegates to a real inbox store, and resolves its tenant partition on every mark the way a real
	/// multi-tenant provider does — through the framework's own conversion, including its fail-closed arm.
	/// </summary>
	private sealed class TenantResolvingInboxStore(IInboxStore inner) : IInboxStore, IInboxStoreAdmin
	{
		private readonly List<TenantScope> _observed = [];

		public int MarkCount { get; private set; }

		public TenantRequiredException? FailedClosed { get; private set; }

		public IReadOnlyList<TenantScope> ObservedScopes => _observed;

		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken) =>
			inner.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, cancellationToken);

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			Observe();
			return inner.MarkProcessedAsync(messageId, handlerType, cancellationToken);
		}

		public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
		{
			Observe();
			return inner.TryMarkAsProcessedAsync(messageId, handlerType, cancellationToken);
		}

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			inner.IsProcessedAsync(messageId, handlerType, cancellationToken);

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			inner.GetEntryAsync(messageId, handlerType, cancellationToken);

		public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
		{
			Observe();
			return inner.MarkFailedAsync(messageId, handlerType, errorMessage, cancellationToken);
		}

		public ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
			int maxRetries, DateTimeOffset? olderThan, int batchSize, CancellationToken cancellationToken) =>
			((IInboxStoreAdmin)inner).GetAllTenantsFailedEntriesAsync(maxRetries, olderThan, batchSize, cancellationToken);

		public ValueTask MarkFailedAsync(
			string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			Observe();
			return ((IInboxStoreAdmin)inner).MarkFailedAsync(messageId, handlerType, errorMessage, retryCount, cancellationToken);
		}

		public ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken) =>
			((IInboxStoreAdmin)inner).GetAllTenantsEntriesAsync(cancellationToken);

		public ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken) =>
			((IInboxStoreAdmin)inner).GetAllTenantsStatisticsAsync(cancellationToken);

		public ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
			((IInboxStoreAdmin)inner).CleanupAllTenantsProcessedEntriesAsync(olderThan, cancellationToken);

		private void Observe()
		{
			MarkCount++;
			try
			{
				_observed.Add(TenantScope.FromContext(new RawAmbientTenantContext()));
			}
			catch (TenantRequiredException ex)
			{
				FailedClosed ??= ex;
			}
		}
	}

	private sealed class DrainHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;
		private readonly IInboxStore _inner;

		private DrainHarness(
			ServiceProvider services,
			IInboxStore inner,
			TenantResolvingInboxStore store,
			InboxProcessor processor,
			IDispatcher dispatcher)
		{
			_services = services;
			_inner = inner;
			Store = store;
			Processor = processor;
			Dispatcher = dispatcher;
		}

		public TenantResolvingInboxStore Store { get; }

		public InboxProcessor Processor { get; }

		public IDispatcher Dispatcher { get; }

		public static async Task<DrainHarness> CreateAsync(
			string? entryTenantId,
			int? parallelProcessingDegree,
			bool dispatchSucceeds,
			ICircuitBreakerPolicy? circuitBreaker = null)
		{
			MessageTypeRegistry.RegisterType<DrainProbeMessage>();

			var dispatcher = A.Fake<IDispatcher>();
			if (dispatchSucceeds)
			{
				_ = A.CallTo(() => dispatcher.DispatchAsync(
						A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
					.Returns(Task.FromResult<IMessageResult>(MessageResult.Success()));
			}

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();
			_ = services.AddScoped(_ => dispatcher);
			var provider = services.BuildServiceProvider();

			var inner = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new DrainProbeMessage(ProbeMessageId)));

			_ = await inner.CreateEntryAsync(
				ProbeMessageId,
				HandlerTypeName,
				MessageTypeName,
				payload,
				new Dictionary<string, object>(StringComparer.Ordinal),
				CancellationToken.None).ConfigureAwait(false);

			// Put the entry into the state the re-admission drain selects: Failed, one attempt spent, and
			// last attempted long enough ago to clear the drain's always-on re-admission floor.
			await inner.MarkFailedAsync(ProbeMessageId, HandlerTypeName, "seeded failure", CancellationToken.None)
				.ConfigureAwait(false);

			var seeded = await inner.GetEntryAsync(ProbeMessageId, HandlerTypeName, CancellationToken.None)
					.ConfigureAwait(false)
				?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
			seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);
			seeded.TenantId = entryTenantId;

			var store = new TenantResolvingInboxStore(inner);

			var processor = new InboxProcessor(
				CreateOptions(parallelProcessingDegree),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance,
				circuitBreakerRegistry: circuitBreaker is null ? null : new SingleBreakerRegistry(circuitBreaker));

			processor.Init("dispatcher-drain-invariants");

			return new DrainHarness(provider, inner, store, processor, dispatcher);
		}

		/// <summary>Reads the entry through the INNER store, so the read is not itself observed as a mark.</summary>
		public ValueTask<InboxEntry?> GetEntryAsync() =>
			_inner.GetEntryAsync(ProbeMessageId, HandlerTypeName, CancellationToken.None);

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		private static IOptions<DeliveryInboxOptions> CreateOptions(int? parallelProcessingDegree)
		{
			var options = new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 1,
					ProducerBatchSize = 1,
					ConsumerBatchSize = 1,
					PerRunTotal = 1,
				},
				MaxAttempts = 3,
			};

			// null == leave the shipped default in place. Assigning it explicitly, even to the same value,
			// would hide a future change of that default from this arm.
			if (parallelProcessingDegree is { } degree)
			{
				options.Capacity.ParallelProcessingDegree = degree;
			}

			return Options.Create(options);
		}
	}

	/// <summary>A namespaced probe message: its <c>FullName</c> and <c>Name</c> differ.</summary>
	private sealed record DrainProbeMessage(string Id) : IDispatchEvent;
}
