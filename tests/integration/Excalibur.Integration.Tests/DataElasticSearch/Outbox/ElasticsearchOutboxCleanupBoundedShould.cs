// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Outbox;

// Author≠impl regression lock for bd-v8k7jo (MS-A1, DATA LOSS HEADLINE):
// ElasticsearchOutboxStore.CleanupSentMessagesAsync previously issued a MatchAll DeleteByQuery against a
// HARDCODED "excalibur-outbox" index literal, deleting the ENTIRE live outbox (Staged + recent Sent
// included) regardless of olderThan. The fix is a bounded BoolQuery (status==Sent AND sentAt < olderThan)
// on the CONFIGURED IndexName. Run against a CUSTOM IndexName so the lock is non-vacuous against BOTH
// extinguished defects: the original MatchAll (would delete the Staged + recent-Sent docs too) AND the
// hardcoded-index bug (a custom index would have had NOTHING deleted). Only Sent docs strictly older than
// the cutoff are deleted; a Sent doc exactly at the cutoff is retained (strictly older-than). Mirrors the
// sibling inbox lock ElasticsearchInboxCleanupCutoffShould. Serial -m:1 (ES TestContainers).
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Elasticsearch")]
public sealed class ElasticsearchOutboxCleanupBoundedShould : ElasticsearchIntegrationTestBase
{
	private const int StagedStatus = (int)OutboxStatus.Staged; // 0
	private const int SentStatus = (int)OutboxStatus.Sent;     // 2

	[Fact]
	public async Task DeleteOnlySentDocsStrictlyOlderThanTheCutoff_OnACustomIndex()
	{
		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-30);
		var customIndex = $"{TestIndexPrefix}outbox"; // custom IndexName — exercises the index-target fix

		var documents = new[]
		{
			// status=Sent, strictly older than cutoff -> DELETED
			SentDoc("sent-old-1", now.AddDays(-100)),
			SentDoc("sent-old-2", now.AddDays(-60)),
			// status=Sent exactly at cutoff -> RETAINED (strictly older-than)
			SentDoc("sent-boundary", cutoff),
			// status=Sent but recent -> RETAINED (newer than cutoff)
			SentDoc("sent-recent", now.AddDays(-1)),
			// status=Staged (never sent) -> RETAINED regardless of age (MatchAll would have nuked it)
			StagedDoc("staged-old", now.AddDays(-100)),
		};

		await IndexDocumentsAsync(customIndex, documents).ConfigureAwait(false);
		(await SearchDocumentsAsync<OutboxTestDoc>(customIndex).ConfigureAwait(false)).Count.ShouldBe(5);

		var store = new ElasticsearchOutboxStore(
			Client,
			Options.Create(new ElasticsearchOutboxOptions { IndexName = customIndex }),
			LoggerFactory.CreateLogger<ElasticsearchOutboxStore>());

		// Act
		var deleted = await store.CleanupSentMessagesAsync(cutoff, batchSize: 100, CancellationToken.None);
		_ = await Client.Indices.RefreshAsync(customIndex).ConfigureAwait(false);

		// Assert — only the two strictly-older Sent docs were deleted; everything else remains.
		deleted.ShouldBe(2, "only Sent docs strictly older than the cutoff must be deleted");

		var remainingIds = (await SearchDocumentsAsync<OutboxTestDoc>(customIndex).ConfigureAwait(false))
			.Select(d => d.Id)
			.ToHashSet(StringComparer.Ordinal);

		remainingIds.ShouldNotContain("sent-old-1");
		remainingIds.ShouldNotContain("sent-old-2");
		remainingIds.ShouldContain("sent-boundary", "a Sent doc exactly at the cutoff is retained (strictly older-than)");
		remainingIds.ShouldContain("sent-recent", "a recent Sent doc must not be deleted");
		remainingIds.ShouldContain("staged-old", "a Staged doc must never be deleted by cleanup (the MatchAll data-loss bug)");
	}

	private static OutboxTestDoc SentDoc(string id, DateTimeOffset sentAt) => new()
	{
		Id = id,
		MessageType = "TestMessage",
		Destination = "test-destination",
		Status = SentStatus,
		CreatedAt = sentAt.AddDays(-1),
		SentAt = sentAt,
	};

	private static OutboxTestDoc StagedDoc(string id, DateTimeOffset createdAt) => new()
	{
		Id = id,
		MessageType = "TestMessage",
		Destination = "test-destination",
		Status = StagedStatus,
		CreatedAt = createdAt,
		SentAt = null,
	};
}

// Field-name-matched mirror of the (internal) ElasticsearchOutboxDocument fields the cleanup query reads.
// The Elasticsearch client infers field names per property name consistently across types, so Status/SentAt
// here map to the same fields the store's bounded BoolQuery (status + sentAt range) targets.
internal sealed class OutboxTestDoc
{
	public string Id { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public string Destination { get; set; } = string.Empty;
	public int Status { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? SentAt { get; set; }
}
