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
/// Real-infrastructure locks on the SQL Server outbox BATCH failure transition, which shipped without the
/// ownership guard, the not-already-sent guard and the visibility floor its single-message sibling enforced.
/// </summary>
/// <remarks>
/// <para>
/// The consequence of each omission is different, which is why each gets its own arm: without ownership a
/// dispatcher marks failed a message a peer holds; without the sent guard a DELIVERED message is reverted to
/// Failed and sent a second time, a duplicate produced by our own bookkeeping rather than by any transport;
/// and without the floor the lease is freed with no lower bound on the next claim, which is the retry
/// hot-loop the floor exists to prevent.
/// </para>
/// <para>
/// Every arm is paired safety and liveness, because each of these guards is trivially satisfied by a batch
/// mark that does nothing at all. Run against a live SQL Server container, never skipped, because all three
/// are emitted-database behaviour that no assertion over a command string can prove.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxMarkBatchFailedGuardsShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const int StatusSent = 2;
	private const int StatusFailed = 3;

	/// <summary>Short enough to wait out in the liveness arm, long enough to observe in the safety arm.</summary>
	private const int FloorSeconds = 4;

	/// <summary>
	/// How long the liveness half keeps asking for the message before it calls it stranded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Generous on purpose, and not a tolerance on the store's behaviour. The floor is written and judged on
	/// the SERVER's clock (<c>SYSUTCDATETIME()</c>); any wait this test performs is measured on the TEST
	/// HOST's. Those are two different clocks, and on a loaded machine a containerised database's clock does
	/// not advance in step with the host's -- it stalls and then catches up. Measured on the machine this
	/// suite runs on, sampling a container's clock across fifty host-side 2.5 second waits: six of the fifty
	/// advanced the container clock by only 345 to 541 ms, a shortfall well beyond the two seconds of slack
	/// a fixed sleep of <c>FloorSeconds + 2</c> leaves.
	/// </para>
	/// <para>
	/// A single sample after a fixed sleep therefore asserts that the SERVER has seen the floor elapse when
	/// only the HOST has, and reports a store that is behaving correctly -- withholding a retry because its
	/// own clock says the floor has not passed -- as a message returned to nobody. Withholding is the safe
	/// direction, so the store is right and the sample is wrong. Polling asserts the same property without
	/// assuming the two clocks agree; a batch mark that genuinely strands the message still fails here,
	/// because a clock that stalls always catches up.
	/// </para>
	/// </remarks>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(30);

	/// <summary>How often the liveness half re-asks the store while the window is open.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(250);

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxMarkBatchFailedGuardsShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY: a batch mark from a processor that does not hold the lease changes nothing.
	/// LIVENESS: the processor that does hold it still marks its own messages failed.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task NotMarkFailed_AMessageAnotherProcessorHolds_WhileStillMarkingItsOwn()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var proc1 = CreateStore("proc-1");
		var proc2 = CreateStore("proc-2");

		var held = NewMessage();
		var owned = NewMessage();
		await proc1.StageMessageAsync(held, ct).ConfigureAwait(false);
		await proc1.StageMessageAsync(owned, ct).ConfigureAwait(false);

		// proc-1 claims both, so both rows are leased by proc-1.
		_ = (await proc1.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		(await ReadRowAsync(held.Id).ConfigureAwait(false)).LeasedBy.ShouldBe("proc-1");

		// proc-2 attempts to settle a batch containing a message it does not hold.
		await proc2.MarkBatchFailedAsync([held.Id], "hijack", 9, ct).ConfigureAwait(false);

		// SAFETY -- the foreign batch mark matched no rows.
		var afterForeign = await ReadRowAsync(held.Id).ConfigureAwait(false);
		afterForeign.LeasedBy.ShouldBe(
			"proc-1",
			"a batch mark from a processor that does not hold the lease must not clear a lease it does not " +
			"own. The single-message path guards on ownership; the batch path reaching the same contract " +
			"without it meant the guarantee depended on which overload the processor happened to call.");
		afterForeign.Status.ShouldNotBe(
			StatusFailed,
			"a foreign processor must not move an in-flight message it does not hold to Failed.");

		// LIVENESS -- the guard did not simply disable the batch path for the legitimate owner.
		await proc1.MarkBatchFailedAsync([owned.Id], "genuine failure", 1, ct).ConfigureAwait(false);

		var afterOwner = await ReadRowAsync(owned.Id).ConfigureAwait(false);
		afterOwner.Status.ShouldBe(
			StatusFailed,
			"the lease owner must still be able to settle its own batch. A guard that refuses everyone " +
			"satisfies the safety assertion above and delivers nothing.");
		afterOwner.LeasedBy.ShouldBeNull("settling the batch releases the lease the owner held.");
	}

	/// <summary>
	/// SAFETY: a batch mark cannot revert a DELIVERED message to Failed.
	/// LIVENESS: a genuinely failed message in the SAME batch is still marked.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The ownership guard alone does not close this. The failure transition itself releases the lease, so
	/// once any failure has occurred the row reads as unleased to every processor thereafter, and a late or
	/// duplicated batch mark matches a message that has since been delivered. Reverting a delivered message
	/// to Failed puts it back in the claim pool, so it is sent twice.
	/// </remarks>
	[Fact]
	public async Task NotRevert_ADeliveredMessageToFailed_WhileStillFailingItsUndeliveredSibling()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var store = CreateStore("proc-1");

		var delivered = NewMessage();
		var undelivered = NewMessage();
		await store.StageMessageAsync(delivered, ct).ConfigureAwait(false);
		await store.StageMessageAsync(undelivered, ct).ConfigureAwait(false);

		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// One of the two is delivered. This is the terminal state no mark may reverse.
		await store.MarkSentAsync(delivered.Id, ct).ConfigureAwait(false);
		(await ReadRowAsync(delivered.Id).ConfigureAwait(false)).Status.ShouldBe(StatusSent);

		// A late batch failure report naming BOTH -- the shape a retried or duplicated settle produces.
		await store.MarkBatchFailedAsync([delivered.Id, undelivered.Id], "late report", 2, ct).ConfigureAwait(false);

		// SAFETY -- the delivered message is untouched.
		var afterDelivered = await ReadRowAsync(delivered.Id).ConfigureAwait(false);
		afterDelivered.Status.ShouldBe(
			StatusSent,
			"a delivered message must never be moved back to Failed. Sent is terminal: reverting it returns " +
			"the message to the claim pool and it is delivered a second time, a duplicate caused by our own " +
			"bookkeeping rather than by any transport.");

		// LIVENESS -- the guard is scoped to delivered messages, not to the whole batch.
		var afterUndelivered = await ReadRowAsync(undelivered.Id).ConfigureAwait(false);
		afterUndelivered.Status.ShouldBe(
			StatusFailed,
			"the undelivered sibling in the same batch must still be marked failed. A guard that discarded " +
			"the whole batch when one member is delivered would strand every genuine failure alongside it.");
	}

	/// <summary>
	/// SAFETY: the lease a batch failure frees carries a floor, so the message is not immediately re-claimable.
	/// LIVENESS: once the floor elapses the message IS re-claimed, so the floor defers and never strands.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task WithholdAFailedBatchForTheFloor_AndReturnItToTheClaimPoolOnceTheFloorElapses()
	{
		await EnsureReadyAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var store = CreateStore("proc-1");

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		await store.MarkBatchFailedAsync([message.Id], "boom", 1, ct).ConfigureAwait(false);

		// The floor is recorded on the row, computed on the server clock.
		var afterFail = await ReadRowAsync(message.Id).ConfigureAwait(false);
		afterFail.NextAttemptAt.ShouldNotBeNull(
			"a batch failure must write a next-attempt floor. Freeing the lease without one leaves the " +
			"message claimable on the very next poll, which is the retry hot-loop the floor exists to stop.");

		// SAFETY -- the drain does not re-claim it while the floor stands.
		var immediately = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		immediately.ShouldNotContain(
			m => m.Id == message.Id,
			"a message whose floor has not elapsed must not be re-claimed. Without the floor this same call " +
			"returns it at once and the drain spins on it.");

		// LIVENESS -- the floor defers the retry; it does not cancel it. Asked repeatedly rather than sampled
		// once: see ReclaimWindow for why one sample after a fixed wait cannot tell a floor that strands the
		// message from a server whose own clock has not yet reached it.
		await Task.Delay(TimeSpan.FromSeconds(FloorSeconds + 2), ct).ConfigureAwait(false);

		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == message.Id),
			ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"once the floor has elapsed the message must be re-claimable. A floor that withheld it forever " +
			"would satisfy the safety assertion above by dropping the message, breaking at-least-once. This " +
			$"arm kept asking for {ReclaimWindow.TotalSeconds:0} seconds, which is far longer than any clock " +
			"stall observed on this host, so the message was never handed back.");
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
			Processing =
			{
				CommandTimeoutSeconds = 30,
				FailureBackoffFloorSeconds = FloorSeconds,
			},
		});

		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task<(DateTimeOffset? LeasedAt, string? LeasedBy, int Status, DateTimeOffset? NextAttemptAt)> ReadRowAsync(string id)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = new SqlCommand(
			"SELECT LeasedAt, LeasedBy, Status, NextAttemptAt FROM [dbo].[OutboxMessages] WHERE Id = @id",
			connection);
		_ = command.Parameters.Add(new SqlParameter("@id", id));

		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue($"row '{id}' must exist.");

		var leasedAt = await reader.IsDBNullAsync(0).ConfigureAwait(false) ? (DateTimeOffset?)null : reader.GetDateTimeOffset(0);
		var leasedBy = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? null : reader.GetString(1);
		var status = reader.GetInt32(2);
		var nextAttemptAt = await reader.IsDBNullAsync(3).ConfigureAwait(false) ? (DateTimeOffset?)null : reader.GetDateTimeOffset(3);

		return (leasedAt, leasedBy, status, nextAttemptAt);
	}
}
