// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Binds the saga event-dedup guarantee stated in the package's architecture document: dedup is a BOUNDED
/// window of 1000 event ids per saga instance, evicted FIFO, and beyond that bound a redelivery re-executes
/// the step. Both arms drive the real coordinator path, not the id set in isolation -- a set test proves the
/// data structure and says nothing about whether the saga consults it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaEventDedupWindowShould
{
	// The documented bound. Written out rather than read from the implementation on purpose: if someone
	// changes the bound or the eviction policy without changing the sentence that states them, these arms
	// are what fails.
	private const int DocumentedBound = 1000;

	// SAFETY: a redelivery INSIDE the window is ignored -- the step does not run a second time.
	[Fact]
	public async Task IgnoreARedeliveryWhileTheEventIdIsStillInsideTheWindow()
	{
		var (coordinator, saga, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, sagaId, "step-1");
		await DeliverAsync(coordinator, sagaInfo, sagaId, "step-1");

		saga.HandledSteps.ShouldBe(["step-1"], "a redelivery inside the window must not re-execute the step");
	}

	// LIVENESS: past the bound the window has evicted the oldest id, and a redelivery of THAT event runs
	// again. This is the arm that states the guarantee honestly rather than implying exactly-once, and it is
	// the one that fails when the bound or the eviction policy moves without the document moving with it.
	[Fact]
	public async Task ReExecuteAStepWhoseEventIdHasBeenEvictedFromTheWindow()
	{
		var (coordinator, saga, sagaInfo, sagaId) = NewCoordinator();

		await DeliverAsync(coordinator, sagaInfo, sagaId, "step-1");

		// Fill the window past its bound so the very first id is the one evicted.
		for (var i = 0; i < DocumentedBound; i++)
		{
			await DeliverAsync(coordinator, sagaInfo, sagaId, $"filler-{i}");
		}

		await DeliverAsync(coordinator, sagaInfo, sagaId, "step-1");

		saga.HandledSteps.Count(step => step == "step-1")
			.ShouldBe(2, "beyond the bound the id is evicted FIFO and the redelivered step runs again");
	}

	private static async Task DeliverAsync(
		SagaCoordinator coordinator, SagaInfo sagaInfo, Guid sagaId, string stepId) =>
		await coordinator.HandleEventInternalAsync<CountingSaga, CountingSagaState>(
			A.Fake<IMessageContext>(),
			new CountingEvent { SagaId = sagaId.ToString(), StepId = stepId },
			sagaInfo,
			CancellationToken.None);

	private static (SagaCoordinator Coordinator, StepLog Saga, SagaInfo Info, Guid SagaId) NewCoordinator()
	{
		var sagaId = Guid.NewGuid();

		// One state instance for the life of the test: the processed-id set lives in the saga row, so a
		// redelivery only sees the earlier ids if the store hands back the state that recorded them.
		var state = new CountingSagaState();
		var store = A.Fake<ISagaStore>();
		A.CallTo(() => store.LoadAsync<CountingSagaState>(A<Guid>._, A<CancellationToken>._)).Returns(state);

		// The coordinator activates a fresh saga per delivery, so the execution record lives outside the saga
		// and is injected into it.
		var log = new StepLog();

		var services = new ServiceCollection();
		services.AddSingleton(store);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		services.AddSingleton(log);
		var serviceProvider = services.BuildServiceProvider();

		var sagaInfo = new SagaInfo(typeof(CountingSaga), typeof(CountingSagaState));
		sagaInfo.Handles<CountingEvent>();

		var coordinator = new SagaCoordinator(
			serviceProvider,
			store,
			Microsoft.Extensions.Options.Options.Create(new SagaOptions()),
			NullLogger<SagaCoordinator>.Instance);

		return (coordinator, log, sagaInfo, sagaId);
	}

	private sealed class StepLog
	{
		public List<string> HandledSteps { get; } = [];
	}

	private sealed class CountingSagaState : SagaState
	{
	}

	private sealed class CountingEvent : ISagaEvent
	{
		public required string SagaId { get; init; }

		public string? StepId { get; init; }
	}

	private sealed class CountingSaga(
		CountingSagaState initialState,
		IDispatcher dispatcher,
		ILogger<CountingSaga> logger,
		StepLog log)
		: SagaBase<CountingSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is CountingEvent;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is CountingEvent countingEvent && countingEvent.StepId is { } stepId)
			{
				log.HandledSteps.Add(stepId);
			}

			return Task.CompletedTask;
		}
	}
}
