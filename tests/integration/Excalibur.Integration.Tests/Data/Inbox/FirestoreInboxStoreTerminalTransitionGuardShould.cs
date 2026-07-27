// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure lock (bead bkra3g) for <see cref="FirestoreInboxStore"/>'s terminal-protected status
/// transitions: once an entry is finalized to <see cref="InboxStatus.Processed"/>, no later
/// <c>MarkProcessing</c> / <c>MarkFailed</c> may downgrade it back to a non-terminal state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hazard:</b> a GET→Update TOCTOU could overwrite a terminal <c>Processed</c> entry with a
/// non-terminal status → re-admit the message → double-processing. The fix routes every mutating Mark*
/// transition through a <c>Precondition.LastUpdated</c>-guarded conditional update (bounded re-read retry)
/// that treats a terminal <c>Processed</c> entry as a no-op.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on pre-fix blind Update):</b> on the pre-fix impl a <c>MarkProcessing</c>/<c>MarkFailed</c>
/// after finalize unconditionally Updates the entry to Processing/Failed → the status assertion is RED.
/// A mock cannot reproduce the Firestore server-side precondition (per <c>verify-against-real-infra-not-mock</c>).
/// Never skipped: the fixture fails fast when the Firestore emulator is unavailable (63xsiv class).
/// </para>
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Firestore")]
[Trait("Component", "Inbox")]
public sealed class FirestoreInboxStoreTerminalTransitionGuardShould : IClassFixture<FirestoreInboxStoreContainerFixture>
{
	private const string HandlerType = "TestHandler";
	private readonly FirestoreInboxStoreContainerFixture _fixture;

	public FirestoreInboxStoreTerminalTransitionGuardShould(FirestoreInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private FirestoreInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available - real-infra terminal-transition lock is never skipped.");
		var options = Options.Create(new FirestoreInboxOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
		});
		return new FirestoreInboxStore(_fixture.Db, options, NullLogger<FirestoreInboxStore>.Instance);
	}

	[Fact]
	public async Task Refuse_to_downgrade_a_Processed_entry_via_MarkProcessing()
	{
		var store = CreateStore();
		var messageId = $"msg-guard-processing-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		await store.MarkProcessingAsync(messageId, HandlerType, ct);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a terminal Processed entry must never be downgraded to Processing");
	}

	[Fact]
	public async Task Refuse_to_downgrade_a_Processed_entry_via_MarkFailed()
	{
		var store = CreateStore();
		var messageId = $"msg-guard-failed-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		await store.MarkFailedAsync(messageId, HandlerType, "boom", ct);
		await store.MarkFailedAsync(messageId, HandlerType, "boom-again", retryCount: 3, ct);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a terminal Processed entry must never be downgraded to Failed");
	}

	[Fact]
	public async Task Hold_Processed_under_a_concurrent_downgrade_race()
	{
		const int Concurrency = 8;
		var store = CreateStore();
		var messageId = $"msg-guard-race-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		// N callers concurrently attempt to downgrade the terminal entry. The precondition guard must reject all.
		var tasks = Enumerable.Range(0, Concurrency)
			.Select(i => Task.Run(async () =>
			{
				if (i % 2 == 0)
				{
					await store.MarkProcessingAsync(messageId, HandlerType, ct).ConfigureAwait(false);
				}
				else
				{
					await store.MarkFailedAsync(messageId, HandlerType, "race", ct).ConfigureAwait(false);
				}
			}))
			.ToArray();
		await Task.WhenAll(tasks).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			$"none of {Concurrency} concurrent downgrade attempts may move a terminal Processed entry");
	}
}
