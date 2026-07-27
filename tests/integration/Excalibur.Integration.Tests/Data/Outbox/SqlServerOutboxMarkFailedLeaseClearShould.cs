// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA2100 // SQL strings are constant; the message id is passed as a parameter.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure lock for the SQL Server outbox <c>MarkFailed</c> lease-release + ownership guard
/// (j1wfzu). Exercises the emitted database behavior against a live SQL Server container — never mocked,
/// never skipped — because the fix is a DB-behavior change that a CommandText assertion cannot prove.
/// </summary>
/// <remarks>
/// <para>
/// The transition under test releases the lease (<c>LeasedAt</c>/<c>LeasedBy</c> cleared) on failure, in
/// parity with the sent/dead-lettered terminals, and guards the update on ownership
/// (<c>WHERE Id = @MessageId AND (LeasedBy IS NULL OR LeasedBy = @LeasedBy)</c>) so a stale peer cannot
/// mark-failed a row another processor holds.
/// </para>
/// <para>
/// Each assertion is paired safety∧liveness: the lease is cleared and the message is accounted as failed
/// (safety) while the legitimate owner and an unleased row are BOTH still allowed to mark-failed
/// (liveness — the guard did not wedge the honest paths), and a foreign processor is refused (safety).
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxMarkFailedLeaseClearShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const int StatusFailed = 3;

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxMarkFailedLeaseClearShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ClearTheLeaseInTheDatabase_AndReportFailedNotInflight_WhenTheOwnerMarksFailed()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		var message = NewMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Claim -> the row is leased by proc-1 (precondition for a meaningful lease-clear).
		var claimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		claimed.ShouldContain(m => m.Id == message.Id, "the staged message must be claimable.");
		var afterClaim = await ReadRowAsync(message.Id).ConfigureAwait(false);
		afterClaim.LeasedBy.ShouldBe("proc-1", "claiming must set the lease to the claiming processor.");

		// Act.
		await store.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

		// Safety: the lease is released in the database.
		var afterFail = await ReadRowAsync(message.Id).ConfigureAwait(false);
		afterFail.LeasedAt.ShouldBeNull("MarkFailed must clear LeasedAt (parity with the other terminals).");
		afterFail.LeasedBy.ShouldBeNull("MarkFailed must clear LeasedBy (parity with the other terminals).");
		afterFail.Status.ShouldBe(StatusFailed);

		// Liveness half of the accounting: statistics report it failed, not lingering in-flight.
		var stats = await store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.FailedMessageCount.ShouldBe(1);
		stats.SendingMessageCount.ShouldBe(0, "a failed message must not be over-reported as in-flight.");
	}

	[Fact]
	public async Task StillMarkFailed_ForTheLeaseOwner_AndForAnUnleasedRow()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		// (a) Liveness: an unleased staged row (never claimed) is still markable — the guard's
		//     "LeasedBy IS NULL" branch must not wedge honest single-processor use.
		var unleased = NewMessage();
		await store.StageMessageAsync(unleased, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(unleased.Id, "e", 1, CancellationToken.None).ConfigureAwait(false);
		(await ReadRowAsync(unleased.Id).ConfigureAwait(false)).Status
			.ShouldBe(StatusFailed, "an unleased row must still be markable (LeasedBy IS NULL branch).");

		// (b) Liveness: the lease owner is not blocked by its own guard.
		var owned = NewMessage();
		await store.StageMessageAsync(owned, CancellationToken.None).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		await store.MarkFailedAsync(owned.Id, "e", 1, CancellationToken.None).ConfigureAwait(false);
		var row = await ReadRowAsync(owned.Id).ConfigureAwait(false);
		row.Status.ShouldBe(StatusFailed, "the lease owner must be able to mark its own claimed row failed.");
		row.LeasedBy.ShouldBeNull("the owner's mark-failed also releases the lease.");
	}

	[Fact]
	public async Task NotModify_ARowLeasedByAnotherProcessor_WhenAForeignProcessorMarksFailed()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var proc1 = CreateStore("proc-1");
		var proc2 = CreateStore("proc-2");

		var message = NewMessage();
		await proc1.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// proc-1 claims -> the row is leased by proc-1.
		_ = (await proc1.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		(await ReadRowAsync(message.Id).ConfigureAwait(false)).LeasedBy.ShouldBe("proc-1");

		// proc-2 attempts to mark proc-1's leased row failed. The ownership guard matches zero rows, so
		// the call completes without error but changes nothing.
		await proc2.MarkFailedAsync(message.Id, "hijack", 9, CancellationToken.None).ConfigureAwait(false);

		// Safety: the row is untouched — still leased by proc-1, not marked failed by the foreign processor.
		var afterForeign = await ReadRowAsync(message.Id).ConfigureAwait(false);
		afterForeign.LeasedBy.ShouldBe("proc-1", "a foreign processor must not steal/clear another's lease.");
		afterForeign.Status.ShouldNotBe(StatusFailed, "a foreign processor must not mark another's row failed.");
	}

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue")
		{
			Id = Guid.NewGuid().ToString(),
		};

	private async Task EnsureReadyAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - this real-infra lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private SqlServerOutboxStore CreateStore(string processorId)
	{
		var options = Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			ProcessorId = processorId,
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
		});

		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task<(DateTimeOffset? LeasedAt, string? LeasedBy, int Status)> ReadRowAsync(string id)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT LeasedAt, LeasedBy, Status FROM [dbo].[OutboxMessages] WHERE Id = @id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@id", id));

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue($"row '{id}' must exist.");

		var leasedAt = await reader.IsDBNullAsync(0).ConfigureAwait(false) ? (DateTimeOffset?)null : reader.GetDateTimeOffset(0);
		var leasedBy = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? null : reader.GetString(1);
		var status = reader.GetInt32(2);

		return (leasedAt, leasedBy, status);
	}
}
