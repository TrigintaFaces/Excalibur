// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using AlphaOrderPlaced = Excalibur.Saga.Tests.Orchestration.DedupKeyCollision.Alpha.OrderPlaced;
using BetaOrderPlaced = Excalibur.Saga.Tests.Orchestration.DedupKeyCollision.Beta.OrderPlaced;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Binds the half of the saga event-dedup guarantee that presupposes an event id identifies an EVENT: two
/// distinct event types that share a simple name in different namespaces must derive distinct dedup keys.
/// <para>
/// The failure this detects is not the documented one. The architecture document states the dedup failure
/// mode as RE-EXECUTION -- an at-least-once shape an idempotent handler absorbs, which is why the stated
/// consumer obligation is idempotency. A simple-name collision produces the OPPOSITE: a distinct event is
/// NEVER executed, silently, and idempotency is no protection whatever against zero execution. Nothing an
/// operator can observe distinguishes that drop from a correct dedup, because it emits the same log line.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaEventDedupKeyIsNamespaceQualifiedShould
{
	// The two types below are deliberately both named OrderPlaced, in sibling namespaces. That is the whole
	// fixture: it is legal, it is ordinary in a codebase with a module per bounded context, and it is what a
	// simple-name-keyed derivation collapses.
	private static readonly string AlphaName = typeof(AlphaOrderPlaced).FullName!;
	private static readonly string BetaName = typeof(BetaOrderPlaced).FullName!;

	[Fact]
	public void UseTypeNamesThatCollideOnTheirSimpleNameSoTheFixtureIsNotVacuous()
	{
		// A control on the fixture itself. If someone renames either event type, the arms below would pass
		// for a reason that has nothing to do with the derivation, so this asserts the premise directly.
		typeof(AlphaOrderPlaced).Name.ShouldBe(typeof(BetaOrderPlaced).Name);
		AlphaName.ShouldNotBe(BetaName);
	}

	// The defect arm. With the key derived from the SIMPLE name these two compose one key and Beta is
	// dropped as a duplicate; with it derived from the namespace-qualified name they are distinct.
	[Fact]
	public async Task ExecuteBothEventsWhenTwoDistinctTypesShareASimpleNameAndAStepId()
	{
		var (coordinator, log, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId, StepId = "place" });
		await DeliverAsync(coordinator, sagaInfo, new BetaOrderPlaced { SagaId = sagaId, StepId = "place" });

		log.Handled.ShouldBe(
			[AlphaName, BetaName],
			"two distinct event types are two distinct events; deriving the dedup key from the simple type "
			+ "name collapses them onto one key and silently discards the second");
	}

	// The same collision with no StepId set. The shipped remark tells consumers to always set StepId to a
	// unique value per step, so this arm pins that the documented remedy is not what closes the collision --
	// a consumer who follows it is safe only because the key is namespace-qualified.
	[Fact]
	public async Task ExecuteBothEventsWhenTwoDistinctTypesShareASimpleNameAndNoStepIdIsSet()
	{
		var (coordinator, log, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId });
		await DeliverAsync(coordinator, sagaInfo, new BetaOrderPlaced { SagaId = sagaId });

		log.Handled.ShouldBe([AlphaName, BetaName]);
	}

	// The liveness control: a true redelivery of the SAME type must still be deduplicated. Without this the
	// arms above would pass for a derivation that had simply stopped deduplicating anything at all.
	[Fact]
	public async Task StillIgnoreATrueRedeliveryOfTheSameEventType()
	{
		var (coordinator, log, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId, StepId = "place" });
		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId, StepId = "place" });

		log.Handled.ShouldBe([AlphaName], "a redelivery of the same type at the same step is a duplicate");
	}

	private static async Task DeliverAsync(SagaCoordinator coordinator, SagaInfo sagaInfo, ISagaEvent evt) =>
		await coordinator.HandleEventInternalAsync<CollisionSaga, CollisionSagaState>(
			A.Fake<IMessageContext>(),
			evt,
			sagaInfo,
			CancellationToken.None);

	private static (SagaCoordinator Coordinator, EventLog Log, SagaInfo Info, string SagaId) NewCoordinator()
	{
		var sagaId = Guid.NewGuid().ToString();

		// One state instance for the life of the test: the processed-id set lives in the saga row, so the
		// second delivery only sees the first id if the store hands back the state that recorded it.
		var state = new CollisionSagaState();
		var store = A.Fake<ISagaStore>();
		A.CallTo(() => store.LoadAsync<CollisionSagaState>(A<Guid>._, A<CancellationToken>._)).Returns(state);

		var log = new EventLog();

		var services = new ServiceCollection();
		services.AddSingleton(store);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		services.AddSingleton(log);
		var serviceProvider = services.BuildServiceProvider();

		var sagaInfo = new SagaInfo(typeof(CollisionSaga), typeof(CollisionSagaState));
		sagaInfo.Handles<AlphaOrderPlaced>();
		sagaInfo.Handles<BetaOrderPlaced>();

		var coordinator = new SagaCoordinator(
			serviceProvider,
			store,
			Microsoft.Extensions.Options.Options.Create(new SagaOptions()),
			NullLogger<SagaCoordinator>.Instance);

		return (coordinator, log, sagaInfo, sagaId);
	}

	private sealed class EventLog
	{
		public List<string> Handled { get; } = [];
	}

	private sealed class CollisionSagaState : SagaState
	{
	}

	private sealed class CollisionSaga(
		CollisionSagaState initialState,
		IDispatcher dispatcher,
		ILogger<CollisionSaga> logger,
		EventLog log)
		: SagaBase<CollisionSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) =>
			eventMessage is AlphaOrderPlaced or BetaOrderPlaced;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			log.Handled.Add(eventMessage.GetType().FullName!);

			return Task.CompletedTask;
		}
	}
}
