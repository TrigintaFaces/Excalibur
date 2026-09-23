// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Oracle;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Real-Oracle lock on the FENCED completion path: a superseded tenure must not be able to record a
/// failure or bury a message, and a live tenure must still be able to do both.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this runs against a real Oracle rather than a fake.</b> The guarantee lives in a PL/SQL block
/// whose fence MERGE and guarded mutation share one execution. A fake returns whatever it was told and
/// cannot exhibit a high-water rejection, so no unit test can distinguish a block that fences from one
/// that merely looks like it does. The equivalent SQL Server lock failed three of five arms on its first
/// run over a duplicate declaration that made every fenced failure write throw -- a defect no amount of
/// source reading had caught, and which the package's other 100-plus tests sailed past because none of
/// them call these members.
/// </para>
/// <para>
/// <b>Oracle-specific hazards this pins.</b> Burial MOVES the row to a separate dead-letter table rather
/// than setting a status column, so a refused burial must leave the outbox row in place AND write nothing
/// to the dead-letter table; gating only the delete would leave a message simultaneously buried and
/// deliverable. And the claim term here is an exact match rather than the process prefix the unfenced
/// statement must use, so the arms below distinguish a foreign claim from a superseded tenure -- two
/// refusals that mean opposite things to the caller.
/// </para>
/// <para>
/// <b>Both directions on every arm.</b> A block that refused everything would satisfy every safety
/// assertion here, so each refusal is paired with the honest write it must still permit.
/// </para>
/// </remarks>
[Collection(OracleOutboxCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleFencedCompletionPathShould
{
	private const int MaxAttempts = 5;

	private readonly OracleOutboxStoreContainerFixture _fixture;

	public OracleFencedCompletionPathShould(OracleOutboxStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task RecordTheFailure_ForTheTenureThatHoldsTheClaim()
	{
		// LIVENESS. Without this arm every refusal below is satisfied by a block that never writes at all,
		// which is the cheapest way to look fenced while being broken.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var (message, claim) = await StageAndClaimAsync(store).ConfigureAwait(false);

			var outcome = await store.MarkFailedAsync(
				message.Id, "boom", 1, null, new OutboxWriteAuthority(5, claim), CancellationToken.None)
				.ConfigureAwait(false);

			outcome.ShouldBe(
				OutboxCompletionOutcome.Applied,
				"the tenure holds the claim and presents a token the fence accepts, so the failure must be recorded");

			var failed = await ((IOutboxStoreAdmin)store)
				.GetAllTenantsFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false);
			_ = failed.FirstOrDefault(m => m.Id == message.Id).ShouldNotBeNull(
				"an Applied outcome must correspond to a row a later reader can actually see");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task RefuseTheFailure_ForASupersededTenure()
	{
		// SAFETY. A newer tenure has advanced the high-water; the older one must be refused and told its
		// whole tenure is void rather than that this one message was re-granted.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var (first, firstClaim) = await StageAndClaimAsync(store).ConfigureAwait(false);
			var live = await store.MarkFailedAsync(
				first.Id, "by the live tenure", 1, null, new OutboxWriteAuthority(9, firstClaim), CancellationToken.None)
				.ConfigureAwait(false);
			live.ShouldBe(OutboxCompletionOutcome.Applied, "the live tenure establishes the high-water at 9");

			var (second, secondClaim) = await StageAndClaimAsync(store).ConfigureAwait(false);
			var superseded = await store.MarkFailedAsync(
				second.Id, "by a superseded tenure", 2, null, new OutboxWriteAuthority(4, secondClaim), CancellationToken.None)
				.ConfigureAwait(false);

			superseded.ShouldBe(
				OutboxCompletionOutcome.FenceRefused,
				"GREATEST keeps 9, so a tenure presenting 4 must be refused");

			var failed = await ((IOutboxStoreAdmin)store)
				.GetAllTenantsFailedMessagesAsync(100, null, 10, CancellationToken.None).ConfigureAwait(false);
			failed.ShouldNotContain(
				m => m.Id == second.Id,
				"a refused failure report must write nothing at all, not merely report a refusal");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task ReportClaimLost_WhenTheTenureIsLiveButTheClaimIsNot()
	{
		// The two gates are independent, and this arm proves neither subsumes the other: the fence accepts
		// and the claim does not, so a block carrying only the fence would wrongly report Applied.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var (message, _) = await StageAndClaimAsync(store).ConfigureAwait(false);

			var outcome = await store.MarkFailedAsync(
				message.Id, "wrong claim", 1, null,
				new OutboxWriteAuthority(7, "some-dispatcher:not-the-claim-that-was-granted"),
				CancellationToken.None).ConfigureAwait(false);

			outcome.ShouldBe(
				OutboxCompletionOutcome.ClaimLost,
				"the row exists and the tenure is live, so only the claim term can have refused, and the caller "
				+ "must be told to continue with its other messages rather than to stop draining");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task BuryForALiveTenure_AndRefuseASupersededOne()
	{
		// Burial MOVES the row on Oracle, so a refused burial must leave the outbox row present. Asserting
		// only the returned outcome would pass for a block that copied the row and then refused the delete.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			// The FIRST token on a fresh scope ESTABLISHES the fence rather than being refused by it.
			var (first, _) = await StageAndClaimAsync(store).ConfigureAwait(false);
			var established = await store.MarkDeadLetteredAsync(
				first.Id, "establishes the fence", 3, CancellationToken.None).ConfigureAwait(false);
			established.ShouldBe(
				OutboxCompletionOutcome.Applied,
				"the first token on a fresh scope establishes the fence");

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

			// The row must still be THERE. On Oracle a burial removes it, so its continued presence is the
			// only evidence the refusal suppressed the move rather than half-performing it.
			//
			// Asserted by a direct existence query, NOT by re-claiming. An earlier draft called
			// GetUnsentMessagesAsync and failed: that statement returns only rows whose reservation has
			// lapsed, and StageAndClaimAsync reserves for 300 seconds, so the row was present and simply
			// not claimable. Claimability and presence are different properties, and the one this arm is
			// about is presence.
			(await CountInOutboxAsync(third.Id).ConfigureAwait(false)).ShouldBe(
				1,
				"a refused burial must leave the outbox row in place");

			// And the other half of the move must not have happened either. A block that inserted into the
			// dead-letter table and skipped only the delete would leave the message both buried and
			// deliverable, which is worse than either outcome alone.
			(await CountInDeadLettersAsync(third.Id).ConfigureAwait(false)).ShouldBe(
				0,
				"a refused burial must write nothing to the dead-letter table");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task RefuseADefaultedAuthority_BeforeItReachesTheBlock()
	{
		// OutboxWriteAuthority is a struct, so its default cannot be intercepted by a constructor, and a zero
		// token would otherwise be ACCEPTED on a fresh scope: the MERGE creates the mark from the presented
		// value and GREATEST returns it unchanged. Guarding at the member makes that unreachable rather than
		// relying on the claim term to fail for an unrelated reason.
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var (message, _) = await StageAndClaimAsync(store).ConfigureAwait(false);

			_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
				await store.MarkFailedAsync(
					message.Id, "defaulted", 1, null, default, CancellationToken.None).ConfigureAwait(false))
				.ConfigureAwait(false);
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	// The statement literal sits at the assignment site in each method. Factoring it into a shared helper
	// that takes the text as a parameter reads better but trips the injection analyzer, which cannot tell a
	// fixture constant from consumer input -- and suppressing that analyzer on a SQL-writing test is a worse
	// trade than two near-identical methods.
	private async Task<int> CountInOutboxAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM OUTBOX WHERE message_id = :MessageId";
		AddMessageId(command, messageId);

		return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	private async Task<int> CountInDeadLettersAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM OUTBOX_DEAD_LETTERS WHERE message_id = :MessageId";
		AddMessageId(command, messageId);

		return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	private static void AddMessageId(System.Data.Common.DbCommand command, string messageId)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = "MessageId";
		parameter.Value = messageId;
		_ = command.Parameters.Add(parameter);
	}

	private async Task<(OutboundMessage Message, string ClaimIdentity)> StageAndClaimAsync(OracleOutboxStore store)
	{
		var message = new OutboundMessage("Test.Fenced", "payload"u8.ToArray(), "test-queue");

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var reserved = claimed.FirstOrDefault(m => m.Id == message.Id);
		_ = reserved.ShouldNotBeNull("the freshly-staged message must be claimable");

		// The claim identity is taken from what the RESERVATION actually stamped, not reconstructed. The
		// store composes it per claim, and a test that rebuilt that shape by hand would keep passing against
		// a store that had stopped stamping it.
		reserved.DispatcherId.ShouldNotBeNullOrWhiteSpace(
			"reserving must stamp the claim identity the fenced write is scoped to");

		return (message, reserved.DispatcherId!);
	}

	private async Task<OracleOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - this real-infra fencing lock is NEVER skipped. A skip here "
			+ "would leave the completion-path PL/SQL unexecuted, which is exactly the state that let a fatal "
			+ "declaration error through on the SQL Server equivalent.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = MaxAttempts,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}
}
