// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Saga replay identity is the ENVELOPE MESSAGE ID, and a delivery that carries none is processed
/// without replay protection rather than being mistaken for a duplicate.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms bind.</b> Replay identity used to be computed from the payload's business
/// fields — <c>{Type.FullName}:{SagaId}:{StepId}</c>, or <c>{Type.FullName}:{SagaId}</c> when no step id
/// was set. <c>StepId</c> is a nullable property that nothing enforces, so every event of one type
/// reaching one saga composed the SAME key: the first was processed and every later one was discarded as
/// a duplicate and NEVER EXECUTED, silently, while the log line said "skipped duplicate event". A step
/// running zero times is the failure direction no handler idempotency recovers.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> <see cref="ProcessEveryDeliveryThatSharesTypeSagaAndStep"/> is the defect arm and
/// is RED against the superseded derivation. <see cref="IgnoreARedeliveryCarryingTheSameMessageId"/> is
/// the control: without it the defect arm is satisfied by a coordinator that deduplicates nothing at all.
/// </para>
/// <para>
/// <b>Which tier supplies the identity.</b> These arms set <c>IMessageContext.MessageId</c> explicitly on
/// the context handed to the coordinator, so no arm passes because a harness happened to populate it. The
/// identity-less arms assert the absence just as explicitly.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaReplayIdentityIsTheEnvelopeMessageIdShould
{
	// DEFECT ARM. Two genuinely distinct deliveries that agree on every business field the superseded
	// derivation used — same type, same saga, no step id — and differ only in the envelope identity that
	// the delivery actually carries.
	[Fact]
	public async Task ProcessEveryDeliveryThatSharesTypeSagaAndStep()
	{
		var fixture = NewCoordinator();

		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-1");
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-2");
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-3");

		fixture.Log.Handled.Count.ShouldBe(
			3,
			"three distinct deliveries are three events. Deriving the key from {type, saga, step} gives "
			+ "them one key, so the second and third are dropped as duplicates and never execute");
	}

	// CONTROL / SAFETY. A true redelivery carries the SAME envelope id and must still be deduplicated.
	[Fact]
	public async Task IgnoreARedeliveryCarryingTheSameMessageId()
	{
		var fixture = NewCoordinator();

		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-1");
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-1");

		fixture.Log.Handled.Count.ShouldBe(
			1,
			"the same envelope id is the same delivery. Without this arm the defect arm above is satisfied "
			+ "by a coordinator that deduplicates nothing");
	}

	// THE RULED DISPOSITION for a delivery with no identity: PROCESS IT, DEDUPLICATE NOTHING, AND SAY SO.
	// One warning per event type per process; the counter increments per delivery, because a warning that
	// fires once cannot show a producer that sends no id on every single message.
	[Fact]
	public async Task ProcessAndNotDeduplicateADeliveryWithNoIdentityWhileWarningOncePerType()
	{
		var fixture = NewCoordinator();

		var perType = new Dictionary<string, long>(StringComparer.Ordinal);
		using var listener = ListenToUndeduplicableCounter(perType);

		// Three identity-less deliveries of one type, then one of a second type. The second type is what
		// makes "once per TYPE" falsifiable: a once-per-process mutant emits one warning, not two.
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, messageId: null);
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, messageId: null);
		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, messageId: null);
		await fixture.DeliverAsync(new ApprovalGranted { SagaId = fixture.SagaId.ToString() }, messageId: null);

		listener.Dispose();

		fixture.Log.Handled.Count.ShouldBe(
			4,
			"an absent identity means UNDEDUPLICABLE, which is neither a duplicate nor a first delivery. "
			+ "Refusing the event would make the framework unusable with producers a consumer does not "
			+ "control, so every delivery is processed");

		fixture.State.ProcessedEventIds.ShouldBeEmpty(
			"nothing may be recorded for a delivery whose identity is unknown -- a recorded entry would "
			+ "guard deliveries it cannot identify, which is the defect this replaced");

		var warnings = fixture.Logger.Entries
			.Where(entry => entry.Level == LogLevel.Warning
				&& entry.Message.Contains("no message identity", StringComparison.Ordinal))
			.ToList();

		warnings.Count.ShouldBe(
			2,
			"one warning per event type per process: four deliveries across two types is two warnings. "
			+ "Per-delivery would be unreadable; once per process would hide the second type entirely");

		perType.GetValueOrDefault(typeof(ParcelShipped).FullName!).ShouldBe(
			3,
			"the counter is what makes a persistent identity-less producer visible after the first log "
			+ "line, so it increments PER MESSAGE, not per type");
		perType.GetValueOrDefault(typeof(ApprovalGranted).FullName!).ShouldBe(1);
	}

	// UPGRADE. A saga saved by the superseded scheme carries keys of the form {Type.FullName}:{SagaId}.
	// Those entries can never be produced again, so they must be inert: they may not match, and they may
	// not be confused with a key this coordinator writes.
	[Fact]
	public async Task LeaveAKeyWrittenByTheSupersededSchemeInertRatherThanMatchingAgainstIt()
	{
		var fixture = NewCoordinator();

		var supersededKey = $"{typeof(ParcelShipped).FullName}:{fixture.SagaId}";
		fixture.State.TryMarkEventProcessed(supersededKey).ShouldBeTrue("fixture premise: the saga was saved with a superseded key");

		await fixture.DeliverAsync(new ParcelShipped { SagaId = fixture.SagaId.ToString() }, "msg-1");

		fixture.Log.Handled.Count.ShouldBe(
			1,
			"a superseded key names a delivery this scheme cannot identify. Honouring it would resurrect "
			+ "the collapse for every upgraded saga -- the cost of ignoring it is one re-execution, which "
			+ "the documented idempotency obligation covers; the cost of honouring it is zero executions "
			+ "of every later event of that type, which nothing covers");

		fixture.State.ProcessedEventIds.ShouldContain(
			supersededKey,
			"old keys are not translated or removed -- they simply stop being produced");
		fixture.State.ProcessedEventIds.Count(key => key.StartsWith('#')).ShouldBe(
			1,
			"a key this scheme writes begins with a marker no superseded key can begin with: those began "
			+ "with a namespace-qualified CLR type name, and '#' cannot lead a C# identifier");
	}

	private static MeterListener ListenToUndeduplicableCounter(Dictionary<string, long> perEventType)
	{
		var listener = new MeterListener
		{
			InstrumentPublished = (instrument, meterListener) =>
			{
				if (instrument.Name == "excalibur.saga.undeduplicable_deliveries")
				{
					meterListener.EnableMeasurementEvents(instrument);
				}
			},
		};

		listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
		{
			foreach (var tag in tags)
			{
				if (tag.Key == "event_type" && tag.Value is string eventType)
				{
					lock (perEventType)
					{
						perEventType[eventType] = perEventType.GetValueOrDefault(eventType) + measurement;
					}
				}
			}
		});

		listener.Start();

		return listener;
	}

	private static Fixture NewCoordinator()
	{
		var sagaId = Guid.NewGuid();

		// One state instance for the life of the test: the processed-id set lives in the saga row, so a
		// redelivery only sees the earlier ids if the store hands back the state that recorded them.
		var state = new ParcelSagaState();
		var store = A.Fake<ISagaStore>();
		A.CallTo(() => store.LoadAsync<ParcelSagaState>(A<Guid>._, A<CancellationToken>._)).Returns(state);

		var log = new EventLog();
		var logger = new CapturingLogger<SagaCoordinator>();

		var services = new ServiceCollection();
		services.AddSingleton(store);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		services.AddSingleton(log);
		var serviceProvider = services.BuildServiceProvider();

		var sagaInfo = new SagaInfo(typeof(ParcelSaga), typeof(ParcelSagaState));
		sagaInfo.Handles<ParcelShipped>();
		sagaInfo.Handles<ApprovalGranted>();

		var coordinator = new SagaCoordinator(
			serviceProvider,
			store,
			Microsoft.Extensions.Options.Options.Create(new SagaOptions()),
			logger);

		return new Fixture(coordinator, sagaInfo, sagaId, state, log, logger);
	}

	private sealed record Fixture(
		SagaCoordinator Coordinator,
		SagaInfo Info,
		Guid SagaId,
		ParcelSagaState State,
		EventLog Log,
		CapturingLogger<SagaCoordinator> Logger)
	{
		public async Task DeliverAsync(ISagaEvent evt, string? messageId)
		{
			var context = A.Fake<IMessageContext>();
			A.CallTo(() => context.MessageId).Returns(messageId);

			await Coordinator.HandleEventInternalAsync<ParcelSaga, ParcelSagaState>(
				context,
				evt,
				Info,
				CancellationToken.None);
		}
	}

	private sealed class EventLog
	{
		public List<string> Handled { get; } = [];
	}

	private sealed class ParcelSagaState : SagaState
	{
	}

	// Both events leave StepId null on purpose: that is the shape the superseded derivation collapsed,
	// and nothing in the framework enforces it, so it is the shape a consumer actually ships.
	private sealed class ParcelShipped : ISagaEvent
	{
		public required string SagaId { get; init; }

		public string? StepId => null;
	}

	private sealed class ApprovalGranted : ISagaEvent
	{
		public required string SagaId { get; init; }

		public string? StepId => null;
	}

	private sealed class ParcelSaga(
		ParcelSagaState initialState,
		IDispatcher dispatcher,
		ILogger<ParcelSaga> logger,
		EventLog log)
		: SagaBase<ParcelSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is ParcelShipped or ApprovalGranted;

		public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			log.Handled.Add(eventMessage.GetType().FullName!);

			return Task.FromResult(SagaEventOutcome.Handled);
		}
	}
}
