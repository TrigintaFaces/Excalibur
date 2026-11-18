// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Regression")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxClaimCollationShould(MongoDbOutboxStoreContainerFixture fixture, ITestOutputHelper output)
{
	[Theory]
	[InlineData(true, 0)]
	[InlineData(true, 1)]
	[InlineData(true, 2)]
	[InlineData(true, 3)]
	[InlineData(false, 0)]
	[InlineData(false, 1)]
	[InlineData(false, 2)]
	[InlineData(false, 3)]
	public async Task PreserveBinaryMessageAndAcquisitionIdentityUnderTheCollectionCollation(bool simple, int operation)
	{
		fixture.DockerAvailable.ShouldBeTrue("This claim identity control requires the real MongoDB server; never skipped.");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var client = new MongoClient(fixture.ConnectionString);
		var database = client.GetDatabase(fixture.DatabaseName);
		var name = "claim_collation_" + Guid.NewGuid().ToString("N");
		var collation = simple ? Collation.Simple : new Collation("en", strength: CollationStrength.Secondary);
		Exception? primary = null;
		Exception? cleanupError = null;
		try
		{
			// Even an uncertain partial provisioning result is inside the owned cleanup guard.
			await database.CreateCollectionAsync(name, new CreateCollectionOptions { Collation = collation }, deadline.Token);
			using (var descriptions = await database.ListCollectionsAsync(new ListCollectionsOptions
			{
				Filter = new BsonDocument("name", name),
			}, deadline.Token))
			{
				var description = (await descriptions.ToListAsync(deadline.Token)).ShouldHaveSingleItem();
				output.WriteLine("OWNED_MONGO_COLLECTION_METADATA: " + description.ToJson());
				var collectionOptions = description["options"].AsBsonDocument;
				if (simple)
				{
					// Mongo may omit an explicit default-simple option; the comparator control below proves binary behavior.
					if (collectionOptions.TryGetValue("collation", out var actual)) { actual["locale"].AsString.ShouldBe("simple"); }
				}
				else
				{
					collectionOptions["collation"]["locale"].AsString.ShouldBe("en");
					collectionOptions["collation"]["strength"].AsInt32.ShouldBe(2);
				}
			}
			var options = new MongoDbOutboxOptions
			{
				ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
				CollectionName = name, ProcessorId = "case-sensitive-owner",
			};
			await using var store = new MongoDbOutboxStore(Options.Create(options), NullLogger<MongoDbOutboxStore>.Instance);
			var row = new OutboundMessage("case-event", [0, 255, 128, 1], "case-destination")
			{
				Id = "case-message-" + Guid.NewGuid().ToString("N"), TenantId = "case-tenant",
			};
			if (!simple)
			{
				// Historical RED proved unsafe mutation here. This mode is now explicitly unsupported;
				// seed a genuine legacy provider document without asking the rejecting store to stage it.
				await RejectUnsafeLegacyNamespaceAsync(database, name, options, row, operation, deadline.Token);
				return;
			}
			await store.StageMessageAsync(row, deadline.Token);
			var acquired = (await store.GetUnsentMessagesAsync(1, deadline.Token)).ShouldHaveSingleItem();
			acquired.Id.ShouldBe(row.Id);
			var receipt = acquired.DispatcherId;
			receipt.ShouldNotBeNullOrWhiteSpace();
			var wrongReceipt = ChangeOneAsciiLetterCase(receipt);
			var wrongId = ChangeOneAsciiLetterCase(row.Id);
			string.Equals(receipt, wrongReceipt, StringComparison.Ordinal).ShouldBeFalse();
			string.Equals(row.Id, wrongId, StringComparison.Ordinal).ShouldBeFalse();
			var collection = database.GetCollection<BsonDocument>(name);
			using (var indexes = await collection.Indexes.ListAsync(deadline.Token))
			{
				var metadata = await indexes.ToListAsync(deadline.Token);
				metadata.ShouldContain(index => index["name"].AsString == "_id_");
				foreach (var index in metadata) { output.WriteLine("OWNED_MONGO_INDEX_METADATA: " + index.ToJson()); }
			}
			// Capture both actual planners BEFORE the expected semantic RED assertion, without assuming an indexed seek.
			await CapturePlannerAsync(database, name, row.Id, false, deadline.Token);
			await CapturePlannerAsync(database, name, row.Id, true, deadline.Token);
			// Deliberately omit operation collation: independently demonstrate the collection's actual comparator.
			var comparison = await database.RunCommandAsync<BsonDocument>(new BsonDocument
			{
				{ "aggregate", name },
				{ "pipeline", new BsonArray
					{
						new BsonDocument("$match", new BsonDocument("_id", row.Id)),
						new BsonDocument("$project", new BsonDocument
						{
							{ "_id", 0 },
							{ "sameReceipt", new BsonDocument("$eq", new BsonArray { "$leasedBy", new BsonDocument("$literal", wrongReceipt) }) },
							{ "sameId", new BsonDocument("$eq", new BsonArray { "$_id", new BsonDocument("$literal", wrongId) }) },
						}),
					}
				},
				{ "cursor", new BsonDocument() },
			}, cancellationToken: deadline.Token);
			var comparator = comparison["cursor"]["firstBatch"].AsBsonArray.ShouldHaveSingleItem();
			comparator["sameReceipt"].AsBoolean.ShouldBe(!simple);
			comparator["sameId"].AsBoolean.ShouldBe(!simple);
			var before = await ReadRawBytesAsync(database, name, row.Id, deadline.Token);
			(await Complete(store, row.Id, wrongReceipt, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
			(await ReadRawBytesAsync(database, name, row.Id, deadline.Token)).ShouldBe(before,
				"wrong acquisition must not change any raw document field");
			(await Complete(store, wrongId, receipt, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
			(await ReadRawBytesAsync(database, name, row.Id, deadline.Token)).ShouldBe(before,
				"a collation-equivalent but binary-distinct message ID is not the acquired row");
			(await Complete(store, row.Id, receipt, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
			var persisted = await collection.Find(new BsonDocument("_id", row.Id), new FindOptions { Collation = Collation.Simple }).SingleAsync(deadline.Token);
			persisted["status"].AsInt32.ShouldBe((int)(operation == 0 ? OutboxStatus.Sent : operation == 3 ? OutboxStatus.DeadLettered
				: operation == 2 ? OutboxStatus.Staged : OutboxStatus.Failed));
			persisted["payload"].AsBsonBinaryData.Bytes.ShouldBe(row.Payload);
			persisted["tenantId"].AsString.ShouldBe("case-tenant");
			persisted.GetValue("leasedBy", BsonNull.Value).IsBsonNull.ShouldBeTrue();
			persisted.GetValue("leasedAt", BsonNull.Value).IsBsonNull.ShouldBeTrue();
			persisted.Contains("outcome").ShouldBeFalse();
			if (operation == 0) { persisted["sentAt"].IsBsonNull.ShouldBeFalse(); }
			else
			{
				persisted["retryCount"].AsInt32.ShouldBe(2);
				persisted["lastError"].AsString.ShouldBe(operation == 3 ? "case-dead" : operation == 2 ? "case-backoff" : "case-failure");
				if (operation is 1 or 2)
				{
					persisted["nextAttemptAt"].AsBsonDateTime.MillisecondsSinceEpoch.ShouldBeGreaterThan(persisted["lastAttemptAt"].AsBsonDateTime.MillisecondsSinceEpoch);
				}
			}
		}
		catch (Exception error)
		{
			primary = error;
			throw;
		}
		finally
		{
			// The exact UUID-named collection is owned by this case; do not drop the shared fixture database.
			using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try
			{
				await database.DropCollectionAsync(name, cleanup.Token);
			}
			catch (MongoCommandException error) when (error.Code == 26)
			{
				output.WriteLine("OWNED_MONGO_CLEANUP: collection absent after uncertain provisioning");
			}
			catch (Exception cleanupFailure)
			{
				output.WriteLine("OWNED_MONGO_CLEANUP_ERROR: " + cleanupFailure.GetType().FullName
					+ "; PRIMARY: " + (primary?.GetType().FullName ?? "none"));
				if (primary is null) { cleanupError = cleanupFailure; }
			}
		}
		if (cleanupError is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupError).Throw(); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RetryFailedAndCanceledAdmissionThenPreserveDistinctCaseIdsUnderConcurrentCreation(bool borrowed)
	{
		fixture.DockerAvailable.ShouldBeTrue("Real schema admission must execute, never skip.");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var client = new MongoClient(fixture.ConnectionString);
		var database = client.GetDatabase(fixture.DatabaseName);
		var name = "schema_retry_" + Guid.NewGuid().ToString("N");
		Exception? primary = null;
		Exception? cleanupError = null;
		try
		{
			await database.CreateCollectionAsync(name, new CreateCollectionOptions
			{
				Collation = new Collation("en", strength: CollationStrength.Secondary),
			}, deadline.Token);
			var options = new MongoDbOutboxOptions
			{
				ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
				CollectionName = name, ProcessorId = "schema-retry-owner",
			};
			await using var first = borrowed
				? new MongoDbOutboxStore(client, Options.Create(options), NullLogger<MongoDbOutboxStore>.Instance)
				: new MongoDbOutboxStore(Options.Create(options), NullLogger<MongoDbOutboxStore>.Instance);
			var lower = new OutboundMessage("case", [1, 2], "destination") { Id = "case-a", TenantId = "tenant-lower" };
			var upper = new OutboundMessage("case", [3, 4], "destination") { Id = "CASE-A", TenantId = "tenant-upper" };
			await Should.ThrowAsync<InvalidOperationException>(async () => await first.StageMessageAsync(lower, deadline.Token));
			// Only this test owns the never-admitted namespace. No production migration is performed.
			await database.DropCollectionAsync(name, deadline.Token);
			using var canceled = new CancellationTokenSource();
			await canceled.CancelAsync();
			await Should.ThrowAsync<OperationCanceledException>(async () => await first.StageMessageAsync(lower, canceled.Token));
			await using var second = new MongoDbOutboxStore(client, Options.Create(options), NullLogger<MongoDbOutboxStore>.Instance);
			// Two public store instances initialize the same absent namespace; both must finish
			// with binary identity and no lost row. Exact Code48 interleaving is not asserted.
			await Task.WhenAll(first.StageMessageAsync(lower, deadline.Token).AsTask(), second.StageMessageAsync(upper, deadline.Token).AsTask());
			var collection = database.GetCollection<BsonDocument>(name);
			(await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: deadline.Token)).ShouldBe(2);
			var lowerDoc = await collection.Find(new BsonDocument("_id", lower.Id)).SingleAsync(deadline.Token);
			var upperDoc = await collection.Find(new BsonDocument("_id", upper.Id)).SingleAsync(deadline.Token);
			lowerDoc["tenantId"].AsString.ShouldBe(lower.TenantId);
			upperDoc["tenantId"].AsString.ShouldBe(upper.TenantId);
			lowerDoc["payload"].AsBsonBinaryData.Bytes.ShouldBe(lower.Payload);
			upperDoc["payload"].AsBsonBinaryData.Bytes.ShouldBe(upper.Payload);
			var claim = (await first.GetUnsentMessagesAsync(2, deadline.Token)).ToArray();
			claim.Length.ShouldBe(2);
			foreach (var acquired in claim)
			{
				(await Complete(first, acquired.Id, acquired.DispatcherId!, 0, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
			}
			foreach (var id in new[] { lower.Id, upper.Id })
			{
				var persisted = await collection.Find(new BsonDocument("_id", id)).SingleAsync(deadline.Token);
				persisted["status"].AsInt32.ShouldBe((int)OutboxStatus.Sent);
				var plan = await database.RunCommandAsync<BsonDocument>(new BsonDocument
				{
					{ "explain", new BsonDocument { { "find", name }, { "filter", new BsonDocument("_id", id) } } },
					{ "verbosity", "executionStats" },
				}, cancellationToken: deadline.Token);
				output.WriteLine("OWNED_SIMPLE_POINT_LOOKUP: " + plan.ToJson());
				plan["executionStats"]["nReturned"].ToInt64().ShouldBe(1);
				plan["executionStats"]["totalDocsExamined"].ToInt64().ShouldBe(1);
				plan["queryPlanner"]["winningPlan"].ToJson().ShouldNotContain("COLLSCAN");
			}
		}
		catch (Exception error) { primary = error; throw; }
		finally
		{
			using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try { await database.DropCollectionAsync(name, cleanup.Token); }
			catch (MongoCommandException error) when (error.Code == 26) { }
			catch (Exception error)
			{
				output.WriteLine("OWNED_SCHEMA_RETRY_CLEANUP_ERROR: " + error.GetType().FullName);
				if (primary is null) { cleanupError = error; }
			}
		}
		if (cleanupError is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupError).Throw(); }
	}

	private async Task RejectUnsafeLegacyNamespaceAsync(IMongoDatabase database, string name, MongoDbOutboxOptions options,
		OutboundMessage row, int operation, CancellationToken token)
	{
		var donorName = name + "_donor";
		Exception? primary = null;
		Exception? cleanupError = null;
		try
		{
			var donorOptions = new MongoDbOutboxOptions
			{
				ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
				CollectionName = donorName, ProcessorId = "donor-owner",
			};
			await using var donor = new MongoDbOutboxStore(Options.Create(donorOptions), NullLogger<MongoDbOutboxStore>.Instance);
			await donor.StageMessageAsync(row, token);
			var claim = (await donor.GetUnsentMessagesAsync(1, token)).ShouldHaveSingleItem();
			var source = await database.GetCollection<BsonDocument>(donorName).Find(new BsonDocument("_id", row.Id)).SingleAsync(token);
			var target = database.GetCollection<BsonDocument>(name);
			await target.InsertOneAsync(source, cancellationToken: token);
			var before = await ReadRawBytesAsync(database, name, row.Id, token);
			using var indexesBeforeCursor = await target.Indexes.ListAsync(token);
			var indexesBefore = (await indexesBeforeCursor.ToListAsync(token)).Select(index => index.ToBson()).ToArray();
			foreach (var borrowed in new[] { false, true })
			{
				var localOptions = new MongoDbOutboxOptions
				{
					ConnectionString = options.ConnectionString, DatabaseName = options.DatabaseName,
					CollectionName = options.CollectionName, ProcessorId = options.ProcessorId,
				};
				await using var rejected = borrowed
					? new MongoDbOutboxStore(new MongoClient(fixture.ConnectionString), Options.Create(localOptions), NullLogger<MongoDbOutboxStore>.Instance)
					: new MongoDbOutboxStore(Options.Create(localOptions), NullLogger<MongoDbOutboxStore>.Instance);
				// The injected constructor already captured the unsafe effective collection. A mutable
				// option must not redirect admission to the existing safe donor namespace.
				if (borrowed) { localOptions.CollectionName = donorName; }
				// Each repeated attempt must re-admit and refuse before any index/data change.
				for (var attempt = 0; attempt < 2; attempt++)
				{
					var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
						await Complete(rejected, row.Id, claim.DispatcherId!, operation, token));
					error.Message.ShouldContain(name);
					error.Message.ShouldContain("simple (binary)");
				}
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.StageMessageAsync(
					new OutboundMessage("new", [9], "destination") { Id = row.Id + "-new" }, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.GetUnsentMessagesAsync(1, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.MarkSentAsync(row.Id, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.MarkFailedAsync(row.Id, "legacy", 2, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.MarkFailedWithBackoffAsync(row.Id, "legacy", 2, DateTimeOffset.UtcNow.AddMinutes(1), token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.MarkDeadLetteredAsync(row.Id, "legacy", 2, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.CleanupAllTenantsSentMessagesAsync(DateTimeOffset.UtcNow, 10, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.GetAllTenantsFailedMessagesAsync(3, null, 10, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow, 10, token));
				await Should.ThrowAsync<InvalidOperationException>(async () => await rejected.GetAllTenantsStatisticsAsync(token));
				(await ReadRawBytesAsync(database, name, row.Id, token)).ShouldBe(before);
				(await target.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: token)).ShouldBe(1);
				using var indexesAfterCursor = await target.Indexes.ListAsync(token);
				var indexesAfter = (await indexesAfterCursor.ToListAsync(token)).Select(index => index.ToBson()).ToArray();
				indexesAfter.Length.ShouldBe(indexesBefore.Length);
				for (var index = 0; index < indexesBefore.Length; index++) { indexesAfter[index].ShouldBe(indexesBefore[index]); }
			}
		}
		catch (Exception error) { primary = error; throw; }
		finally
		{
			using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try { await database.DropCollectionAsync(donorName, cleanup.Token); }
			catch (MongoCommandException error) when (error.Code == 26) { }
			catch (Exception error)
			{
				output.WriteLine("OWNED_DONOR_CLEANUP_ERROR: " + error.GetType().FullName);
				if (primary is null) { cleanupError = error; }
			}
		}
		if (cleanupError is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupError).Throw(); }
	}

	private async Task CapturePlannerAsync(IMongoDatabase database, string name, string id, bool simple, CancellationToken token)
	{
		var find = new BsonDocument { { "find", name }, { "filter", new BsonDocument("_id", id) } };
		if (simple) { find.Add("collation", new BsonDocument("locale", "simple")); }
		var command = new BsonDocument { { "explain", find }, { "verbosity", "queryPlanner" } };
		var plan = await database.RunCommandAsync<BsonDocument>(command, cancellationToken: token);
		output.WriteLine("OWNED_MONGO_EXPLAIN_" + (simple ? "SIMPLE" : "DEFAULT") + ": " + plan.ToJson());
		plan.Contains("queryPlanner").ShouldBeTrue();
		output.WriteLine("OWNED_MONGO_WINNING_PLAN_" + (simple ? "SIMPLE" : "DEFAULT") + ": " + plan["queryPlanner"]["winningPlan"].ToJson());
	}

	private static async Task<byte[]> ReadRawBytesAsync(IMongoDatabase database, string name, string id, CancellationToken token)
	{
		using var raw = await database.GetCollection<RawBsonDocument>(name)
			.Find(new BsonDocument("_id", id), new FindOptions { Collation = Collation.Simple }).SingleAsync(token);
		return raw.ToBson();
	}

	private static string ChangeOneAsciiLetterCase(string value)
	{
		var changed = value.ToCharArray();
		for (var index = 0; index < changed.Length; index++)
		{
			if (changed[index] is >= 'a' and <= 'z') { changed[index] = (char)(changed[index] - ('a' - 'A')); return new string(changed); }
			if (changed[index] is >= 'A' and <= 'Z') { changed[index] = (char)(changed[index] + ('a' - 'A')); return new string(changed); }
		}
		throw new InvalidOperationException("Actual opaque acquisition has no case-discriminating control character.");
	}

	private static ValueTask<OutboxCompletionOutcome> Complete(MongoDbOutboxStore store, string id, string receipt, int operation, CancellationToken token)
	{
		var capabilities = (IServiceProvider)store;
		var strong = capabilities.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
		return operation switch
		{
			0 => strong.MarkSentAsync(id, receipt, token),
			1 => strong.MarkFailedAsync(id, "case-failure", 2, receipt, token),
			2 => strong.MarkFailedWithBackoffAsync(id, "case-backoff", 2, DateTimeOffset.UtcNow.AddMinutes(1), receipt, token),
			3 => capabilities.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>()
				.MarkDeadLetteredAsync(id, "case-dead", 2, receipt, token),
			_ => throw new ArgumentOutOfRangeException(nameof(operation)),
		};
	}
}
