// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Keystone real-infrastructure lock for PostgreSQL (owxhc8 CreatedAt-preservation + su6232 per-partition
/// ordering). su6232's ordering fix is PG/Oracle-only, so this behavior CANNOT be proven on SQL Server —
/// it must be locked on a real Postgres container.
/// </summary>
/// <remarks>
/// Never skipped: the fixture fails fast when Docker is unavailable rather than passing silently.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxKeystoneRoundTripShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxKeystoneRoundTripShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task PreserveTheCallersCreatedAt_AndCanonicalFields_OnStageThenReload()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// Explicit historical CreatedAt — the RED-pre-fix arm: pre-fix, occurred_on defaulted to NOW()
		// instead of binding the caller's value, so a pre-fix impl would reload ~today, not 2020.
		var createdAt = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
		// A PAST scheduled instant so the message is drainable (reserve excludes scheduled_at > NOW()).
		var scheduledAt = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);

		var message = new OutboundMessage("Keystone.MessageType", "keystone-payload"u8.ToArray(), "keystone-destination")
		{
			Id = Guid.NewGuid().ToString(),
			CreatedAt = createdAt,
			ScheduledAt = scheduledAt,
			TenantId = "tenant-keystone",
			CorrelationId = "corr-keystone",
			CausationId = "cause-keystone",
			Priority = 7,
			PartitionKey = "partition-A",
			GroupKey = "group-A",
			SequenceNumber = 42,
			TargetTransports = "kafka,rabbitmq",
			IsMultiTransport = true,
		};
		message.Headers["header-one"] = "value-one";

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var reloaded = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
			.Single(m => m.Id == message.Id);

		// FULL consumer field set byte-identical through the real drain-reload — catches the Dapper
		// alias silent-drop CLASS (a mismatched/absent RETURNING alias hydrates nothing, no error), not
		// just the CreatedAt/SequenceNumber instances. (RetryCount/Attempts is store-managed → normalized
		// to 0 on fresh stage and NOT restored on drain — out of owxhc8's fresh-stage AC; the re-drain
		// RetryCount-loss is tracked separately.)
		reloaded.CreatedAt.ToUniversalTime().ShouldBe(createdAt.ToUniversalTime());
		reloaded.ScheduledAt!.Value.ToUniversalTime().ShouldBe(scheduledAt.ToUniversalTime());
		reloaded.MessageType.ShouldBe("Keystone.MessageType");
		reloaded.Payload.ShouldBe("keystone-payload"u8.ToArray());
		reloaded.Destination.ShouldBe("keystone-destination");
		reloaded.TenantId.ShouldBe("tenant-keystone");
		reloaded.CorrelationId.ShouldBe("corr-keystone");
		reloaded.CausationId.ShouldBe("cause-keystone");
		reloaded.Priority.ShouldBe(7);
		reloaded.PartitionKey.ShouldBe("partition-A");
		reloaded.GroupKey.ShouldBe("group-A");
		reloaded.SequenceNumber.ShouldBe(42);
		reloaded.TargetTransports.ShouldBe("kafka,rabbitmq");
		reloaded.IsMultiTransport.ShouldBeTrue();
		reloaded.Headers.ShouldContainKey("header-one");
	}

	[Fact]
	public async Task ClaimSamePartitionMessages_InAscendingSequenceNumberOrder_AcrossSuccessiveSingleClaims()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		const string partition = "ordering-partition";
		// Stage OUT of sequence order (3, 1, 2) within one partition.
		foreach (var seq in new long[] { 3, 1, 2 })
		{
			await store.StageMessageAsync(
				new OutboundMessage("Ordering.Type", "p"u8.ToArray(), "dest")
				{
					Id = $"ord-{seq}",
					PartitionKey = partition,
					SequenceNumber = seq,
				},
				CancellationToken.None).ConfigureAwait(false);
		}

		// su6232 guarantees same-partition messages are CLAIMED in ascending sequence_number order (the
		// reserve CTE's `ORDER BY partition_key, sequence_number, occurred_on` + `LIMIT`). Intra-batch
		// RETURN order is NOT guaranteed (`UPDATE … RETURNING` does not preserve the CTE order — the
		// SqlServer sibling documents this), so we assert the deterministic guarantee: successive
		// single-message claims surface seq 1, then 2, then 3 — never the [3,1,2] insertion order.
		var claimed = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var batch = (await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false)).ToList();
			batch.Count.ShouldBe(1, "each single claim must surface exactly one still-unclaimed message.");
			claimed.Add(batch[0].Id);
		}

		// Safety: ascending per-partition claim order. Liveness: all three are claimable (loop got 3).
		claimed.ShouldBe(new[] { "ord-1", "ord-2", "ord-3" });
	}

	private async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - this keystone real-infra lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			// Long reservation so a claimed message stays reserved across the successive single-claim loop
			// (the claim-order assertion depends on already-claimed rows not reappearing mid-loop).
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}
}
