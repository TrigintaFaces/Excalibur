// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.Options;

using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

namespace Excalibur.Integration.Tests.DataElasticSearch.Inbox;

// bd-6toaue (S841, ADR-336): ElasticsearchInboxStore.CleanupAllTenantsProcessedEntriesAsync issued a MatchAll DeleteByQuery, deleting
// EVERY inbox document regardless of age (FR-4 silent data-loss). The fix is a strict older-than DateRange on
// ReceivedAt against the configured IndexName. Independent engage-test (author≠impl), run against a CUSTOM
// IndexName so it is non-vacuous against BOTH defects extinguished in 6toaue: the original MatchAll (would
// delete the recent docs too) AND the index-target bug I flagged (hardcoded "excalibur-inbox" would delete
// NOTHING from a custom index). Only entries strictly older than the cutoff are deleted; an entry exactly at
// the cutoff is retained (EC-5). Serial -m:1 (ES TestContainers).
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "Elasticsearch")]
[Collection(nameof(ElasticsearchHostTests))]
public sealed class ElasticsearchInboxCleanupCutoffShould(ElasticsearchContainerFixture fixture)
	: ElasticsearchIntegrationTestBase(fixture)
{
	[Fact]
	public async Task DeleteOnlyEntriesStrictlyOlderThanTheCutoff_OnACustomIndex()
	{
		var now = DateTimeOffset.UtcNow;
		var cutoff = now.AddDays(-30);
		var customIndex = $"{TestIndexPrefix}inbox"; // custom IndexName — exercises the index-target fix

		var documents = new[]
		{
			NewDoc("old-1", now.AddDays(-100)),
			NewDoc("old-2", now.AddDays(-60)),
			NewDoc("boundary", cutoff), // exactly at the cutoff — retained (strictly older-than, EC-5)
			NewDoc("recent-1", now.AddDays(-10)),
			NewDoc("recent-2", now.AddDays(-1)),
		};

		await IndexDocumentsAsync(customIndex, documents).ConfigureAwait(false);
		(await SearchDocumentsAsync<InboxTestDoc>(customIndex).ConfigureAwait(false)).Count.ShouldBe(5);

		var store = new ElasticsearchInboxStore(
			Client,
			Options.Create(new ElasticsearchInboxOptions { IndexName = customIndex }),
			LoggerFactory.CreateLogger<ElasticsearchInboxStore>(),
			SingleTenantTestContext.Instance);

		// Act
		var deleted = await store.CleanupAllTenantsProcessedEntriesAsync(cutoff, CancellationToken.None);
		_ = await Client.Indices.RefreshAsync(customIndex).ConfigureAwait(false);

		// Assert — only the two strictly-older entries were deleted; the boundary + the two recent ones remain.
		deleted.ShouldBe(2, "only the entries strictly older than the cutoff must be deleted");

		var remainingIds = (await SearchDocumentsAsync<InboxTestDoc>(customIndex).ConfigureAwait(false))
			.Select(d => d.MessageId)
			.ToHashSet(StringComparer.Ordinal);

		remainingIds.ShouldNotContain("old-1");
		remainingIds.ShouldNotContain("old-2");
		remainingIds.ShouldContain("boundary", "an entry exactly at the cutoff is retained (strictly older-than)");
		remainingIds.ShouldContain("recent-1");
		remainingIds.ShouldContain("recent-2");
	}

	/// <summary>
	/// A PROCESSED inbox entry completed at <paramref name="processedAt"/> — the only shape retention
	/// considers. Status must be Processed and ProcessedAt must be set, or cleanup correctly ignores the
	/// document and this suite measures nothing.
	/// </summary>
	private static InboxTestDoc NewDoc(string messageId, DateTimeOffset processedAt) => new()
	{
		MessageId = messageId,
		HandlerType = "TestHandler",
		MessageType = "TestMessageType",
		// Arrival precedes completion; kept distinct from ProcessedAt so a regression that keys retention
		// on arrival again fails here instead of passing on coincidentally-equal timestamps.
		ReceivedAt = processedAt.AddMinutes(-5),
		ProcessedAt = processedAt,
		Status = (int)InboxStatus.Processed,
	};

}

// Field-name-matched mirror of the (internal) ElasticsearchInboxDocument fields the cleanup query reads.
// The Elasticsearch client infers field names per property name consistently across types, so these map
// to the same fields the store's query targets.
//
// ProcessedAt is part of that set: retention keys on WHEN THE ENTRY COMPLETED, not when it arrived.
// The mirror omitted it while cleanup still keyed on ReceivedAt; cleanup was since corrected to
// `Status = Processed AND ProcessedAt < cutoff` (the predicate the SQL providers use), because keying on
// arrival deleted long-running and recently-retried entries, and running without the status filter also
// deleted Pending entries — dropping the dedup record and letting a redelivered message be processed
// twice, which is the one thing an inbox exists to prevent.
internal sealed class InboxTestDoc
{
	public string MessageId { get; set; } = string.Empty;
	public string HandlerType { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public DateTimeOffset ReceivedAt { get; set; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int Status { get; set; }
}
