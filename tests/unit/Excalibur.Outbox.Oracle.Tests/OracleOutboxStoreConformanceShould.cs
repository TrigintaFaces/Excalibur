// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="OracleOutboxStore"/> using the Outbox Conformance
/// Test Kit against a live Oracle (<c>gvenzl/oracle-free</c>) container.
/// </summary>
/// <remarks>
/// Verifies the Oracle implementation satisfies the <see cref="IOutboxStore"/> (and admin/dead-letter/
/// backoff/transactional) contract against real infrastructure. Never skipped: when Docker is unavailable
/// the fixture fails fast, so a missing container surfaces as a failure rather than a silent pass. The
/// store is constructed via its consumer-default surface — an <see cref="IDb"/> over a fresh Oracle
/// connection per access plus an <c>IOptions&lt;OracleOutboxStoreOptions&gt;</c> bound to the fixture's
/// connection string. Exercises the emitted behavior — the two-step reserve/claim, dead-letter move,
/// duplicate-insert dedup (ORA-00001), and the empty-field round-trip (A0 ruling #5) — not merely that a
/// value was written. The fixture owns the schema because the store does not self-create its tables.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<OracleOutboxStoreContainerFixture>
{
	private readonly OracleOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleOutboxStoreConformanceShould(OracleOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Oracle connection per access
		// (a fresh connection is required for the concurrent-staging conformance cases, which drive
		// multiple operations through IDb.Connection in parallel).
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

	/// <inheritdoc/>
	protected override async Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra re-claim-floor conformance is never skipped.");
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
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		// Reserve under a dispatcher id distinct from OracleOutboxStore's static per-process id, so the row's owner
		// differs from the caller of IOutboxStore.MarkFailedAsync — the only way to exercise the R2 guard.
		var reserved = await ((OracleOutboxStore)store)
			.ReserveOutboxMessagesAsync("conformance-foreign-leader", 50, CancellationToken.None).ConfigureAwait(false);
		return reserved.Any(m => m.MessageId == messageId);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Proves the Oracle reserve honors TRUE <c>FOR UPDATE SKIP LOCKED</c> (SA ruling on bead 9wka92):
	/// two dispatchers reserving concurrently claim DISJOINT batches and neither BLOCKS to timeout. A
	/// non-skip-locked (lock-wait) reserve would serialize/deadlock the two sessions and time out.
    /// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Reserve_TwoConcurrentDispatchers_ClaimDisjointBatchesWithoutBlocking()
	{
		// Arrange — 10 unsent messages; two dispatchers each try to reserve 5, concurrently, on their
		// own Oracle sessions (a second store => a second connection; skip-locked is only observable
		// across distinct sessions).
		var storeA = (OracleOutboxStore)Store;
		var storeB = (OracleOutboxStore)await CreateStoreAsync().ConfigureAwait(false);

		var stagedIds = new List<string>();
		for (var i = 0; i < 10; i++)
		{
			var message = CreateTestMessage();
			stagedIds.Add(message.Id);
			await storeA.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act — both reserves launched together; a lock-wait impl would block one until the other's txn
		// releases, blowing the timeout. Skip-locked lets both proceed immediately over disjoint rows.
		var reserveA = storeA.ReserveOutboxMessagesAsync("dispatcher-A", 5, CancellationToken.None);
		var reserveB = storeB.ReserveOutboxMessagesAsync("dispatcher-B", 5, CancellationToken.None);

		var results = await Task.WhenAll(reserveA, reserveB)
			.WaitAsync(TimeSpan.FromSeconds(30))
			.ConfigureAwait(false);

		// Assert — disjoint, each <= batchSize, union has no duplicate, all belong to the staged set.
		var idsA = results[0].Select(m => m.MessageId).ToList();
		var idsB = results[1].Select(m => m.MessageId).ToList();

		// NON-VACUITY (SA REVIEW_ARCH B2): a positive LOWER bound. The previous assertions (<=5, unique,
		// disjoint, subset) are ALL satisfied by EMPTY result sets, so a reserve that returns ZERO rows
		// (the B1 select-back positional-binding stall) passed GREEN. With 10 staged messages and two
		// dispatchers each claiming up to 5 over FOR UPDATE SKIP LOCKED, the two together MUST claim the
		// entire claimable set (10), and each MUST claim at least one. This is RED on the zero-row impl.
		idsA.Count.ShouldBeGreaterThan(0, "dispatcher-A must actually claim rows, not an empty select-back");
		idsB.Count.ShouldBeGreaterThan(0, "dispatcher-B must actually claim rows, not an empty select-back");
		idsA.Count.ShouldBeLessThanOrEqualTo(5);
		idsB.Count.ShouldBeLessThanOrEqualTo(5);
		idsA.ShouldBeUnique();
		idsB.ShouldBeUnique();
		idsA.Intersect(idsB).ShouldBeEmpty("concurrent dispatchers must claim disjoint rows (skip-locked)");

		var union = idsA.Concat(idsB).ToList();
		union.Count.ShouldBe(union.Distinct().Count());
		union.ShouldAllBe(id => stagedIds.Contains(id));
		// The claimable set is fully drained across the two dispatchers — the load-bearing lower bound.
		union.Count.ShouldBe(10, "both dispatchers together must claim every staged message, not zero rows");
	}

	/// <summary>
	/// u4x8sb scoped lock: the Oracle-local fix adds <c>correlation_id</c>/<c>causation_id</c> columns and
	/// positional binds so a staged message's CorrelationId and CausationId survive a reload from real Oracle.
	/// </summary>
	/// <remarks>
	/// Deliberately scoped to the two Oracle-local fields. Priority and the four multi-transport fields
	/// (PartitionKey/GroupKey/TargetTransports/IsMultiTransport) are a cross-provider shared-seam carry tracked
	/// separately (y5tn3e) and are intentionally NOT asserted here — the broad shared-kit
	/// <c>StageMessage_RoundTripsEveryConsumerSuppliedField</c> covers those and stays RED until y5tn3e.
	/// </remarks>
	[Fact]
	public async Task StageMessage_RoundTripsCorrelationAndCausationId_OnRealOracleReload()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await CleanupAsync().ConfigureAwait(false);

		var message = new OutboundMessage("Test.OrderPlaced", "order-data"u8.ToArray(), "orders-queue")
		{
			CorrelationId = "corr-oracle-1",
			CausationId = "cause-oracle-2",
		};

		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var retrieved = unsent.FirstOrDefault(m => m.Id == message.Id);

		_ = retrieved.ShouldNotBeNull("the staged message must reload from real Oracle");
		retrieved.CorrelationId.ShouldBe("corr-oracle-1", "correlation_id must round-trip (Oracle-local fix)");
		retrieved.CausationId.ShouldBe("cause-oracle-2", "causation_id must round-trip (Oracle-local fix)");
	}
}
