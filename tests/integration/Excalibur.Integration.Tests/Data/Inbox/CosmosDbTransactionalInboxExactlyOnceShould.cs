// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// bd-etm9ih (S874) B2 — real-infrastructure exactly-once lock for the Cosmos DB scoped transactional inbox
/// seam <see cref="IScopedTransactionalInboxStore"/> against a live Cosmos emulator. Proves the keystone's
/// headline guarantee under the scenario the inbox exists for: <b>concurrent redelivery of the same message
/// by competing consumers commits the handler side-effect EXACTLY ONCE</b>.
/// </summary>
/// <remarks>
/// NON-VACUOUS by construction: the two concurrent attempts differ only in their side-effect doc id, so the
/// ONLY colliding write is the processed-mark (a deterministic id). With a non-atomic mark
/// (<c>batch.UpsertItem</c> — create-or-replace, never 409) BOTH batches commit → two side-effects, two
/// "true" results → <b>RED</b>. With the atomic mark (<c>batch.CreateItem</c> — first-writer-wins, 2nd gets
/// 409 → whole batch rolls back) exactly one commits → <b>GREEN</b>. Asserts EMITTED behavior (committed
/// side-effect count + processed state) through the real store path, never a mock, never capability-presence.
/// Also folds the earlier-flagged opt-out coverage gap: no <c>SharedPartitionKey</c> means no false atomic
/// advertisement.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "CosmosDb")]
[Trait("Component", "Inbox")]
public sealed class CosmosDbTransactionalInboxExactlyOnceShould
	: IClassFixture<CosmosDbTransactionalInboxExactlyOnceFixture>, IAsyncLifetime
{
	private const string SharedPartition = "inbox-shared";

	private readonly CosmosDbTransactionalInboxExactlyOnceFixture _fixture;

	public CosmosDbTransactionalInboxExactlyOnceShould(CosmosDbTransactionalInboxExactlyOnceFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	// LOUD graceful-degrade for the two emulator-dependent Facts: matches the sibling Cosmos-integration
	// convention (Assert.SkipUnless(_fixture.IsInitialized, ...)). A gated run shows as a SKIPPED test with a
	// visible reason - never a silent pass. The classic Cosmos emulator is unreliable on some hosts
	// ("response ended prematurely"); the empirical RED->GREEN proof runs where the emulator is healthy.
	private void RequireEmulator() =>
		Assert.SkipUnless(
			_fixture.IsInitialized,
			$"SKIPPED: Cosmos emulator unavailable - real-infra exactly-once lock not exercised here. {_fixture.InitError}");

	private CosmosDbInboxStore CreateStore(string? sharedPartitionKey)
	{
		var options = Options.Create(new CosmosDbInboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = _fixture.ContainerName,
			PartitionKeyPath = "/handler_type",
			SharedPartitionKey = sharedPartitionKey,
			DefaultTimeToLiveSeconds = 0,
			Client =
			{
				ConnectionString = _fixture.ConnectionString,
				UseDirectMode = false,
				HttpClientFactory = _fixture.HttpClientFactory,
			},
		});
		return new CosmosDbInboxStore(options, NullLogger<CosmosDbInboxStore>.Instance);
	}

	// A handler-owned side-effect doc, written ENLISTED on the batch so it commits atomically with the mark.
	// Its partition-key field equals the shared partition; its id is unique per attempt so the ONLY colliding
	// write is the processed-mark.
	private sealed class SideEffectDocument
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("handler_type")]
		public string HandlerType { get; set; } = string.Empty;

		[JsonPropertyName("marker")]
		public string Marker { get; set; } = string.Empty;
	}

	private async Task<(bool committed, Exception? error)> TryProcessOnceAsync(string messageId, string handlerType, string marker)
	{
		var store = CreateStore(SharedPartition);
		try
		{
			var committed = await ((IScopedTransactionalInboxStore)store).TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				(scope, ct) =>
				{
					_ = ct;
					var side = new SideEffectDocument
					{
						Id = $"side-{Guid.NewGuid():N}",
						HandlerType = SharedPartition,
						Marker = marker,
					};
					_ = scope.AsCosmosBatch().CreateItem(side);
					return ValueTask.CompletedTask;
				},
				CancellationToken.None).ConfigureAwait(false);
			return (committed, null);
		}
		catch (Exception ex)
		{
			// The losing concurrent committer surfaces the batch 409 as a throw (whole batch rolled back).
			return (false, ex);
		}
	}

	[Fact]
	public async Task Concurrent_redelivery_commits_the_handler_side_effect_exactly_once()
	{
		RequireEmulator();

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.CosmosConcurrentDup";
		var marker = Guid.NewGuid().ToString();

		// Two competing consumers redeliver the SAME message concurrently.
		var results = await Task.WhenAll(
			TryProcessOnceAsync(messageId, handlerType, marker),
			TryProcessOnceAsync(messageId, handlerType, marker)).ConfigureAwait(false);

		var committedCount = results.Count(r => r.committed);
		committedCount.ShouldBe(
			1,
			"exactly one concurrent redelivery may commit - the non-atomic UpsertItem mark lets BOTH commit (RED)");

		// The load-bearing assertion: the handler's durable side-effect exists EXACTLY ONCE.
		(await _fixture.CountSideEffectsAsync(marker, SharedPartition).ConfigureAwait(false))
			.ShouldBe(1, "the handler side-effect commits exactly once - a double-commit means exactly-once was violated");

		// And the message is durably marked processed (a fresh store observes it).
		var verifier = CreateStore(SharedPartition);
		(await verifier.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after a successful transactional process the message is durably marked processed");
	}

	[Fact]
	public async Task Settled_duplicate_is_detected_and_the_handler_does_not_run_again()
	{
		RequireEmulator();

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.CosmosSettledDup";
		var marker = Guid.NewGuid().ToString();

		var first = await TryProcessOnceAsync(messageId, handlerType, marker).ConfigureAwait(false);
		first.committed.ShouldBeTrue("the first delivery processes the message");

		// A later (settled) redelivery: the pre-read sees Processed and returns false without re-running.
		var duplicate = await TryProcessOnceAsync(messageId, handlerType, marker).ConfigureAwait(false);
		duplicate.committed.ShouldBeFalse("an already-processed message is a duplicate - the handler is not invoked");
		duplicate.error.ShouldBeNull("a settled duplicate returns false cleanly, it does not throw");

		(await _fixture.CountSideEffectsAsync(marker, SharedPartition).ConfigureAwait(false))
			.ShouldBe(1, "the committed handler effect happens exactly once - the duplicate never wrote");
	}

	[Fact]
	public void SupportsTransactional_is_false_without_a_shared_partition_key_no_false_atomic_advertisement()
	{
		var withoutSharedKey = CreateStore(sharedPartitionKey: null);
		((IInboxStoreCapabilities)withoutSharedKey).SupportsTransactional.ShouldBeFalse(
			"without a SharedPartitionKey the store must NOT advertise atomicity (Cosmos TransactionalBatch is single-partition)");

		var withSharedKey = CreateStore(SharedPartition);
		((IInboxStoreCapabilities)withSharedKey).SupportsTransactional.ShouldBeTrue(
			"a configured SharedPartitionKey opts the store into transactional (exactly-once) processing");
	}

	[Fact]
	public async Task TryProcessTransactionally_throws_NotSupported_when_no_shared_partition_key_is_configured()
	{
		var store = CreateStore(sharedPartitionKey: null);

		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await ((IScopedTransactionalInboxStore)store).TryProcessTransactionallyAsync(
				Guid.NewGuid().ToString(),
				"Handler.Type.CosmosOptOut",
				(_, _) => ValueTask.CompletedTask,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}
}
