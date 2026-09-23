// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// eg3gz8 — the remaining two arms (of four) against the REAL <see cref="OracleOutboxStore"/> path:
/// (3) backward-compatibility — a row written before this code ran is still readable, and (4) liveness —
/// the scheduled-sweep comparison predicate selects the correct rows across an offset boundary, not merely
/// stores everything. Arms (1) and (2) — non-zero-offset and offset-zero round-trip — are measured at the
/// Dapper/ODP.NET layer in isolation by <see cref="OracleDapperDateTimeOffsetDefaultBehaviorShould"/>
/// (production registers no custom DateTimeOffset type handler; that class runs without one to measure
/// exactly what shipped code gets). Both there and here: PASS. BackendDeveloper's "Oracle is unaffected"
/// claim is confirmed as MEASURED, not merely reasoned.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Collection(OracleOutboxCollection.Name)]
public sealed class OracleOutboxTimestampOffsetShould
{
	private readonly OracleOutboxStoreContainerFixture _fixture;

	public OracleOutboxTimestampOffsetShould(OracleOutboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// ARM 3 (backward compatibility) — a row written directly via SQL, bypassing the store entirely (the
	/// shape of data that predates any store-level change), is still correctly read back by the current
	/// <see cref="OracleOutboxStore.GetAllTenantsScheduledMessagesAsync"/> path.
	/// </summary>
	[Fact]
	public async Task ReadAPreExistingRowWrittenOutsideTheStore()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var id = "eg3gz8-legacy-row";
			var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToOffset(TimeSpan.FromHours(-5));

			await using (var connection = _fixture.CreateConnection())
			{
				await connection.OpenAsync().ConfigureAwait(false);
				await connection.ExecuteAsync(
					$"""
					INSERT INTO {_fixture.OutboxTableName}
					    (message_id, message_type, message_body, destination, occurred_on, attempts, scheduled_at, tenant_id)
					VALUES
					    (:Id, 'Legacy.Type', UTL_RAW.CAST_TO_RAW('x'), 'dest', SYSTIMESTAMP, 0, :ScheduledAt, '__untenanted__')
					""",
					new { Id = id, ScheduledAt = scheduledAt }).ConfigureAwait(false);
			}

			var due = await store.GetAllTenantsScheduledMessagesAsync(
				DateTimeOffset.UtcNow.AddMinutes(5), 10, CancellationToken.None).ConfigureAwait(false);

			var found = due.FirstOrDefault(m => m.Id == id);
			found.ShouldNotBeNull(
				"a row written outside the store (simulating one written before this code ran) must still be "
				+ "readable by the current GetAllTenantsScheduledMessagesAsync path — no silent format break.");
			found.ScheduledAt!.Value.ToUniversalTime().ShouldBe(
				scheduledAt.ToUniversalTime(), TimeSpan.FromSeconds(1),
				"the instant must be preserved exactly, not shifted by the offset under which it was originally written.");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// ARM 4 (liveness) — the sweep's comparison predicate (<c>scheduled_at &lt;= :Cutoff</c>) must select
	/// rows by their true INSTANT, not by wall-clock component comparison. Constructs two messages where the
	/// wall-clock hour ordering is the OPPOSITE of the true UTC-instant ordering, and proves the cutoff
	/// selects the one that is actually due.
	/// </summary>
	[Fact]
	public async Task SelectTheCorrectRowsAcrossAnOffsetBoundary()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var now = DateTimeOffset.UtcNow;

			// Truly EARLIER (due), expressed with a large positive offset so its local wall-clock hour reads
			// numerically LARGER than the other message's.
			var dueMessage = new OutboxMessage
			{
				MessageId = "eg3gz8-due",
				MessageType = "T",
				MessageMetadata = "{}",
				MessageBody = [1],
				CreatedAt = now,
			};
			var dueAt = now.AddMinutes(-1).ToOffset(TimeSpan.FromHours(9));
			await store.ScheduleMessageAsync(dueMessage, dueAt, CancellationToken.None).ConfigureAwait(false);

			// Truly LATER (not yet due), expressed with a negative offset so its local wall-clock hour reads
			// numerically SMALLER than the due message's — the trap a wall-clock-component comparison falls into.
			var notDueMessage = new OutboxMessage
			{
				MessageId = "eg3gz8-not-due",
				MessageType = "T",
				MessageMetadata = "{}",
				MessageBody = [1],
				CreatedAt = now,
			};
			var notDueAt = now.AddHours(1).ToOffset(TimeSpan.FromHours(-5));
			await store.ScheduleMessageAsync(notDueMessage, notDueAt, CancellationToken.None).ConfigureAwait(false);

			var due = await store.GetAllTenantsScheduledMessagesAsync(now, 10, CancellationToken.None).ConfigureAwait(false);
			var dueIds = due.Select(m => m.Id).ToList();

			dueIds.ShouldContain(dueMessage.MessageId, "the truly-earlier instant must be selected as due, regardless of its offset's wall-clock hour.");
			dueIds.ShouldNotContain(notDueMessage.MessageId, "the truly-later instant must NOT be selected as due — a column that stores everything and matches nothing (or everything) would pass arms 1-3 and fail here.");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	private async Task<OracleOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — eg3gz8 is a real-infra MEASUREMENT, not reasoning, and is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}
}
