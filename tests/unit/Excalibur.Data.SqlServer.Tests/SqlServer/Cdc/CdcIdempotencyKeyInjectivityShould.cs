// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the requirement that the CDC idempotency key is INJECTIVE across the consumer/table boundary.
/// </summary>
/// <remarks>
/// <para>
/// The key was joined as <c>{consumerId}:{tableName}:{hexLsn}:{hexSeqVal}</c>. The position terms are hex
/// and cannot contain a colon, but the consumer id and the table name are both unvalidated caller
/// strings, so consumer "a:b" on table "c" and consumer "a" on table "b:c" rendered the SAME key and
/// shared one already-processed set.
/// </para>
/// <para>
/// This is a deduplication filter, so the collision is silent. One consumer's processed marker suppresses
/// the other consumer's genuinely new change, which is skipped and never delivered. It fails in the
/// losing direction: a false duplicate drops a change permanently, while a missed duplicate only
/// reprocesses one, which an idempotent handler absorbs.
/// </para>
/// <para>
/// The key is in-process only and never persisted, so there is no stored state keyed by the old shape and
/// no upgrade consequence.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcIdempotencyKeyInjectivityShould : UnitTestBase
{
	private static readonly byte[] Lsn = [0x00, 0x00, 0x00, 0x01];
	private static readonly byte[] SeqVal = [0x00, 0x00, 0x00, 0x01];

	private static InMemoryCdcIdempotencyFilter CreateFilter() =>
		new(NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

	/// <summary>
	/// SAFETY: the colliding pair -- a colon shifted across the consumer/table boundary.
	/// </summary>
	[Fact]
	public async Task NotSuppressAChangeForATupleThatCollidedUnderTheBareColonJoin()
	{
		var sharedStore = CreateFilter();

		// Consumer "a:b" processes a change on table "c".
		// Under the bare join this recorded the key "a:b:c:00000001:00000001".
		await sharedStore.MarkProcessedAsync("c", Lsn, SeqVal, "a:b", CancellationToken.None)
			.ConfigureAwait(false);

		// A DIFFERENT consumer, "a", asks about a DIFFERENT table, "b:c".
		// Under the bare join this composed the identical key and read as already-processed.
		var alreadySeen = await sharedStore
			.IsProcessedAsync("b:c", Lsn, SeqVal, "a", CancellationToken.None)
			.ConfigureAwait(false);

		alreadySeen.ShouldBeFalse(
			"consumer 'a' on table 'b:c' has never processed this change. If the colon shifting across the "
			+ "consumer/table boundary makes it share a key with consumer 'a:b' on table 'c', it skips a "
			+ "change it never saw and nothing reports an error -- silent, permanent data loss.");
	}

	/// <summary>
	/// LIVENESS: the same consumer on the same table must still be deduplicated against its own progress.
	/// </summary>
	/// <remarks>
	/// Required, and it is the arm that stops the trivially wrong fix. A filter that answered "not
	/// processed" to every caller would satisfy the safety arm perfectly while deduplicating nothing --
	/// turning at-most-once into at-least-once for every consumer.
	/// </remarks>
	[Fact]
	public async Task StillDeduplicateAChangeForTheSameConsumerAndTable()
	{
		var store = CreateFilter();

		await store.MarkProcessedAsync("dbo.Orders", Lsn, SeqVal, "orders-projector", CancellationToken.None)
			.ConfigureAwait(false);

		var seenAgain = await store
			.IsProcessedAsync("dbo.Orders", Lsn, SeqVal, "orders-projector", CancellationToken.None)
			.ConfigureAwait(false);

		seenAgain.ShouldBeTrue(
			"the consumer that processed this change must still see it as processed -- a filter that "
			+ "reported everything unseen would pass the injectivity arm while deduplicating nothing");
	}

	/// <summary>
	/// LIVENESS: a consumer whose id legitimately contains a colon must still be deduplicated.
	/// </summary>
	/// <remarks>
	/// The over-correction guard. A "fix" that rejected or stripped colons would make the safety arm pass
	/// while breaking dedup for every consumer using a colon-bearing identifier -- a URN, or a
	/// "topic:partition" style name.
	/// </remarks>
	[Fact]
	public async Task StillDeduplicateForAConsumerIdContainingAColon()
	{
		var store = CreateFilter();

		await store.MarkProcessedAsync("c", Lsn, SeqVal, "a:b", CancellationToken.None).ConfigureAwait(false);

		var seenAgain = await store.IsProcessedAsync("c", Lsn, SeqVal, "a:b", CancellationToken.None)
			.ConfigureAwait(false);

		seenAgain.ShouldBeTrue(
			"a colon in a consumer id is legal caller data. Making the key injective must not cost dedup "
			+ "for the consumers whose ids contain one");
	}

	/// <summary>
	/// A different position on the same table for the same consumer is still a different change.
	/// </summary>
	[Fact]
	public async Task NotSuppressADifferentPositionForTheSameConsumer()
	{
		var store = CreateFilter();

		await store.MarkProcessedAsync("c", Lsn, SeqVal, "a:b", CancellationToken.None).ConfigureAwait(false);

		byte[] laterSeqVal = [0x00, 0x00, 0x00, 0x02];

		var laterSeen = await store.IsProcessedAsync("c", Lsn, laterSeqVal, "a:b", CancellationToken.None)
			.ConfigureAwait(false);

		laterSeen.ShouldBeFalse(
			"a later position is a different change and must not be suppressed by the earlier one -- this "
			+ "guards a repair that keyed on identity and dropped the position");
	}
}
