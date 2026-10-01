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
/// The event type plays NO part in the saga replay key, so two distinct event types can never collapse
/// onto one.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this used to bind, and why it changed.</b> The replay key was once composed from the event
/// type, the saga id and the step id, and these arms pinned the type component to the
/// namespace-qualified name — because two distinct types sharing a simple name in different namespaces
/// composed ONE key from the simple name, and the second was then discarded as a duplicate and NEVER
/// executed. The key is now the envelope message id, so the collision is not merely guarded against, it
/// is inexpressible: no part of the type reaches the key at all.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> <see cref="WriteAKeyThatContainsNoPartOfTheEventTypeName"/> is the arm that goes
/// RED if any type-derived component is reintroduced — including a "harmless" qualification prefix, which
/// is how the collapsed key arrived the first time. The behavioural arm above it is the regression partner:
/// the structural arm alone is satisfied by a coordinator that writes no key whatever.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaEventDedupKeyIsNamespaceQualifiedShould
{
	// The two types below are deliberately both named OrderPlaced, in sibling namespaces. That is the whole
	// fixture: it is legal, it is ordinary in a codebase with a module per bounded context, and it is what a
	// simple-name-keyed derivation collapsed.
	private static readonly string AlphaName = typeof(AlphaOrderPlaced).FullName!;
	private static readonly string BetaName = typeof(BetaOrderPlaced).FullName!;

	[Fact]
	public void UseTypeNamesThatCollideOnTheirSimpleNameSoTheFixtureIsNotVacuous()
	{
		// A control on the fixture itself. If someone renames either event type, the arms below would pass
		// for a reason that has nothing to do with the key, so this asserts the premise directly.
		typeof(AlphaOrderPlaced).Name.ShouldBe(typeof(BetaOrderPlaced).Name);
		AlphaName.ShouldNotBe(BetaName);
	}

	[Fact]
	public async Task ExecuteBothEventsWhenTwoDistinctTypesShareASimpleName()
	{
		var (coordinator, log, _, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId }, "delivery-1");
		await DeliverAsync(coordinator, sagaInfo, new BetaOrderPlaced { SagaId = sagaId }, "delivery-2");

		log.Handled.ShouldBe(
			[AlphaName, BetaName],
			"two distinct deliveries are two events, whatever their types are named");
	}

	// The structural arm. Reintroducing ANY type-derived component — even a prefix added "for readability"
	// — puts a type name back into the persisted key and reopens the collapse, and this is what fails.
	[Fact]
	public async Task WriteAKeyThatContainsNoPartOfTheEventTypeName()
	{
		var (coordinator, _, state, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, new AlphaOrderPlaced { SagaId = sagaId }, "delivery-1");

		var recorded = state.ProcessedEventIds.ShouldHaveSingleItem();

		recorded.ShouldNotContain("OrderPlaced", Case.Insensitive,
			"the simple type name is what collapsed two distinct events onto one key");
		recorded.ShouldNotContain(nameof(DedupKeyCollision.Alpha), Case.Insensitive,
			"nor may the namespace reach the key -- qualification made the collapse rarer, not impossible");
		recorded.ShouldNotContain(sagaId, Case.Insensitive,
			"the saga id restates the partition the set already hangs off, so it carries no information");
	}

	// The message ids deliberately share no substring with either type or namespace name: otherwise the
	// structural arm below fails on its own fixture instead of on the key.
	private static async Task DeliverAsync(
		SagaCoordinator coordinator, SagaInfo sagaInfo, ISagaEvent evt, string messageId)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);

		await coordinator.HandleEventInternalAsync<CollisionSaga, CollisionSagaState>(
			context,
			evt,
			sagaInfo,
			CancellationToken.None);
	}

	private static (SagaCoordinator Coordinator, EventLog Log, CollisionSagaState State, SagaInfo Info, string SagaId)
		NewCoordinator()
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

		return (coordinator, log, state, sagaInfo, sagaId);
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

		public override Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			log.Handled.Add(eventMessage.GetType().FullName!);

			return Task.FromResult(SagaEventOutcome.Handled);
		}
	}
}
