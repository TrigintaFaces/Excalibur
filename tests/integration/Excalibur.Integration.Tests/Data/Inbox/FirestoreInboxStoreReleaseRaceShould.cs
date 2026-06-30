// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure lock for <see cref="FirestoreInboxStore"/>'s atomic delete-unless-Processed
/// <c>ReleaseAsync</c> (dpi8m4): a concurrent <c>MarkProcessedAsync</c> finalizing the entry in the window between
/// Release's status-read and its delete must NOT cause Release to remove the now-Processed entry.
/// </summary>
/// <remarks>
/// The fix captures <c>snapshot.UpdateTime</c> on read and deletes with <c>Precondition.LastUpdated(ts)</c>; a
/// concurrent finalize changes the update time so the delete fails (<c>FailedPrecondition</c>) → re-read → no-op on a
/// Processed entry. The deterministic race is driven by the test-only <c>ReleaseRaceHookForTests</c> seam (fires once
/// in that window). <b>RED mutant:</b> revert to an unconditional <c>DeleteAsync</c> → the Processed entry is deleted
/// → <c>IsProcessedAsync</c> false. Never skipped (Firestore emulator availability per the 63xsiv class).
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Firestore")]
[Trait("Component", "Inbox")]
public sealed class FirestoreInboxStoreReleaseRaceShould : IClassFixture<FirestoreInboxStoreContainerFixture>
{
	private const string HandlerType = "TestHandler";
	private readonly FirestoreInboxStoreContainerFixture _fixture;

	public FirestoreInboxStoreReleaseRaceShould(FirestoreInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private FirestoreInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available - real-infra release-race lock is never skipped.");
		var options = Options.Create(new FirestoreInboxOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
		});
		return new FirestoreInboxStore(_fixture.Db, options, NullLogger<FirestoreInboxStore>.Instance);
	}

	[Fact]
	public async Task Preserve_a_concurrently_finalized_entry_in_the_release_race()
	{
		var store = CreateStore();
		var messageId = $"msg-release-race-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		store.ReleaseRaceHookForTests = async hookCt =>
		{
			store.ReleaseRaceHookForTests = null; // fire exactly once
			await store.MarkProcessedAsync(messageId, HandlerType, hookCt).ConfigureAwait(false);
		};

		await store.ReleaseAsync(messageId, HandlerType, ct);

		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue(
			"a concurrent finalize in the release window must NOT let Release delete the now-Processed entry");
	}

	[Fact]
	public async Task Delete_a_non_finalized_entry_on_a_normal_release()
	{
		var store = CreateStore();
		var messageId = $"msg-normal-release-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.ReleaseAsync(messageId, HandlerType, ct);

		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeFalse();
		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue(
			"a released (non-finalized) entry must be re-admitted on redelivery");
	}
}
