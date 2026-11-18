// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Helpers;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Locks saga compensation: committed steps declare an undo, the obligation is DURABLE, and a later
/// failure runs the owed undos in reverse.
/// </summary>
/// <remarks>
/// <para>
/// Compensation is what makes a saga a saga — the thing that reverses committed work when a later step
/// fails. Before this it was unreachable: the source generator resolved compensating methods by an
/// attribute that existed in zero files, so nothing could ever be declared and the shipped
/// <c>RecordSagaCompensated</c> metric had no production caller.
/// </para>
/// <para>
/// <b>Why registration and obligation are separate, which these arms pin.</b> A compensating action must
/// run LATER, when a different message fails, so it cannot be a delegate captured on the failing message's
/// handler. It is registered by NAME in the constructor — created fresh on every load, so nothing is
/// serialized and nothing is resolved by reflection, which is what keeps it working under trimming and
/// Native AOT — while the knowledge that a step actually ran lives in the persisted state.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaCompensationShould : UnitTestBase
{
	private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
	private readonly ILogger<CompensatingSaga> _logger = NullLogger<CompensatingSaga>.Instance;

	/// <summary>
	/// SAFETY: a later failure undoes every committed step, most recent first.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task UndoEveryCommittedStepInReverseOrder_WhenALaterStepFails()
	{
		var saga = NewSaga();

		await saga.ReserveStockAsync(CancellationToken.None);
		await saga.ChargeCardAsync(CancellationToken.None);

		// The third step fails after the first two committed.
		await saga.RunCompensationAsync(CancellationToken.None);

		saga.Undone.ShouldBe(
			["ChargeCard", "ReserveStock"],
			"compensation must run most recent first. Undoing forwards releases the stock the charge still "
			+ "references, and a saga whose later step depends on its earlier one either fails on that "
			+ "dependency or orphans the earlier work.");
	}

	/// <summary>
	/// LIVENESS: a saga that never fails compensates nothing. Without this arm an implementation that
	/// undid everything unconditionally would satisfy the safety arm above.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task UndoNothing_WhenNoStepRecordedAnObligation()
	{
		var saga = NewSaga();

		await saga.RunCompensationAsync(CancellationToken.None);

		saga.Undone.ShouldBeEmpty(
			"a saga that committed nothing owes nothing. An implementation that ran every registered "
			+ "action regardless would pass the reverse-order arm and reverse work that never happened.");
	}

	/// <summary>
	/// DURABILITY, and this is the arm that makes the feature real: the obligation survives being
	/// persisted and reloaded in a different instance.
	/// </summary>
	/// <returns>The arm.</returns>
	/// <remarks>
	/// A saga runs for hours or days across any number of processes, so the instance that committed a step
	/// is routinely not the one that has to undo it. An in-memory log is lost by the single event that
	/// makes it matter. The round trip here goes through System.Text.Json because that is what every
	/// durable provider stores — state is persisted as a JSON blob on all seven — and because a get-only
	/// collection is written by the serializer and then DISCARDED on read under its default Replace
	/// handling, which would reload an empty log that is indistinguishable from owing nothing.
	/// </remarks>
	[Fact]
	public async Task SurviveAPersistAndReload_SoAFreshInstanceStillOwesTheUndo()
	{
		var original = NewSaga();
		await original.ReserveStockAsync(CancellationToken.None);
		await original.ChargeCardAsync(CancellationToken.None);

		var json = JsonSerializer.Serialize(original.State);
		var reloaded = JsonSerializer.Deserialize<CompensatingSagaState>(json);

		_ = reloaded.ShouldNotBeNull();
		reloaded.PendingCompensations.ShouldBe(
			["ReserveStock", "ChargeCard"],
			"the obligation and its ORDER must both survive the round trip. Order is as load-bearing as "
			+ "membership: reverse order is computed from it, so a log that reloads shuffled undoes the "
			+ "steps in the wrong sequence while looking complete.");

		// A fresh instance over the reloaded state owes the same undos and can run them.
		var revived = new CompensatingSaga(reloaded, _dispatcher, _logger);
		await revived.RunCompensationAsync(CancellationToken.None);

		revived.Undone.ShouldBe(
			["ChargeCard", "ReserveStock"],
			"the instance that undoes the work is routinely not the one that did it, which is the entire "
			+ "reason the log is persisted rather than held in the committing instance");
	}

	/// <summary>
	/// FAIL LOUD: an owed step whose action is not registered must throw, never be skipped.
	/// </summary>
	/// <returns>The arm.</returns>
	/// <remarks>
	/// Skipping is the worst available outcome and the one an implementation drifts into: the obligation is
	/// cleared, the committed work stays, and nothing downstream can tell that apart from a saga that
	/// compensated cleanly. That is the silent class, so it is a throw.
	/// </remarks>
	[Fact]
	public async Task Throw_WhenAnOwedStepHasNoRegisteredAction()
	{
		var saga = NewSaga();
		saga.RecordObligationWithNoAction("StepNobodyRegistered");

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => saga.RunCompensationAsync(CancellationToken.None));

		thrown.Message.ShouldContain(
			"StepNobodyRegistered",
			Case.Sensitive,
			"the fault must name the step whose work has no reachable undo, or an operator cannot tell "
			+ "which committed work is now stranded");
	}

	/// <summary>
	/// One failing action must not strand the others: every owed action is attempted.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task AttemptEveryUndoAndAggregate_WhenOneOfThemFails()
	{
		var saga = NewSaga(failUndoOf: "ChargeCard");

		await saga.ReserveStockAsync(CancellationToken.None);
		await saga.ChargeCardAsync(CancellationToken.None);

		_ = await Should.ThrowAsync<AggregateException>(() => saga.RunCompensationAsync(CancellationToken.None));

		saga.Undone.ShouldBe(
			["ReserveStock"],
			"the stock release must still have been attempted after the card refund threw. Abandoning the "
			+ "remaining undos on the first failure leaves earlier committed work reversed by nothing, as "
			+ "collateral from one unreachable downstream service.");
	}


	/// <summary>
	/// END TO END: a publisher holding only a business key reaches the instance a previous message created.
	/// </summary>
	/// <returns>The arm.</returns>
	/// <remarks>
	/// This is the gap correlation exists to close. Before it, the only thing that located a saga was its
	/// identifier, so the first message in a conversation could address an instance and a later message
	/// from another service could not — nothing shared the GUID. The key is shared; the identifier is
	/// derived from it; the ordinary load-by-identifier path then finds the instance with no lookup table
	/// and nothing for a provider to implement.
	/// </remarks>
	[Fact]
	public async Task FindAnExistingInstanceFromTheBusinessKeyAlone()
	{
		var store = new InMemorySagaStore(new TestTenantContext());
		const string sagaType = "OrderSaga";
		const string orderNumber = "ORDER-4242";

		// First message: the publisher knows only the order number.
		var sagaId = SagaCorrelation.ToSagaId(sagaType, orderNumber);
		var created = new CompensatingSagaState { SagaId = sagaId, CorrelationId = orderNumber };
		await store.SaveAsync(created, CancellationToken.None);

		// A later message, from a service that never saw the identifier, derives it from the same key.
		var rederived = SagaCorrelation.ToSagaId(sagaType, orderNumber);
		var found = await store.LoadAsync<CompensatingSagaState>(rederived, CancellationToken.None);

		_ = found.ShouldNotBeNull(
			"the second publisher held only the order number. If derivation were not deterministic, or were "
			+ "not a function of the key alone, this load returns nothing and the conversation silently "
			+ "starts a second saga instead of continuing the first.");

		found.CorrelationId.ShouldBe(
			orderNumber,
			"the key must round-trip through the store. The identifier is a one-way function of it, so "
			+ "without the recorded key nothing can say which order a given saga belongs to — which is the "
			+ "direction derivation cannot provide and an operator needs.");
	}

	private CompensatingSaga NewSaga(string? failUndoOf = null) =>
		new(new CompensatingSagaState { SagaId = Guid.NewGuid() }, _dispatcher, _logger, failUndoOf);

	/// <summary>State for the test saga.</summary>
	private sealed class CompensatingSagaState : SagaState
	{
	}

	/// <summary>
	/// A saga with two compensatable steps, declaring its undos by name in the constructor.
	/// </summary>
	private sealed class CompensatingSaga : SagaBase<CompensatingSagaState>
	{
		private readonly List<string> _undone = [];

		/// <summary>Initializes a new instance of the <see cref="CompensatingSaga"/> class.</summary>
		/// <param name="state">The saga state.</param>
		/// <param name="dispatcher">The dispatcher.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="failUndoOf">A step whose undo should throw, or null.</param>
		public CompensatingSaga(
			CompensatingSagaState state,
			IDispatcher dispatcher,
			ILogger logger,
			string? failUndoOf = null)
			: base(state, dispatcher, logger)
		{
			// Declared in the constructor, so a reloaded instance has them without deserializing a
			// delegate or resolving a method by name at runtime.
			RegisterCompensation("ReserveStock", _ => Undo("ReserveStock", failUndoOf));
			RegisterCompensation("ChargeCard", _ => Undo("ChargeCard", failUndoOf));
		}

		/// <summary>Gets the steps undone, in the order they were undone.</summary>
		public IReadOnlyList<string> Undone => _undone;

		/// <inheritdoc />
		public override bool HandlesEvent(object eventMessage) => true;

		/// <inheritdoc />
		public override Task<SagaEventOutcome> HandleAsync(
			object eventMessage, CancellationToken cancellationToken) =>
			Task.FromResult(SagaEventOutcome.Handled);

		/// <summary>Commits the stock reservation and records that it is owed an undo.</summary>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task.</returns>
		public Task ReserveStockAsync(CancellationToken cancellationToken)
		{
			// Recorded AFTER the work, never before: owing an undo for work that never happened is its
			// own defect.
			RecordPendingCompensation("ReserveStock");
			return Task.CompletedTask;
		}

		/// <summary>Commits the card charge and records that it is owed an undo.</summary>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task.</returns>
		public Task ChargeCardAsync(CancellationToken cancellationToken)
		{
			RecordPendingCompensation("ChargeCard");
			return Task.CompletedTask;
		}

		/// <summary>Runs the protected compensation pass. The fixture exposes it rather than the
		/// production type widening its visibility for a test.</summary>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <returns>A task.</returns>
		public Task RunCompensationAsync(CancellationToken cancellationToken) =>
			CompensateAsync(cancellationToken);

		/// <summary>Records an obligation for a step with no registered action.</summary>
		/// <param name="stepName">The step name.</param>
		public void RecordObligationWithNoAction(string stepName) => RecordPendingCompensation(stepName);

		private Task Undo(string stepName, string? failUndoOf)
		{
			if (string.Equals(stepName, failUndoOf, StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"the undo of '{stepName}' failed");
			}

			_undone.Add(stepName);
			return Task.CompletedTask;
		}
	}
}
