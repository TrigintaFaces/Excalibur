// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Infrastructure;

#pragma warning disable CA2100 // SQL strings are constant; the message id is passed as a parameter.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure locks on the SINGLE-MESSAGE SQL Server outbox failure transitions -- the plain
/// <c>MarkFailedAsync</c> and the <c>MarkFailedWithBackoffAsync</c> the processor prefers whenever the
/// backoff capability is present -- proving a superseded tenure cannot move a DELIVERED message back to
/// Failed.
/// </summary>
/// <remarks>
/// <para>
/// The interleaving these arms construct is the one the outbox cannot survive silently. A dispatcher claims
/// a message, is paused past its lease, and wakes holding a failure report for work it no longer owns. By
/// then a peer has re-claimed the message and DELIVERED it. If the late failure report lands, the message
/// returns to the claim pool and is delivered a second time -- a duplicate manufactured by our own
/// bookkeeping rather than by any transport.
/// </para>
/// <para>
/// The ownership guard cannot close this on its own, and that is the whole reason these arms exist rather
/// than resting on the ownership locks that already ship. Marking sent RELEASES the lease
/// (<c>MarkMessageSentRequest</c> sets <c>LeasedBy = NULL</c>), so after delivery the row reads as unleased
/// to every dispatcher alive, the superseded tenure included. What refuses the late mark is the separate
/// not-already-sent guard, and these arms are red on exactly that guard and on nothing else.
/// </para>
/// <para>
/// The sibling <c>SqlServerOutboxMarkBatchFailedGuardsShould</c> proves the same property for the BATCH
/// path. The guard text is shared between the two (<c>OutboxFailureMark.Guards</c>), so that class already
/// binds the guard's database behaviour; what it cannot bind is whether the single-message statements still
/// compose the shared fragment rather than re-deriving guards of their own. Neither path may reach the
/// contract without it -- which overload a processor happens to call is not something a delivery guarantee
/// may depend on.
/// </para>
/// <para>
/// Run against a live SQL Server container, never skipped: whether an <c>UPDATE</c> matches a row is
/// emitted-database behaviour that no assertion over a command string can prove.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxSingleMarkFailedTerminalGuardShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const int StatusSent = 2;
	private const int StatusFailed = 3;

	/// <summary>Short enough that a superseded tenure is reachable inside a test, rather than in two minutes.</summary>
	private const int LeaseTimeoutSeconds = 2;

	/// <summary>The failure-anchored visibility floor. Any positive value works here; no arm waits it out.</summary>
	private const int FloorSeconds = 4;

	/// <summary>
	/// How long the second tenure keeps asking before it calls the expired lease un-reclaimable.
	/// </summary>
	/// <remarks>
	/// The lease window is judged on the SERVER's clock (<c>SYSUTCDATETIME()</c>) while any wait performed
	/// here is measured on the TEST HOST's, and a containerised database's clock stalls and catches up under
	/// load rather than advancing in step. Asking repeatedly asserts the re-claim without assuming the two
	/// clocks agree; a store that genuinely never releases an expired lease still fails here, because a clock
	/// that stalls always catches up.
	/// </remarks>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(30);

	/// <summary>How often the second tenure re-asks while the window is open.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(250);

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxSingleMarkFailedTerminalGuardShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY: a superseded tenure's plain <c>MarkFailedAsync</c> cannot revert a delivered message, and the
	/// message is not handed back to the drain.
	/// LIVENESS: the current tenure's genuine <c>MarkFailedAsync</c> still records the failure and its
	/// attempt count.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task NotRevertADeliveredMessage_WhenASupersededTenureReplaysItsMarkFailed()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var supersededTenure = CreateStore("proc-a");
		var currentTenure = CreateStore("proc-b");

		var delivered = NewMessage();
		var genuinelyFailed = NewMessage();
		await supersededTenure.StageMessageAsync(delivered, ct).ConfigureAwait(false);
		await supersededTenure.StageMessageAsync(genuinelyFailed, ct).ConfigureAwait(false);

		// Tenure A claims both, then stalls. Its lease is now the one that will expire underneath it.
		_ = (await supersededTenure.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		(await ReadRowAsync(delivered.Id).ConfigureAwait(false)).LeasedBy.ShouldStartWith("proc-a:");

		await WaitForSecondTenureToReclaimAsync(currentTenure, delivered.Id, ct).ConfigureAwait(false);

		// Tenure B delivers the message it now owns. Sent is terminal.
		await currentTenure.MarkSentAsync(delivered.Id, ct).ConfigureAwait(false);
		(await ReadRowAsync(delivered.Id).ConfigureAwait(false)).Status.ShouldBe(StatusSent);

		// Tenure A wakes and reports the failure it was holding for work it no longer owns.
		await supersededTenure.MarkFailedAsync(delivered.Id, "stale report from a superseded tenure", 9, ct)
			.ConfigureAwait(false);

		// SAFETY -- the delivered row is untouched.
		var afterStaleMark = await ReadRowAsync(delivered.Id).ConfigureAwait(false);
		afterStaleMark.Status.ShouldBe(
			StatusSent,
			"a delivered message must never be moved back to Failed by a mark from a superseded tenure. "
			+ "Marking sent releases the lease, so the ownership guard alone reads this row as unleased and "
			+ "admits the stale mark; the not-already-sent guard is what refuses it.");

		// SAFETY -- and the store does not hand it back for a second delivery, which is the harm the status
		// assertion above is a proxy for.
		var redrained = (await currentTenure.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		redrained.ShouldNotContain(
			m => m.Id == delivered.Id,
			"reverting a delivered message returns it to the claim pool. Asserting the status alone would "
			+ "pass a store that left Status at Sent while making the row claimable again.");

		// LIVENESS -- the guard did not disable the failure path for the tenure that genuinely owns the work.
		await currentTenure.MarkFailedAsync(genuinelyFailed.Id, "genuine failure", 3, ct).ConfigureAwait(false);

		var afterGenuine = await ReadRowAsync(genuinelyFailed.Id).ConfigureAwait(false);
		afterGenuine.Status.ShouldBe(
			StatusFailed,
			"the current tenure must still be able to record a genuine failure. A statement that matched "
			+ "nothing at all would satisfy every safety assertion above and deliver no bookkeeping.");
		afterGenuine.RetryCount.ShouldBe(
			3,
			"the recorded attempt count drives the dead-letter ceiling, so a failure that is written without "
			+ "it is a message that never terminates.");
		afterGenuine.LeasedBy.ShouldBeNull("recording the failure releases the lease the tenure held.");
	}

	/// <summary>
	/// SAFETY: the same for <c>MarkFailedWithBackoffAsync</c> -- the path the processor PREFERS whenever the
	/// backoff capability is present, and therefore the path most stale marks actually arrive on.
	/// LIVENESS: the current tenure's genuine backoff-scheduled failure still lands, with its schedule.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This overload composes its own <c>NextAttemptAt</c> assignment while sharing the guards, so it is the
	/// statement most likely to be re-derived by a future edit and lose them. Its sibling arm above cannot
	/// speak for it: they are two different statements built from the same fragments.
	/// </remarks>
	[Fact]
	public async Task NotRevertADeliveredMessage_WhenASupersededTenureReplaysItsBackoffScheduledMark()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var supersededTenure = CreateStore("proc-a");
		var currentTenure = CreateStore("proc-b");

		var delivered = NewMessage();
		var genuinelyFailed = NewMessage();
		await supersededTenure.StageMessageAsync(delivered, ct).ConfigureAwait(false);
		await supersededTenure.StageMessageAsync(genuinelyFailed, ct).ConfigureAwait(false);

		_ = (await supersededTenure.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		await WaitForSecondTenureToReclaimAsync(currentTenure, delivered.Id, ct).ConfigureAwait(false);

		await currentTenure.MarkSentAsync(delivered.Id, ct).ConfigureAwait(false);
		(await ReadRowAsync(delivered.Id).ConfigureAwait(false)).Status.ShouldBe(StatusSent);

		await supersededTenure.MarkFailedWithBackoffAsync(
			delivered.Id,
			"stale report from a superseded tenure",
			9,
			DateTimeOffset.UtcNow.AddSeconds(1),
			ct).ConfigureAwait(false);

		// SAFETY -- delivered stays delivered on the preferred path too.
		var afterStaleMark = await ReadRowAsync(delivered.Id).ConfigureAwait(false);
		afterStaleMark.Status.ShouldBe(
			StatusSent,
			"the backoff-scheduling overload must refuse a stale mark on exactly the same terms as its plain "
			+ "sibling. A guarantee that holds or not depending on which overload the processor reached is "
			+ "not a guarantee.");

		var redrained = (await currentTenure.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		redrained.ShouldNotContain(
			m => m.Id == delivered.Id,
			"a reverted delivered message is re-claimable, which is the second delivery this guard exists "
			+ "to prevent.");

		// LIVENESS -- a genuine backoff-scheduled failure still lands, and still carries a schedule.
		await currentTenure.MarkFailedWithBackoffAsync(
			genuinelyFailed.Id,
			"genuine failure",
			2,
			DateTimeOffset.UtcNow.AddSeconds(FloorSeconds + 30),
			ct).ConfigureAwait(false);

		var afterGenuine = await ReadRowAsync(genuinelyFailed.Id).ConfigureAwait(false);
		afterGenuine.Status.ShouldBe(
			StatusFailed,
			"the owning tenure must still be able to schedule a genuine retry through this overload.");
		afterGenuine.NextAttemptAt.ShouldNotBeNull(
			"a scheduled failure without a next-attempt time frees the lease with no lower bound on the next "
			+ "claim, which is the retry hot-loop the floor exists to stop.");
	}

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue")
		{
			Id = Guid.NewGuid().ToString(),
		};

	/// <summary>
	/// Blocks until the second tenure has actually taken the expired lease, so the arms that follow are
	/// reasoning about a genuinely superseded first tenure rather than about a claim that never happened.
	/// </summary>
	private async Task WaitForSecondTenureToReclaimAsync(SqlServerOutboxStore currentTenure, string messageId, CancellationToken ct)
	{
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => (await currentTenure.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == messageId),
			ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			$"the second tenure must be able to claim a lease that expired {LeaseTimeoutSeconds}s ago; "
			+ $"it asked for {ReclaimWindow.TotalSeconds:0} seconds. Without the re-claim there is no "
			+ "superseded tenure and the arms below would prove nothing.");

		(await ReadRowAsync(messageId).ConfigureAwait(false)).LeasedBy.ShouldStartWith(
			"proc-b:", Case.Sensitive,
			"the expired lease must have transferred, not merely have been read past.");
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
			Processing =
			{
				CommandTimeoutSeconds = 30,
				FailureBackoffFloorSeconds = FloorSeconds,
				LeaseTimeoutSeconds = LeaseTimeoutSeconds,
			},
		});

		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task<(string? LeasedBy, int Status, int RetryCount, DateTimeOffset? NextAttemptAt)> ReadRowAsync(string id)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT LeasedBy, Status, RetryCount, NextAttemptAt FROM [dbo].[OutboxMessages] WHERE Id = @id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@id", id));

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue($"row '{id}' must exist.");

		var leasedBy = await reader.IsDBNullAsync(0).ConfigureAwait(false) ? null : reader.GetString(0);
		var status = reader.GetInt32(1);
		var retryCount = reader.GetInt32(2);
		var nextAttemptAt = await reader.IsDBNullAsync(3).ConfigureAwait(false) ? (DateTimeOffset?)null : reader.GetDateTimeOffset(3);

		return (leasedBy, status, retryCount, nextAttemptAt);
	}
}
