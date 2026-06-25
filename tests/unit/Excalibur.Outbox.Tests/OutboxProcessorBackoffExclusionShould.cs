// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

// Sprint 849 / Lane R3 (gejhft+ffxglb), CB-open exclusion — a HARD FR-R3 acceptance criterion (PdM ruling
// 15395, SA 15396, Reviewer 15394). A backoff delay (NextAttemptAt) MUST be scheduled ONLY on a GENUINE delivery
// failure (the failedToRetry tuple's ApplyBackoff:true). A transient circuit-breaker-open short-circuit
// (ApplyBackoff:false) MUST stay on the plain MarkFailedAsync — attempt unchanged, NO NextAttemptAt — so a
// throttling delay is never imposed for a failure the message never actually caused.
//
// This is the discriminating (anti-vacuity) lock: without it, a vacuous "schedule backoff on EVERY failed
// message" implementation passes all the other R2/R3 facts (enforce-invariants-structurally). The discriminator
// lives in OutboxProcessor.PerformBatchDatabaseOperationsAsync, which routes applyBackoff ?
// MarkFailedWithBackoffOrFallbackAsync(...) : MarkFailedAsync(...). RED pre-fix: the failedToRetry tuple had no
// ApplyBackoff element and MarkFailedWithBackoffAsync / IBackoffSchedulableOutboxStore did not exist.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxProcessorBackoffExclusionShould : UnitTestBase
{
	private static readonly MethodInfo PerformBatchDatabaseOperationsAsyncMethod = typeof(OutboxProcessor)
		.GetMethod("PerformBatchDatabaseOperationsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private PerformBatchDatabaseOperationsAsync method.");

	[Fact]
	public async Task ScheduleBackoffOnGenuineFailure_ButNotOnACircuitBreakerOpenTransient()
	{
		// A store that CAN schedule backoff, so the genuine-failure path routes to MarkFailedWithBackoffAsync.
		var store = A.Fake<IOutboxStore>(f => f
			.Implements<IDeadLetterableOutboxStore>()
			.Implements<IBackoffSchedulableOutboxStore>());
		await using var processor = CreateProcessor(store);

		// Two failures in one batch: one GENUINE (ApplyBackoff:true), one CB-open transient (ApplyBackoff:false).
		await InvokePrivateAsync(
			PerformBatchDatabaseOperationsAsyncMethod,
			processor,
			new List<string>(),
			new List<(string, int, bool)> { ("genuine-fail", 2, true), ("cb-open-transient", 2, false) },
			new List<(string, int)>(),
			CancellationToken.None);

		var schedulable = (IBackoffSchedulableOutboxStore)store;

		// Genuine failure → backoff scheduled (NextAttemptAt), NOT a plain mark-failed.
		A.CallTo(() => schedulable.MarkFailedWithBackoffAsync(
				"genuine-fail", ErrorConstants.RetryAttempt, 2, A<DateTimeOffset>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => store.MarkFailedAsync("genuine-fail", A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// CB-open transient → plain mark-failed (attempt unchanged), NO backoff scheduled. This is the
		// discriminator a vacuous "always schedule" impl would violate.
		A.CallTo(() => store.MarkFailedAsync(
				"cb-open-transient", ErrorConstants.RetryAttempt, 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => schedulable.MarkFailedWithBackoffAsync(
				"cb-open-transient", A<string>._, A<int>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static OutboxProcessor CreateProcessor(IOutboxStore outboxStore)
	{
		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = 3,
			EnableBatchDatabaseOperations = true,
		});

		return new OutboxProcessor(
			options,
			outboxStore,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: null,
			deadLetterQueue: null,
			circuitBreakerRegistry: null);
	}

	private static async Task InvokePrivateAsync(MethodInfo method, object target, params object?[] args)
	{
		try
		{
			var task = (Task?)method.Invoke(target, args)
				?? throw new InvalidOperationException($"Expected Task return from '{method.Name}'.");
			await task;
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}
}
