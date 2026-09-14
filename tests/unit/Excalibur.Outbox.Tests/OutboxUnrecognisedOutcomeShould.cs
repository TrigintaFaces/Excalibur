// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// A completion outcome this build cannot interpret must be reported, never swallowed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the second half of a two-part property, and the half with a future in it.</b> The sibling
/// arm binds the enum: its default value must not mean success. That type is finished. This one binds the
/// consuming switch, which every outcome member added from now on will pass through - including one added
/// by someone who never reads this file. The two fail independently and fixing either alone leaves the
/// framework reporting a success it did not perform.
/// </para>
/// <para>
/// <b>Why the assertion is on a log.</b> Every branch of that switch returns normally, so the log is the
/// only thing that differs between "recorded" and "this build has no idea what the store just said". The
/// log is therefore the requirement, not a mechanism chosen for convenience. It is asserted by event id
/// rather than by message text, so the wording stays free to change.
/// </para>
/// <para>
/// <b>Written only once the remedy existed.</b> An earlier draft of this arm was declined twice, because
/// the disposition for the unrecognised branch was an open contract question and an arm written to a
/// guess locks the guess. It is written now against what the code does, not against what its author
/// would have chosen.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxUnrecognisedOutcomeShould
{
	private const int UnrecognisedOutcomeEventId = 134007;

	/// <summary>
	/// SAFETY. A value no member defines is reported rather than passed off as a recorded failure.
	/// </summary>
	/// <remarks>
	/// The cast value stands in for the case that actually matters: a member added to the enum later by
	/// someone who does not update this switch. It cannot be written as a named member, because naming it
	/// would make it recognised.
	/// </remarks>
	[Fact]
	public async Task Report_an_outcome_no_member_defines()
	{
		var log = await DrainAsync((OutboxCompletionOutcome)0x7F);

		log.EventIds.ShouldContain(
			UnrecognisedOutcomeEventId,
			"the store answered with something this build cannot interpret. Passing that off silently is "
			+ "the same failure as the zero-value default: a failure the framework never recorded, "
			+ "reported to nobody, on a message that stays claimed until its reservation lapses");
	}

	/// <summary>
	/// SAFETY. The zero value reaches the same report, so a forgotten assignment is visible.
	/// </summary>
	[Fact]
	public async Task Report_the_zero_value_rather_than_treating_it_as_recorded()
	{
		var log = await DrainAsync(OutboxCompletionOutcome.Unknown);

		log.EventIds.ShouldContain(
			UnrecognisedOutcomeEventId,
			"a store that forgets to assign returns the zero value. It must arrive here and be reported, "
			+ "which is what makes the enum's non-success default observable rather than merely correct");
	}

	/// <summary>
	/// LIVENESS. A recorded failure is not reported as uninterpretable.
	/// </summary>
	/// <remarks>
	/// The pair to both arms above, and the one that fails for the cheapest wrong fix: a switch that
	/// warned on every outcome would satisfy them completely and would be muted by its readers within a
	/// day, at which point it protects nothing.
	/// </remarks>
	[Fact]
	public async Task Stay_silent_when_the_store_recorded_the_failure()
	{
		var log = await DrainAsync(OutboxCompletionOutcome.Applied);

		log.EventIds.ShouldNotContain(
			UnrecognisedOutcomeEventId,
			"the store said it recorded the failure. Warning here would make the signal worthless: an "
			+ "operator who sees it on every drain stops reading it, and then the real one is invisible");
	}

	private static async Task<CapturingLogger> DrainAsync(OutboxCompletionOutcome outcome)
	{
		MessageTypeRegistry.RegisterType<UndispatchableProbe>();

		var log = new CapturingLogger();

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
			new OutcomeReportingStore(outcome),
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			log,
			envelopeDeserializer: null);

		processor.Init("unrecognised-outcome-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token);

		return log;
	}

	/// <summary>
	/// Records the event ids the drain emitted. Asserting on ids rather than rendered text keeps the arms
	/// insensitive to wording, which must stay free to change.
	/// </summary>
	private sealed class CapturingLogger : ILogger<OutboxProcessor>
	{
		private readonly List<int> _eventIds = [];

		public IReadOnlyList<int> EventIds
		{
			get { lock (_eventIds) { return [.. _eventIds]; } }
		}

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			lock (_eventIds)
			{
				_eventIds.Add(eventId.Id);
			}
		}
	}

	/// <summary>
	/// Hands out one undispatchable message carrying a claim identity, then nothing, and answers the
	/// claim-scoped failure report with whatever outcome the arm is driving.
	/// </summary>
	/// <remarks>
	/// Implements the claim-scoped capability DIRECTLY: capability discovery is by instance type, and a
	/// fixture that inherited the member from a first-party base would re-test the base rather than the
	/// interface's own requirement.
	/// </remarks>
	private sealed class OutcomeReportingStore(OutboxCompletionOutcome outcome)
		: IOutboxStore, IClaimScopedOutboxStore, IDeadLetterableOutboxStore
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

		public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, string claimIdentity,
			CancellationToken cancellationToken) => new(outcome);

		public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(
			string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt,
			string claimIdentity, CancellationToken cancellationToken) => new(outcome);

		public ValueTask MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkDeadLetteredAsync(
			string messageId, string reason, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(
			IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}
