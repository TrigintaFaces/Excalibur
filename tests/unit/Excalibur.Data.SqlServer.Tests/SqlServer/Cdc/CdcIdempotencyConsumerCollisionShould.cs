// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the requirement that two independent consumers of the SAME source table each get their own
/// already-processed set, so one consumer's progress can never suppress another's delivery.
/// </summary>
/// <remarks>
/// <para>
/// EXPECTED RED UNTIL THE CONTRACT CARRIES A CONSUMER DISCRIMINATOR. This is a regression lock written
/// ahead of the fix, deliberately, because the defect it describes is silent data loss and the repair has
/// two candidate routes that must both satisfy it.
/// </para>
/// <para>
/// The root cause is not an implementation bug, it is the shape of the contract:
/// <c>ICdcIdempotencyFilter.IsProcessedAsync(tableName, lsn, seqVal, ct)</c> has no consumer or processor
/// parameter, so the dedupe key can only ever be table plus position. No implementation can distinguish
/// two consumers, because the interface never tells it which one is asking. The words "consumer" and
/// "processor" appear in that file only in prose.
/// </para>
/// <para>
/// The consequence fails in the losing direction. A false duplicate DROPS a change permanently; a missed
/// duplicate merely reprocesses one, which an idempotent handler absorbs. Whichever consumer calls
/// <c>MarkProcessedAsync</c> first causes every other consumer of that table to skip the change without
/// ever seeing it, and nothing reports an error.
/// </para>
/// <para>
/// The in-memory filter stands in for the shared store here. Its dictionary models the SQL Server dedupe
/// table exactly in the respect under test -- one set of keys, shared by every caller holding it -- and it
/// makes the property provable in milliseconds rather than behind a container. The SQL Server
/// implementation shares the identical key shape, so a fix that satisfies this lock must change the
/// contract both of them implement.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyConsumerCollisionShould : UnitTestBase
{
	// Same source database, different consumers: the axis this class is about.
	private static readonly CdcConsumerIdentity ConsumerA = new("orders-projector", "shared-db");
	private static readonly CdcConsumerIdentity ConsumerB = new("audit-forwarder", "shared-db");
	private const string SharedTable = "dbo.Orders";

	private static readonly byte[] Lsn = [0x00, 0x00, 0x00, 0x01];
	private static readonly byte[] SeqVal = [0x00, 0x00, 0x00, 0x01];

	/// <summary>
	/// SAFETY: one consumer marking a change processed must not suppress that change for another consumer.
	/// </summary>
	[Fact]
	public async Task NotSuppressAChangeForASecondConsumerOfTheSameTable()
	{
		// One shared dedupe store, two logical consumers of the same source table -- a fan-out topology the
		// framework does not forbid.
		//
		// This property was INEXPRESSIBLE until the contract carried a consumer discriminator: both calls
		// below were identical, so no implementation could distinguish B from A however it was written. The
		// arm was landed red ahead of the repair for exactly that reason. It is now written the way it always
		// wanted to be -- two identities against one store.
		var sharedStore = new InMemoryCdcIdempotencyFilter(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

		// Consumer A handles the change and records it.
		await sharedStore.MarkProcessedAsync(SharedTable, Lsn, SeqVal, ConsumerA, CancellationToken.None)
			.ConfigureAwait(false);

		// Consumer B has never seen this change. It must still be delivered.
		var alreadySeenByB = await sharedStore
			.IsProcessedAsync(SharedTable, Lsn, SeqVal, ConsumerB, CancellationToken.None)
			.ConfigureAwait(false);

		alreadySeenByB.ShouldBeFalse(
			"consumer B has never processed this change, so it must still be delivered. If A's progress marks "
			+ "it already-done for B, B skips a change it never saw and nothing reports an error -- silent "
			+ "data loss, and it fails in the losing direction: a false duplicate drops data permanently "
			+ "while a missed duplicate only reprocesses, which an idempotent handler absorbs.");
	}

	/// <summary>
	/// LIVENESS: the same consumer must still be deduplicated against its own progress.
	/// </summary>
	/// <remarks>
	/// Not optional, and it is the arm that stops the obvious wrong fix. A filter that answered "not
	/// processed" to everyone would satisfy the safety arm above perfectly while deduplicating nothing at
	/// all -- turning at-most-once into at-least-once for every consumer. Whatever discriminator the repair
	/// introduces, re-asking with the SAME identity must still report the change as seen.
	/// </remarks>
	[Fact]
	public async Task StillDeduplicateAChangeForTheConsumerThatProcessedIt()
	{
		var store = new InMemoryCdcIdempotencyFilter(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

		await store.MarkProcessedAsync(SharedTable, Lsn, SeqVal, ConsumerA, CancellationToken.None)
			.ConfigureAwait(false);

		// The SAME identity re-asks -- this is the arm that stops "answer not-processed to everyone".
		var seenAgain = await store.IsProcessedAsync(SharedTable, Lsn, SeqVal, ConsumerA, CancellationToken.None)
			.ConfigureAwait(false);

		seenAgain.ShouldBeTrue(
			"the consumer that processed this change must still see it as processed -- a filter that "
			+ "reported everything unseen would pass the isolation arm while deduplicating nothing");
	}

	/// <summary>
	/// A different position on the same table is a different change, for the same consumer.
	/// </summary>
	/// <remarks>
	/// Guards the opposite over-correction: a repair that keyed on the consumer and DROPPED the position
	/// would suppress every subsequent change for a consumer that had processed any change at all.
	/// </remarks>
	[Fact]
	public async Task NotSuppressADifferentPositionOnTheSameTable()
	{
		var store = new InMemoryCdcIdempotencyFilter(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

		await store.MarkProcessedAsync(SharedTable, Lsn, SeqVal, ConsumerA, CancellationToken.None)
			.ConfigureAwait(false);

		byte[] laterSeqVal = [0x00, 0x00, 0x00, 0x02];

		var laterChangeSeen = await store
			.IsProcessedAsync(SharedTable, Lsn, laterSeqVal, ConsumerA, CancellationToken.None)
			.ConfigureAwait(false);

		laterChangeSeen.ShouldBeFalse(
			"a later position on the same table is a different change and must not be suppressed by the "
			+ "earlier one");
	}

	/// <summary>
	/// SAFETY. Two configured sources that SHARE a connection identifier and differ only in database name
	/// must not share a dedupe namespace.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The axis the key was missing.</b> The checkpoint store matches on
	/// <c>(DatabaseConnectionIdentifier, DatabaseName, TableName)</c> — three axes — while the dedupe key
	/// carried the connection identifier and the table and omitted the database. The dedupe namespace was
	/// therefore strictly COARSER than the namespace of the position it guards, and a coarser dedupe
	/// namespace suppresses rather than duplicates: the first source to reach a given position marks it
	/// done for every other source sharing the coarser key.
	/// </para>
	/// <para>
	/// Nothing elsewhere prevents the configuration. The job options validator checks only that the
	/// collection is non-empty; the fan-out de-duplicates configurations by reference over a class with no
	/// equality members, so two value-identical entries both run; and the dedupe table's own database is
	/// chosen independently of any source, so one table can be shared by unrelated ones. The key therefore
	/// has to carry the axis outright rather than rely on identifiers happening to differ.
	/// </para>
	/// <para>
	/// The named red input: drop <c>DatabaseName</c> from the key composition and this arm fails, because
	/// the second source reads the first source's marker as its own.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NotSuppressAChangeForADifferentSourceDatabaseUnderOneConnectionIdentifier()
	{
		var sharedStore = new InMemoryCdcIdempotencyFilter(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

		// One connection identifier, two source databases -- a shape the configuration permits and
		// nothing rejects.
		var onLegacy = new CdcConsumerIdentity("shared-identifier", "Legacy");
		var onBilling = new CdcConsumerIdentity("shared-identifier", "Billing");

		await sharedStore.MarkProcessedAsync(SharedTable, Lsn, SeqVal, onLegacy, CancellationToken.None)
			.ConfigureAwait(false);

		var alreadySeenOnBilling = await sharedStore
			.IsProcessedAsync(SharedTable, Lsn, SeqVal, onBilling, CancellationToken.None)
			.ConfigureAwait(false);

		alreadySeenOnBilling.ShouldBeFalse(
			"the Billing source has never processed this change. SQL Server log sequence numbers are "
			+ "per-database, so an identical position in a different database is a DIFFERENT change -- "
			+ "treating it as processed skips it permanently and reports nothing.");
	}

	/// <summary>
	/// LIVENESS. The same source must still be deduplicated against its own progress, so the arm above
	/// cannot be satisfied by a filter that simply stopped deduplicating.
	/// </summary>
	[Fact]
	public async Task StillSuppressARepeatFromTheSameSourceDatabase()
	{
		var sharedStore = new InMemoryCdcIdempotencyFilter(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);
		var onLegacy = new CdcConsumerIdentity("shared-identifier", "Legacy");

		await sharedStore.MarkProcessedAsync(SharedTable, Lsn, SeqVal, onLegacy, CancellationToken.None)
			.ConfigureAwait(false);

		var seenAgain = await sharedStore
			.IsProcessedAsync(SharedTable, Lsn, SeqVal, onLegacy, CancellationToken.None)
			.ConfigureAwait(false);

		seenAgain.ShouldBeTrue("adding an axis must not stop the filter deduplicating what it should");
	}
}
