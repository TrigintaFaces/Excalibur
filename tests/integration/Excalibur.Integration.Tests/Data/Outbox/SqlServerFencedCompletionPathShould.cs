// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-SQL-Server lock on the FENCED completion path: a superseded tenure must not be able to record a
/// failure or bury a message, and a live tenure must still be able to do both.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this must run against a real server.</b> The guarantee is enforced by a single T-SQL statement
/// whose fence MERGE and guarded UPDATE share one transaction and one range lock. A fake returns whatever
/// it was told and cannot exhibit a high-water rejection at all, so no unit test can distinguish a
/// statement that fences from one that merely looks like it does. Until this file existed the
/// completion-path SQL had never executed anywhere.
/// </para>
/// <para>
/// <b>Both directions, deliberately.</b> A store that refused every write would satisfy every safety
/// assertion here, so each refusal is paired with the honest write it must still permit. A refusal alone is
/// not evidence of fencing; it is equally evidence of a statement that does nothing.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerFencedCompletionPathShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const int StatusFailed = 3;
	private const int StatusDeadLettered = 5;

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerFencedCompletionPathShould(SqlServerOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task RecordTheFailure_ForTheTenureThatHoldsTheClaim()
	{
		// LIVENESS. Without this arm every refusal asserted below is satisfied by a statement that never
		// writes at all, which is the cheapest way to look fenced while being broken.
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		var (message, claimIdentity) = await StageAndClaimAsync(store).ConfigureAwait(false);

		var outcome = await store.MarkFailedAsync(
			message.Id,
			"boom",
			1,
			null,
			new OutboxWriteAuthority(5, claimIdentity),
			CancellationToken.None).ConfigureAwait(false);

		outcome.ShouldBe(
			OutboxCompletionOutcome.Applied,
			"the tenure holds the claim and presents a token the fence accepts, so the failure must be recorded");
		(await ReadStatusAsync(message.Id).ConfigureAwait(false)).ShouldBe(StatusFailed);
	}

	[Fact]
	public async Task RefuseTheFailure_ForASupersededTenure()
	{
		// SAFETY. A newer tenure has advanced the high-water; the older one must be refused, and told to stop
		// draining entirely rather than to continue with the rest of its batch.
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		var (message, claimIdentity) = await StageAndClaimAsync(store).ConfigureAwait(false);

		// A live tenure at 9 establishes the high-water. Its own write is the liveness half.
		var live = await store.MarkFailedAsync(
			message.Id, "by the live tenure", 1, null,
			new OutboxWriteAuthority(9, claimIdentity),
			CancellationToken.None).ConfigureAwait(false);
		live.ShouldBe(OutboxCompletionOutcome.Applied);

		var (second, secondClaim) = await StageAndClaimAsync(store).ConfigureAwait(false);

		// The superseded tenure presents a LOWER token. The fence keeps 9, so the comparison rejects 4.
		var superseded = await store.MarkFailedAsync(
			second.Id, "by a superseded tenure", 2, null,
			new OutboxWriteAuthority(4, secondClaim),
			CancellationToken.None).ConfigureAwait(false);

		superseded.ShouldBe(
			OutboxCompletionOutcome.FenceRefused,
			"a tenure the fence has moved past must be refused, and must learn its whole tenure is void rather "
			+ "than that this one message was re-granted");
		(await ReadStatusAsync(second.Id).ConfigureAwait(false)).ShouldNotBe(
			StatusFailed,
			"a refused failure report must leave the row alone");
	}

	[Fact]
	public async Task ReportClaimLost_WhenTheTenureIsLiveButTheClaimIsNot()
	{
		// The two gates are independent, and this arm proves neither subsumes the other: the tenure is
		// current, so the fence accepts, and the claim is foreign, so the write still must not land. A
		// statement carrying only the fence would wrongly report Applied here.
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		var (message, _) = await StageAndClaimAsync(store).ConfigureAwait(false);

		var outcome = await store.MarkFailedAsync(
			message.Id, "wrong claim", 1, null,
			new OutboxWriteAuthority(7, "proc-1:not-the-claim-that-was-granted"),
			CancellationToken.None).ConfigureAwait(false);

		outcome.ShouldBe(
			OutboxCompletionOutcome.ClaimLost,
			"the row exists and the tenure is live, so the only thing that can have refused this write is the "
			+ "claim term, and the caller must be told to continue with its other messages rather than stop");
	}

	[Fact]
	public async Task BuryForALiveTenure_AndRefuseASupersededOne()
	{
		// The dead-letter member is where an accepted stale token does the most damage: a burial the live
		// tenure then contradicts by delivering leaves the message both delivered and sitting unreplayed.
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		// The FIRST token on a fresh scope ESTABLISHES the fence rather than being refused by it, so this arm
		// also pins that boundary: 3 is accepted here precisely because nothing preceded it.
		var (first, _) = await StageAndClaimAsync(store).ConfigureAwait(false);
		var established = await store.MarkDeadLetteredAsync(
			first.Id, "establishes the fence", 3, CancellationToken.None).ConfigureAwait(false);
		established.ShouldBe(
			OutboxCompletionOutcome.Applied,
			"the first token on a fresh scope establishes the fence");
		(await ReadStatusAsync(first.Id).ConfigureAwait(false)).ShouldBe(StatusDeadLettered);

		var (second, _) = await StageAndClaimAsync(store).ConfigureAwait(false);
		var live = await store.MarkDeadLetteredAsync(
			second.Id, "by the live tenure", 12, CancellationToken.None).ConfigureAwait(false);
		live.ShouldBe(
			OutboxCompletionOutcome.Applied,
			"a tenure at or above the high-water must still be able to bury");

		var (third, _) = await StageAndClaimAsync(store).ConfigureAwait(false);
		var superseded = await store.MarkDeadLetteredAsync(
			third.Id, "by a superseded tenure", 6, CancellationToken.None).ConfigureAwait(false);

		superseded.ShouldBe(
			OutboxCompletionOutcome.FenceRefused,
			"the fence stands at 12, so a tenure presenting 6 must not be able to bury anything");
		(await ReadStatusAsync(third.Id).ConfigureAwait(false)).ShouldNotBe(
			StatusDeadLettered,
			"a refused burial must leave the row alone; reporting the refusal while still writing would be the "
			+ "worst of both");
	}

	[Fact]
	public async Task RefuseADefaultedAuthority_BeforeItReachesTheStatement()
	{
		// OutboxWriteAuthority is a struct, so its default cannot be intercepted by a constructor, and a zero
		// token would otherwise be ACCEPTED by the fence on a fresh scope: the MERGE creates the mark from the
		// presented value, which then equals itself. Guarding at the member is what makes that unreachable,
		// rather than relying on the claim term to fail for an unrelated reason.
		await EnsureReadyAsync().ConfigureAwait(false);
		var store = CreateStore("proc-1");

		var (message, _) = await StageAndClaimAsync(store).ConfigureAwait(false);

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await store.MarkFailedAsync(
				message.Id, "defaulted", 1, null, default, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	private async Task<(OutboundMessage Message, string ClaimIdentity)> StageAndClaimAsync(SqlServerOutboxStore store)
	{
		var message = new OutboundMessage("Test.MessageType", "test-payload"u8.ToArray(), "test-queue")
		{
			Id = Guid.NewGuid().ToString(),
		};

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		claimed.ShouldContain(m => m.Id == message.Id, "the staged message must be claimable");

		// The claim identity is read BACK from the row rather than reconstructed. The store composes it from
		// the processor id and the claim id, and a test that rebuilt that shape by hand would keep passing
		// against a store that had stopped stamping it.
		var leasedBy = await ReadLeasedByAsync(message.Id).ConfigureAwait(false);
		leasedBy.ShouldNotBeNullOrWhiteSpace(
			"claiming must stamp the claim identity the fenced write is scoped to");

		return (message, leasedBy!);
	}

	private async Task<string?> ReadLeasedByAsync(string id)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT LeasedBy FROM [dbo].[OutboxMessages] WHERE Id = @id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@id", id));

		var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return value is null or DBNull ? null : (string)value;
	}

	private async Task<int> ReadStatusAsync(string id)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT Status FROM [dbo].[OutboxMessages] WHERE Id = @id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@id", id));

		return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

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
}
