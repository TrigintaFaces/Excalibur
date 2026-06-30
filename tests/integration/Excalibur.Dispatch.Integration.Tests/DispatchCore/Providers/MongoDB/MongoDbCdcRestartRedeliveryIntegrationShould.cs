// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Cdc.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDB;

/// <summary>
/// Genuine, NON-SKIPPED real-infra restart-redelivery lock for the MongoDB change-stream CDC processor
/// (e9u90j / AC-N3.4 — CDC streaming restart-redelivery data-loss safety).
/// </summary>
/// <remarks>
/// <para>
/// Unlike the sibling <c>MongoDbCdcStalePositionIntegrationShould</c> (which only exercises the stale-position
/// <em>detector</em> with simulated exceptions because change streams need a replica set), this test drives a
/// <b>real <see cref="MongoDbCdcProcessor"/> against a real change stream</b>. The shared
/// <see cref="MongoDbContainerFixture"/> now starts a single-node replica set (<c>rs0</c>), which is the hard
/// requirement for change streams — so this lock is NEVER skipped (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// <para>
/// <b>Invariant under test (no data loss across a restart):</b> a processor that confirms a resume token, then
/// is torn down and replaced by a fresh processor with the <em>same</em> <c>ProcessorId</c> and the same durable
/// <see cref="MongoDbCdcStateStore"/>, MUST resume from the confirmed token and redeliver every change that
/// occurred after it — none are dropped in the gap. The resume token is persisted via the state store and
/// re-applied as the change stream's <c>ResumeAfter</c>; if that persistence/resume path regressed, processor #2
/// would start "at now" and silently skip the post-confirmation change → this test goes RED.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.MongoDB)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "MongoDB")]
[Trait("SubComponent", "RestartRedelivery")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbCdcRestartRedeliveryIntegrationShould : IntegrationTestBase
{
	private readonly MongoDbContainerFixture _mongoFixture;

	public MongoDbCdcRestartRedeliveryIntegrationShould(MongoDbContainerFixture mongoFixture)
	{
		_mongoFixture = mongoFixture;
	}

	[Fact]
	public async Task ResumeFromConfirmedToken_RedeliversChangesAfterRestart_WithoutDataLoss()
	{
		// Real replica set is a hard requirement (never skip) — assert Docker is up (verify-against-real-infra-not-mock).
		_mongoFixture.DockerAvailable.ShouldBeTrue(
			"e9u90j: the MongoDB change-stream restart-redelivery lock requires a real replica-set container and is NEVER skipped.");

		var client = new MongoClient(_mongoFixture.ConnectionString);
		var databaseName = $"cdc_redelivery_{Guid.NewGuid():N}";
		var collectionName = "orders";
		var database = client.GetDatabase(databaseName);
		var collection = database.GetCollection<BsonDocument>(collectionName);

		// A single durable state store shared across BOTH processor lifetimes — this is what carries the
		// confirmed resume token across the "restart".
		var stateStoreOptions = MsOptions.Create(new MongoDbCdcStateStoreOptions
		{
			DatabaseName = databaseName,
			CollectionName = "cdc_state",
		});
		await using var stateStore = new MongoDbCdcStateStore(client, stateStoreOptions);

		const string processorId = "redelivery-processor";
		var cdcOptions = MsOptions.Create(new MongoDbCdcOptions
		{
			DatabaseName = databaseName,
			CollectionNames = [collectionName],
			ProcessorId = processorId,
			BatchSize = 1,
			// Filter to inserts — a realistic config that also yields a non-null change-stream pipeline.
			// (The no-filter default currently NREs in the provider — tracked as bd-6idsbx.)
			ChangeStream = new MongoDbChangeStreamOptions { OperationTypes = ["insert"], FullDocument = true },
		});

		try
		{
			// ── Phase 1: processor #1 captures a change and confirms its resume token. ──
			// A Mongo change stream opened without a token starts "at now", so the change must occur WHILE the
			// watch is open. Each attempt opens a fresh watch and inserts a unique seed into that window, so the
			// loop converges on a capture without depending on wall-clock timing. Once any change is captured,
			// ProcessBatchAsync durably saves the resume token (the whole point of phase 1).
			var firstSeen = new ConcurrentQueue<string>();
			await using (var processor1 = new MongoDbCdcProcessor(
				client, cdcOptions, stateStore, NullLogger<MongoDbCdcProcessor>.Instance))
			{
				var captured = await CaptureSeedAsync(processor1, collection, firstSeen);
				captured.ShouldBeTrue("processor #1 must capture a change and durably confirm a resume token.");
			}

			// The confirmed token is now durably stored. Insert the target change AFTER processor #1 is gone —
			// this is the change that must not be lost across the restart.
			await InsertOrderAsync(collection, "order-2");

			// ── Phase 2: a fresh processor with the SAME ProcessorId + state store resumes and redelivers. ──
			var secondSeen = new ConcurrentQueue<string>();
			await using (var processor2 = new MongoDbCdcProcessor(
				client, cdcOptions, stateStore, NullLogger<MongoDbCdcProcessor>.Instance))
			{
				// No concurrent insert needed: resuming from the confirmed token replays order-2 immediately.
				await PollUntilCapturedAsync(
					processor2,
					recorder: secondSeen,
					predicate: q => q.Contains("order-2"));
			}

			// No data loss: the change that happened after processor #1's confirmation was redelivered to #2.
			secondSeen.ShouldContain("order-2",
				"processor #2 must resume from the confirmed token and redeliver the post-restart change (no data loss).");
		}
		finally
		{
			await client.DropDatabaseAsync(databaseName, TestCancellationToken);
		}
	}

	private static async Task InsertOrderAsync(IMongoCollection<BsonDocument> collection, string orderId)
	{
		await collection.InsertOneAsync(new BsonDocument
		{
			{ "_id", ObjectId.GenerateNewId() },
			{ "orderId", orderId },
		});
	}

	private static string? ExtractOrderId(MongoDbDataChangeEvent change) =>
		change.FullDocument?.TryGetValue("orderId", out var v) == true ? v.AsString : null;

	/// <summary>
	/// Opens <paramref name="processor"/>'s batch and inserts a unique seed into each watch window until a
	/// change is captured (and its resume token saved). Inserting a fresh seed per attempt avoids the
	/// start-at-now race — a fresh stream cannot see a one-time insert that already happened in the past.
	/// No wall-clock sleep assertion; the loop converges on the first insert that lands inside an open watch.
	/// </summary>
	private async Task<bool> CaptureSeedAsync(
		MongoDbCdcProcessor processor,
		IMongoCollection<BsonDocument> collection,
		ConcurrentQueue<string> recorder)
	{
		Task Handler(MongoDbDataChangeEvent change, CancellationToken ct)
		{
			var id = ExtractOrderId(change);
			if (id is not null) recorder.Enqueue(id);
			return Task.CompletedTask;
		}

		for (var attempt = 0; attempt < 40 && recorder.IsEmpty; attempt++)
		{
			// Open the stream on a background task, then insert so the change occurs inside the watch window.
			var batchTask = processor.ProcessBatchAsync(Handler, TestCancellationToken);
			await InsertOrderAsync(collection, $"seed-{attempt}").ConfigureAwait(false);
			_ = await batchTask.ConfigureAwait(false);
		}

		return !recorder.IsEmpty;
	}

	/// <summary>
	/// Polls <paramref name="processor"/>'s batch until <paramref name="predicate"/> holds over the recorder.
	/// </summary>
	private async Task PollUntilCapturedAsync(
		MongoDbCdcProcessor processor,
		ConcurrentQueue<string> recorder,
		Func<IReadOnlyCollection<string>, bool> predicate)
	{
		Task Handler(MongoDbDataChangeEvent change, CancellationToken ct)
		{
			var id = ExtractOrderId(change);
			if (id is not null) recorder.Enqueue(id);
			return Task.CompletedTask;
		}

		for (var attempt = 0; attempt < 30 && !predicate(recorder); attempt++)
		{
			_ = await processor.ProcessBatchAsync(Handler, TestCancellationToken).ConfigureAwait(false);
		}
	}
}
