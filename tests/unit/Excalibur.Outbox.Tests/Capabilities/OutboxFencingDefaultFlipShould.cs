// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Serialization;
using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests.Capabilities;

/// <summary>
/// author≠impl lock for the sd36sc keystone: outbox leadership fencing is <b>default-ON</b>. The
/// <see cref="OutboxProcessor"/> fences its drain whenever a leader election is registered (a leader gate
/// is present) UNLESS the consumer has explicitly asserted single-active-writer ownership via
/// <c>AsSingleWriter()</c> (<see cref="OutboxDeliveryOptions.SingleActiveWriter"/>). The flip is keyed on
/// the gate's <i>presence</i>, so registering a leader election is the multi-instance signal that turns
/// fencing on — no separate "enable fencing" switch.
/// </summary>
/// <remarks>
/// <para>
/// Binds the <b>property</b> (which store method the drain calls), not the mechanism: the drain is driven
/// end-to-end against a recording store that returns an empty batch, and the lock asserts which claim
/// overload — the fenced <c>GetUnsentMessagesAsync(batchSize, token, ct)</c> or the unfenced
/// <c>GetUnsentMessagesAsync(batchSize, ct)</c> — was actually invoked.
/// </para>
/// <para>
/// SAFETY + LIVENESS (testing-patterns §3):
/// <list type="bullet">
/// <item><description><b>Default fenced (liveness — THE flip):</b> gate present + fencing-capable store +
/// no opt-out → the drain claims through the FENCED overload, presenting the tenure token. RED if fencing
/// is silently keyed off (the flip regressed) — the unfenced overload would be called instead.</description></item>
/// <item><description><b>Fail-fast (safety):</b> gate present + non-fencing store + no opt-out → the ctor
/// throws (the deployment cannot fence and must not "look fenced but isn't"). RED if the fail-fast is
/// dropped.</description></item>
/// <item><description><b>Opt-out unfenced (liveness):</b> gate present + <c>SingleActiveWriter = true</c> →
/// ctor does NOT throw even on a non-fencing store, the drain runs UNFENCED, and the downgrade is logged
/// (never silent).</description></item>
/// <item><description><b>No-LE unfenced (liveness):</b> no gate → the drain runs UNFENCED even against a
/// fencing-CAPABLE store, and the unfenced run is logged. Paired with the default-fenced arm, this isolates
/// the gate's presence — not the store's capability — as the flip trigger.</description></item>
/// </list>
/// </para>
/// <para>
/// Fixture honesty: the stores implement <see cref="IOutboxStore"/> / <see cref="IFencedOutboxStore"/>
/// DIRECTLY (no first-party base supplies the fencing member), so the interface contract binds directly and
/// capability resolution goes through the real <c>GetService</c> seam. The startup <c>GetService</c>-vs-cast
/// resolution through a decorator is a separate concern, locked by
/// <c>FencingStartupGuardResolvesThroughGetServiceShould</c>; this file locks the default-flip drain-path.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxFencingDefaultFlipShould
{
	private const long TenureToken = 7L;

	[Fact]
	public async Task ClaimThroughTheFencedOverload_WhenLeaderGatePresentAndNotOptedOut()
	{
		// LIVENESS — THE default-ON flip. Gate present, fencing-capable store, no opt-out → fenced drain.
		var store = new RecordingFencedOutboxStore();
		await using var processor = CreateProcessor(
			store, gate: FencedGate(TenureToken), singleActiveWriter: false, logger: NullLogger<OutboxProcessor>.Instance);

		await DriveOneDrainAsync(processor).ConfigureAwait(false);

		store.FencedClaimCalled.ShouldBeTrue(
			"A leader gate is present with no AsSingleWriter() opt-out, so the drain MUST claim through the "
			+ "FENCED overload (presenting the tenure token). If this is false the default-ON flip has regressed "
			+ "and a superseded leader could claim messages it no longer owns.");
		store.UnfencedClaimCalled.ShouldBeFalse(
			"The fenced deployment must NOT fall through to the unfenced claim.");
		store.ObservedToken.ShouldBe(TenureToken, "The current tenure's fencing token must be presented to the store.");
	}

	[Fact]
	public void FailFastAtStartup_WhenLeaderGatePresentAndStoreCannotFenceAndNotOptedOut()
	{
		// SAFETY — a gate present + a store that cannot fence + no opt-out is the "looks fenced but isn't"
		// split-brain window. The ctor must refuse to start, and name the AsSingleWriter() opt-out.
		var ex = Should.Throw<InvalidOperationException>(
			() => CreateProcessor(
				new RecordingPlainOutboxStore(), gate: FencedGate(TenureToken), singleActiveWriter: false,
				logger: NullLogger<OutboxProcessor>.Instance),
			"Leader election is registered but the store cannot enforce a fencing high-water mark and there is no "
			+ "AsSingleWriter() opt-out — the processor must fail closed at startup rather than drain unfenced.");

		ex.Message.ShouldContain(
			"AsSingleWriter",
			Case.Insensitive,
			"The fail-fast message must point the operator at the explicit single-writer opt-out.");
	}

	[Fact]
	public async Task RunUnfencedAndLogTheDowngrade_WhenSingleActiveWriterOptOut()
	{
		// LIVENESS — the opt-out is honored: no throw even on a non-fencing store, the drain runs unfenced,
		// and the downgrade is logged so it is observable, never silent.
		var store = new RecordingPlainOutboxStore();
		var logger = new CapturingLogger<OutboxProcessor>();

		await using var processor = CreateProcessor(
			store, gate: FencedGate(TenureToken), singleActiveWriter: true, logger: logger);

		await DriveOneDrainAsync(processor).ConfigureAwait(false);

		store.UnfencedClaimCalled.ShouldBeTrue(
			"With SingleActiveWriter = true the drain must run UNFENCED even though a leader gate is present.");
		logger.LoggedEventIds.ShouldContain(
			OutboxEventId.OutboxUnfencedBySingleWriterOptOut,
			"The single-active-writer downgrade must be logged at startup so it is never a silent loss of fencing.");
	}

	[Fact]
	public async Task RunUnfencedAndLogIt_WhenNoLeaderGate_EvenIfStoreCanFence()
	{
		// LIVENESS — the flip is keyed on the GATE's presence, not the store's capability: a fencing-capable
		// store with NO gate still drains unfenced (and logs it). Paired with the default-fenced arm, this
		// isolates the gate as the trigger.
		var store = new RecordingFencedOutboxStore();
		var logger = new CapturingLogger<OutboxProcessor>();

		await using var processor = CreateProcessor(
			store, gate: null, singleActiveWriter: false, logger: logger);

		await DriveOneDrainAsync(processor).ConfigureAwait(false);

		store.UnfencedClaimCalled.ShouldBeTrue(
			"No leader gate is registered, so the drain must run UNFENCED even though the store CAN fence — "
			+ "fencing is keyed on the gate's presence, not the store's capability.");
		store.FencedClaimCalled.ShouldBeFalse("Without a gate the fenced overload must not be used.");
		logger.LoggedEventIds.ShouldContain(
			OutboxEventId.OutboxRunningUnfenced,
			"An unfenced drain (no leader election) must be logged so the topology assumption is observable.");
	}

	// Drives exactly one producer claim: the recording store returns an empty batch, so the producer claims
	// once, finds nothing, and the producer/consumer loops exit deterministically (no dispatch, no timing).
	private static async Task DriveOneDrainAsync(OutboxProcessor processor)
	{
		processor.Init("fencing-default-flip-test");
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);
	}

	private static ILeaderProcessingGate FencedGate(long token)
	{
		var gate = A.Fake<ILeaderProcessingGate>();
		A.CallTo(() => gate.FencingToken).Returns(token);
		return gate;
	}

	private static OutboxProcessor CreateProcessor(
		IOutboxStore outboxStore,
		ILeaderProcessingGate? gate,
		bool singleActiveWriter,
		ILogger<OutboxProcessor> logger)
	{
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 3,
			EnableBatchDatabaseOperations = true,
			SingleActiveWriter = singleActiveWriter,
		});

		return new OutboxProcessor(
			options,
			outboxStore,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			logger,
			envelopeDeserializer: null,
			deadLetterQueue: null,
			circuitBreakerRegistry: null,
			backoffCalculator: null,
			deliveryGuaranteeOptions: null,
			leaderGate: gate);
	}

	#region Fixtures

	// Non-fencing store — implements IOutboxStore DIRECTLY. The default IServiceProvider.GetService on
	// IOutboxStore returns null for IFencedOutboxStore (not an instance), so this store honestly reports "no
	// fencing capability" through the real seam.
	private sealed class RecordingPlainOutboxStore : IOutboxStore
	{
		public bool UnfencedClaimCalled { get; private set; }

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			UnfencedClaimCalled = true;
			return new ValueTask<IEnumerable<OutboundMessage>>([]);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	// Fencing-capable store — implements IFencedOutboxStore DIRECTLY (no base supplies the fenced member).
	// The default GetService resolves IFencedOutboxStore to this instance, so the processor's capability seam
	// discovers fencing honestly.
	private sealed class RecordingFencedOutboxStore : IFencedOutboxStore
	{
		public bool FencedClaimCalled { get; private set; }

		public bool UnfencedClaimCalled { get; private set; }

		public long? ObservedToken { get; private set; }

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, long fencingToken, CancellationToken cancellationToken)
		{
			FencedClaimCalled = true;
			ObservedToken = fencingToken;
			return new ValueTask<IEnumerable<OutboundMessage>>([]);
		}

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			UnfencedClaimCalled = true;
			return new ValueTask<IEnumerable<OutboundMessage>>([]);
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	// Dependency-free capturing logger — records the EventIds emitted so the startup downgrade logs
	// (131227 opt-out, 131228 no-LE) can be asserted "observable, never silent".
	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public List<EventId> LoggedEventIds { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) => LoggedEventIds.Add(eventId);
	}

	#endregion Fixtures
}
