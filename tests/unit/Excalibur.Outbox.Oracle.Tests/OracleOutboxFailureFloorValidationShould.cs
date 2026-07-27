// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// lz7us9 — the Oracle 5th conformance arm: the cross-options Lamport-R1 floor-vs-poll misconfiguration
/// validator. <c>OracleOutboxStoreOptions.FailureBackoffFloorSeconds</c> (F) must exceed the effective drain
/// poll interval on every active path — <c>effectivePoll = partitionActive ? Max(processing, partition) :
/// processing</c> — or a failed message is re-claimable on the next poll (the zero-backoff retry hot-loop the
/// floor exists to prevent). Uniform with the Postgres/SqlServer/InMemory validators (SA-confirmed, 34487).
/// </summary>
/// <remarks>
/// NON-VACUOUS + wording-robust. The partition-poll arm sets F ABOVE the processing poll but at/below the
/// partition poll: a validator checking only the processing poll would return Success, so the failure can only
/// come from the partition branch — the failure itself proves partition enforcement, independent of the
/// message text. The liveness control proves the validator does not over-reject. RED against a validator
/// missing the floor-vs-poll invariant.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OracleOutboxFailureFloorValidationShould
{
	private static OracleOutboxStoreOptionsValidator CreateValidator(
		double processingPollSeconds = 5,
		(OutboxPartitionStrategy Strategy, double PollSeconds)? partition = null)
	{
		var processing = Options.Create(new OutboxProcessingOptions
		{
			PollingInterval = TimeSpan.FromSeconds(processingPollSeconds),
		});

		var partitionOptions = new OutboxPartitionOptions();
		if (partition is { } p)
		{
			partitionOptions.Strategy = p.Strategy;
			partitionOptions.PollingInterval = TimeSpan.FromSeconds(p.PollSeconds);
		}

		return new OracleOutboxStoreOptionsValidator(processing, Options.Create(partitionOptions));
	}

	private static OracleOutboxStoreOptions ValidOptions() => new()
	{
		OutboxTableName = "valid_outbox",
		DeadLetterTableName = "valid_dead_letters",
		ReservationTimeout = 300,
	};

	// SAFETY (processing-poll bound, partitioning OFF): F <= processing poll => fail.
	[Fact]
	public void FailWhenFailureBackoffFloorAtOrBelowProcessingPoll()
	{
		var validator = CreateValidator(processingPollSeconds: 10);
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 5;

		var result = validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("FailureBackoffFloorSeconds");
		result.FailureMessage.ShouldContain("PollingInterval");
	}

	// SAFETY (partition-poll bound): F above processing poll (5s) but at/below partition poll (20s). Only the
	// partition-poll branch can reject F(10) > processing(5) — the failure proves partition enforcement.
	[Fact]
	public void FailWhenFailureBackoffFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll()
	{
		var validator = CreateValidator(
			processingPollSeconds: 5,
			partition: (OutboxPartitionStrategy.ByTenantHash, 20));
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 10;

		var result = validator.Validate(null, options);

		result.Failed.ShouldBeTrue(
			"F above the processing poll but at/below the partition poll must fail on the partition-poll branch");
	}

	// LIVENESS (no over-rejection): F strictly above every active poll => Success.
	[Fact]
	public void SucceedWhenFailureBackoffFloorAboveEveryActivePoll()
	{
		var validator = CreateValidator(
			processingPollSeconds: 5,
			partition: (OutboxPartitionStrategy.ByTenantHash, 20));
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 30;

		var result = validator.Validate(null, options);

		result.ShouldBe(ValidateOptionsResult.Success);
	}
}
