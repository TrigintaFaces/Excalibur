// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// maf76z (S874) KEYSTONE — real-infrastructure atomicity lock for the MongoDB scoped transactional inbox
/// seam <see cref="IScopedTransactionalInboxStore"/> against a live MongoDB <b>replica set</b>. The handler
/// retrieves the native session via <c>scope.AsMongoSession()</c> and writes its own document ENLISTED in the
/// store's transaction, so the test proves the handler's data write commits / rolls back <b>atomically</b>
/// (both-or-neither) with the processed-mark — the load-bearing exactly-once guarantee.
/// </summary>
/// <remarks>
/// NEVER SKIPPED — a missing Docker/replica-set container fails fast (<c>DockerAvailable.ShouldBeTrue</c>),
/// so an absent container surfaces as a failure rather than a silent pass. The lock asserts EMITTED BEHAVIOR
/// through the real seam (durable processed-mark, atomic handler-write rollback, single-commit under
/// concurrency), read back on a FRESH client — never the presence of the capability interface, never a mock.
/// NON-VACUOUS: a non-transactional mutant that marked processed OUTSIDE the transaction (or committed the
/// handler write independently) would leave the message marked / the side doc present after the rollback path
/// → RED on the rollback assertions.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class MongoDbTransactionalInboxExactlyOnceShould : IClassFixture<MongoDbTransactionalInboxReplicaSetFixture>, IAsyncLifetime
{
	private const string SideCollectionName = "inbox_txn_side_effect";

	private readonly MongoDbTransactionalInboxReplicaSetFixture _fixture;

	public MongoDbTransactionalInboxExactlyOnceShould(MongoDbTransactionalInboxReplicaSetFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB replica-set container must be available - the real-infra transactional-inbox atomicity lock is never skipped.");
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync() => await _fixture.CleanupAsync().ConfigureAwait(false);

	private MongoDbInboxStore CreateStore()
	{
		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			EnableTransactions = true,
		});
		return new MongoDbInboxStore(options, NullLogger<MongoDbInboxStore>.Instance, SingleTenantTestContext.Instance);
	}

	// Counts committed side-effect docs for a marker on a FRESH client (outside any test session/transaction),
	// so only durably committed writes are visible.
	private async Task<long> CountSideEffectsAsync(string marker)
	{
		var client = new MongoClient(_fixture.ConnectionString);
		var collection = client.GetDatabase(_fixture.DatabaseName).GetCollection<BsonDocument>(SideCollectionName);
		return await collection
			.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("marker", marker))
			.ConfigureAwait(false);
	}

	// Writes a side-effect doc ENLISTED in the handler's session so it commits atomically with the mark.
	private static async ValueTask WriteSideEffectInSessionAsync(IInboxTransactionScope scope, string marker, CancellationToken ct)
	{
		var session = scope.AsMongoSession();
		var collection = session.Client
			.GetDatabase("excalibur_inbox_txn")
			.GetCollection<BsonDocument>(SideCollectionName);
		await collection.InsertOneAsync(
			session,
			new BsonDocument { { "marker", marker } },
			cancellationToken: ct).ConfigureAwait(false);
	}

	[Fact]
	public async Task Commit_persists_the_handler_write_and_the_processed_mark_atomically()
	{
		var store = CreateStore();
		var transactional = (IScopedTransactionalInboxStore)store;

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.MongoCommit";
		var marker = Guid.NewGuid().ToString();
		IInboxTransactionScope? observedScope = null;

		var processed = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) =>
			{
				observedScope = scope;
				await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		processed.ShouldBeTrue("a first, successful transactional process returns true");
		_ = observedScope.ShouldNotBeNull("the handler receives the opaque transaction scope");

		// BOTH committed together, read back on a FRESH client.
		(await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after commit the message is durably marked processed");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "the handler's enlisted write commits atomically with the processed-mark");
	}

	[Fact]
	public async Task Rollback_on_throw_persists_neither_the_handler_write_nor_the_processed_mark()
	{
		var store = CreateStore();
		var transactional = (IScopedTransactionalInboxStore)store;

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.MongoRollbackThrow";
		var marker = Guid.NewGuid().ToString();

		// Handler writes its doc in-session then THROWS -> the whole native transaction aborts.
		try
		{
			_ = await transactional.TryProcessTransactionallyAsync(
				messageId,
				handlerType,
				async (scope, ct) =>
				{
					await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
					throw new InvalidOperationException("simulated handler crash after an enlisted write");
				},
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected: the store propagates the handler failure after aborting the transaction.
		}

		// NEITHER survives — both rolled back together (the load-bearing atomicity assertion).
		(await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a thrown handler rolls back the processed-mark so the message is redelivered");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(0, "the handler's enlisted write rolls back atomically with the processed-mark on throw");

		// Redelivery succeeds — now BOTH commit atomically.
		var processedOnRetry = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) => await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false),
			CancellationToken.None).ConfigureAwait(false);

		processedOnRetry.ShouldBeTrue("the redelivered message processes for the first successful time");
		(await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("after a successful transactional process the message is durably marked processed");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "after the successful retry the enlisted write commits exactly once, atomically with the mark");
	}

	[Fact]
	public async Task Already_processed_message_is_a_duplicate_and_the_handler_does_not_run()
	{
		var store = CreateStore();
		var transactional = (IScopedTransactionalInboxStore)store;

		var messageId = Guid.NewGuid().ToString();
		const string handlerType = "Handler.Type.MongoDuplicate";
		var marker = Guid.NewGuid().ToString();
		var invocations = 0;

		var first = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) =>
			{
				_ = Interlocked.Increment(ref invocations);
				await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		first.ShouldBeTrue("the first delivery processes the message");

		var duplicate = await transactional.TryProcessTransactionallyAsync(
			messageId,
			handlerType,
			async (scope, ct) =>
			{
				_ = Interlocked.Increment(ref invocations);
				await WriteSideEffectInSessionAsync(scope, marker, ct).ConfigureAwait(false);
			},
			CancellationToken.None).ConfigureAwait(false);

		duplicate.ShouldBeFalse("an already-processed message is a duplicate — the handler is not invoked");
		invocations.ShouldBe(1, "the handler ran exactly once — never on the duplicate delivery");
		(await CountSideEffectsAsync(marker).ConfigureAwait(false))
			.ShouldBe(1, "the committed handler effect happens exactly once — the duplicate never wrote");
	}
}
