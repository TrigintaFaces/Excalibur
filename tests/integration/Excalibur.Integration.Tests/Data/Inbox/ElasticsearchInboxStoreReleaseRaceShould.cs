// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure lock for <see cref="ElasticsearchInboxStore"/>'s atomic delete-unless-Processed
/// <c>ReleaseAsync</c> (dpi8m4): a concurrent <c>MarkProcessedAsync</c> that finalizes the entry in the window
/// between Release's status-read and its delete must NOT cause Release to remove the now-Processed entry.
/// </summary>
/// <remarks>
/// The fix captures <c>_seq_no</c>/<c>_primary_term</c> on read and issues a conditional delete (<c>IfSeqNo</c>/
/// <c>IfPrimaryTerm</c>); a concurrent finalize bumps the version so the delete fails (409) → re-read → no-op on a
/// Processed entry. The deterministic race is driven by the test-only <c>ReleaseRaceHookForTests</c> seam, which fires
/// once in that exact window. <b>RED mutant:</b> revert to an unconditional <c>DeleteAsync</c> → the Processed entry
/// is deleted in the gap → <c>IsProcessedAsync</c> returns false. Never skipped.
/// </remarks>
[Collection(ElasticsearchInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Inbox")]
public sealed class ElasticsearchInboxStoreReleaseRaceShould : IClassFixture<ElasticsearchInboxStoreContainerFixture>
{
	private const string HandlerType = "TestHandler";
	private readonly ElasticsearchInboxStoreContainerFixture _fixture;

	public ElasticsearchInboxStoreReleaseRaceShould(ElasticsearchInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private ElasticsearchInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Elasticsearch container must be available - real-infra release-race lock is never skipped.");
		var options = Options.Create(new ElasticsearchInboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
		});
		return new ElasticsearchInboxStore(_fixture.Client, options, NullLogger<ElasticsearchInboxStore>.Instance);
	}

	[Fact]
	public async Task Preserve_a_concurrently_finalized_entry_in_the_release_race()
	{
		var store = CreateStore();
		var messageId = $"msg-release-race-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		// Claim the message (creates the non-terminal Processing entry).
		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		// In the window between Release's status-read and its conditional delete, finalize the entry once.
		store.ReleaseRaceHookForTests = async hookCt =>
		{
			store.ReleaseRaceHookForTests = null; // fire exactly once (don't re-enter on the retry)
			await store.MarkProcessedAsync(messageId, HandlerType, hookCt).ConfigureAwait(false);
		};

		await store.ReleaseAsync(messageId, HandlerType, ct);

		// The conditional delete must have failed against the now-Processed (version-bumped) entry → it survives.
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue(
			"a concurrent finalize in the release window must NOT let Release delete the now-Processed entry");
	}

	[Fact]
	public async Task Delete_a_non_finalized_entry_on_a_normal_release()
	{
		// Positive control: with no concurrent finalize, Release still removes a Processing entry (re-admits redelivery).
		var store = CreateStore();
		var messageId = $"msg-normal-release-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.ReleaseAsync(messageId, HandlerType, ct);

		// Entry gone → not processed, and re-claimable (redelivery admitted).
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeFalse();
		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue(
			"a released (non-finalized) entry must be re-admitted on redelivery");
	}
}
