// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infra lock: an inbox entry written by a previously published version of this store is judged on
/// the instant it actually carries, not on the shape that instant is stored in.
/// </summary>
/// <remarks>
/// <para>
/// The store's instants are now stored as BSON dates. The driver's default shape for a
/// <see cref="DateTimeOffset"/> is a <c>{ DateTime, Ticks, Offset }</c> sub-document, so every entry
/// already in a consumer's collection when they upgrade is in the other shape, and nothing rewrites it.
/// Query operators are type-bracketed: a date comparison does not match a sub-document at all. Left
/// unhandled, that makes every pre-upgrade entry invisible to the two queries that bound this
/// collection's growth — a processed entry is never reaped and a failed entry is never re-admitted for
/// retry — and it is invisible silently, because "no rows matched" is indistinguishable from "nothing
/// was due".
/// </para>
/// <para>
/// <b>Why the rest of the suite is blind to this.</b> Every other arm in this directory writes through
/// the current store and reads back through it, so writer and reader move together and the population at
/// risk — entries written by the PREVIOUS version — is never constructed. Those arms pass whether or not
/// this defect is present.
/// </para>
/// <para>
/// <b>Fixture shape.</b> <c>LegacyInboxDocument</c> is the pre-change document class: the same element
/// names with no representation attribute on the instants. Inserting through it makes the driver itself
/// produce the genuine legacy encoding rather than a hand-built approximation, so these arms bind the
/// shape a consumer's collection really holds. The staging helper asserts that encoding before any arm
/// proceeds, so a future driver default cannot quietly turn all of this vacuous.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> replace the two-shape reads in the store — restore
/// <c>Filter.Lt(d =&gt; d.ProcessedAt, …)</c> and <c>Filter.Lt(d =&gt; d.LastAttemptAt, …)</c> — and every
/// LEGACY arm below goes RED while the current-shape arm stays green. That asymmetry is the point: the
/// defect is invisible from the current shape alone.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> BSON type bracketing is the server's behaviour and it is
/// the whole subject here — a mocked collection returns whatever it was told and cannot exhibit it.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes these NON-SKIPPED.
/// </para>
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbInboxLegacyInstantShapeShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private const string InboxCollectionName = "inbox_messages";
	private const string HandlerType = "TestHandler";

	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxLegacyInstantShapeShould(MongoDbInboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// The inbox document exactly as a previously published version declared it: no representation
	/// attribute on any instant, so the driver applies its default sub-document encoding.
	/// </summary>
	private sealed class LegacyInboxDocument
	{
		[BsonId]
		[BsonRepresentation(BsonType.String)]
		public string Id { get; set; } = string.Empty;

		[BsonElement("messageId")]
		public string MessageId { get; set; } = string.Empty;

		[BsonElement("handlerType")]
		public string HandlerType { get; set; } = string.Empty;

		[BsonElement("messageType")]
		public string MessageType { get; set; } = string.Empty;

		[BsonElement("payload")]
		public byte[] Payload { get; set; } = [];

		[BsonElement("metadata")]
		public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);

		[BsonElement("receivedAt")]
		public DateTimeOffset ReceivedAt { get; set; }

		[BsonElement("processedAt")]
		public DateTimeOffset? ProcessedAt { get; set; }

		[BsonElement("status")]
		public int Status { get; set; }

		[BsonElement("lastError")]
		public string? LastError { get; set; }

		[BsonElement("retryCount")]
		public int RetryCount { get; set; }

		[BsonElement("lastAttemptAt")]
		public DateTimeOffset? LastAttemptAt { get; set; }
	}

	/// <summary>Builds a store against the shared container.</summary>
	/// <remarks>
	/// TTL is disabled so these arms cannot race MongoDB's background expiry thread. Whether an instant
	/// is TTL-ELIGIBLE is asserted directly, by its stored BSON type, rather than by waiting on a monitor
	/// that runs on its own schedule.
	/// </remarks>
	private MongoDbInboxStore NewStore() =>
		new(
			Options.Create(new MongoDbInboxOptions
			{
				ConnectionString = _fixture.ConnectionString,
				DatabaseName = _fixture.DatabaseName,
				DefaultTtlSeconds = 0,
			}),
			NullLogger<MongoDbInboxStore>.Instance,
			SingleTenantTestContext.Instance);

	private IMongoDatabase Database() =>
		new MongoClient(_fixture.ConnectionString).GetDatabase(_fixture.DatabaseName);

	/// <summary>
	/// Writes one entry the way the previous version would have, and asserts the encoding really is the
	/// legacy one — otherwise every arm below would be exercising the current shape and proving nothing.
	/// </summary>
	private async Task<string> StageLegacyAsync(string id, Action<LegacyInboxDocument> configure)
	{
		var document = new LegacyInboxDocument
		{
			Id = id,
			MessageId = id,
			HandlerType = HandlerType,
			MessageType = "test.message",
			Payload = [1],
			ReceivedAt = DateTimeOffset.UtcNow.AddDays(-60),
		};

		configure(document);

		await Database()
			.GetCollection<LegacyInboxDocument>(InboxCollectionName)
			.InsertOneAsync(document)
			.ConfigureAwait(false);

		var raw = await Database()
			.GetCollection<BsonDocument>(InboxCollectionName)
			.Find(new BsonDocument("_id", id))
			.FirstAsync()
			.ConfigureAwait(false);

		raw["receivedAt"].BsonType.ShouldBe(
			BsonType.Document,
			"these arms are about the encoding a previously published version produced; if the driver "
			+ "wrote a date here the fixture is no longer reproducing that population and every "
			+ "assertion below is vacuous");

		return id;
	}

	private async Task<List<string>> SurvivingIdsAsync() =>
		(await Database()
			.GetCollection<BsonDocument>(InboxCollectionName)
			.Find(new BsonDocument())
			.Project(new BsonDocument("_id", 1))
			.ToListAsync()
			.ConfigureAwait(false))
		.ConvertAll(d => d["_id"].AsString);

	private async Task CreateAndProcessCurrentEntryAsync(MongoDbInboxStore store, string messageId)
	{
		_ = await store.CreateEntryAsync(
			messageId,
			HandlerType,
			"test.message",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal),
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS and SAFETY on retention. A legacy processed entry past the cutoff is reaped; one inside
	/// the window is not.
	/// </summary>
	/// <remarks>
	/// The liveness half is the arm the defect was about: without it, a store that simply never reaps a
	/// legacy entry — leaving a consumer's dedup collection to grow without bound, with the TTL index
	/// over the same field equally unable to see it — reads as perfectly safe.
	/// </remarks>
	[Fact]
	public async Task ReapALegacyProcessedEntryByAgeRatherThanRetainItForever()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"unbounded growth of a consumer's dedup collection is a durability failure — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var old = await StageLegacyAsync("legacy-old", d =>
		{
			d.Status = (int)InboxStatus.Processed;
			d.ProcessedAt = DateTimeOffset.UtcNow.AddDays(-30);
		}).ConfigureAwait(false);

		var recent = await StageLegacyAsync("legacy-recent", d =>
		{
			d.Status = (int)InboxStatus.Processed;
			d.ProcessedAt = DateTimeOffset.UtcNow;
		}).ConfigureAwait(false);

		var deleted = await NewStore()
			.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow.AddDays(-7), CancellationToken.None)
			.ConfigureAwait(false);

		deleted.ShouldBe(1, "exactly the entry older than the cutoff is removed");

		var surviving = await SurvivingIdsAsync().ConfigureAwait(false);
		surviving.ShouldNotContain(old, "a legacy processed entry past the retention cutoff is not retained forever");
		surviving.ShouldContain(recent, "a legacy processed entry inside the retention window is not deleted early");
	}

	/// <summary>
	/// The same cutoff decides both shapes identically when one collection holds a mixture of them, which
	/// is the state every upgraded consumer is actually in.
	/// </summary>
	[Fact]
	public async Task ApplyOneCutoffConsistentlyAcrossAMixedShapeCollection()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var store = NewStore();

		var legacyOld = await StageLegacyAsync("legacy-old", d =>
		{
			d.Status = (int)InboxStatus.Processed;
			d.ProcessedAt = DateTimeOffset.UtcNow.AddDays(-30);
		}).ConfigureAwait(false);

		var legacyRecent = await StageLegacyAsync("legacy-recent", d =>
		{
			d.Status = (int)InboxStatus.Processed;
			d.ProcessedAt = DateTimeOffset.UtcNow;
		}).ConfigureAwait(false);

		// Written by the CURRENT version, so it is stored in the new shape.
		await CreateAndProcessCurrentEntryAsync(store, "current-recent").ConfigureAwait(false);

		var deleted = await store
			.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow.AddDays(-7), CancellationToken.None)
			.ConfigureAwait(false);

		deleted.ShouldBe(1, "only the legacy entry past the cutoff is due; both recent entries are inside the window");

		var surviving = await SurvivingIdsAsync().ConfigureAwait(false);
		surviving.ShouldNotContain(legacyOld);
		surviving.ShouldContain(legacyRecent, "a recent LEGACY entry must not be swept up by a cutoff it precedes");
		surviving.Count.ShouldBe(2, "the current-shape entry is inside the window and survives too");
	}

	/// <summary>
	/// LIVENESS and SAFETY on retry admission: a legacy failed entry whose last attempt precedes the
	/// caller's bound is offered for retry, and one whose last attempt is more recent is not.
	/// </summary>
	[Fact]
	public async Task OfferALegacyFailedEntryForRetryOnceItsLastAttemptHasAged()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a failed entry that is never re-admitted is a message that is never delivered — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aged = await StageLegacyAsync("legacy-aged", d =>
		{
			d.Status = (int)InboxStatus.Failed;
			d.RetryCount = 1;
			d.LastError = "boom";
			d.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-2);
		}).ConfigureAwait(false);

		var fresh = await StageLegacyAsync("legacy-fresh", d =>
		{
			d.Status = (int)InboxStatus.Failed;
			d.RetryCount = 1;
			d.LastError = "boom";
			d.LastAttemptAt = DateTimeOffset.UtcNow;
		}).ConfigureAwait(false);

		var returned = (await NewStore()
			.GetAllTenantsFailedEntriesAsync(
				maxRetries: 5,
				olderThan: DateTimeOffset.UtcNow.AddHours(-1),
				batchSize: 50,
				CancellationToken.None)
			.ConfigureAwait(false))
			.Select(e => e.MessageId)
			.ToList();

		returned.ShouldContain(aged, "a legacy failed entry whose last attempt has aged past the bound must be retryable");
		returned.ShouldNotContain(fresh, "a legacy failed entry attempted moments ago has not aged past the bound");
	}

	/// <summary>
	/// A legacy entry materialises through the current document class carrying the instant it was written
	/// from, so the queries above settle on real data rather than on a default.
	/// </summary>
	/// <remarks>
	/// Recorded as an arm rather than assumed. The representation attribute governs how an instant is
	/// WRITTEN, and the driver's serializer accepts either shape on read — but if that ever ceased to
	/// hold, every read path in this store would start throwing on a consumer's pre-upgrade data.
	/// </remarks>
	[Fact]
	public async Task ReadALegacyEntryBackThroughTheCurrentDocumentClass()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var processed = DateTimeOffset.UtcNow.AddDays(-3);
		_ = await StageLegacyAsync("legacy-roundtrip", d =>
		{
			d.Status = (int)InboxStatus.Processed;
			d.ProcessedAt = processed;
		}).ConfigureAwait(false);

		var entries = (await NewStore()
			.GetAllTenantsEntriesAsync(CancellationToken.None)
			.ConfigureAwait(false))
			.ToList();

		var entry = entries.SingleOrDefault(e => e.MessageId == "legacy-roundtrip");
		entry.ShouldNotBeNull("a legacy entry must still be readable");
		entry!.ProcessedAt.ShouldNotBeNull();
		entry.ProcessedAt!.Value.ToUnixTimeMilliseconds().ShouldBe(
			processed.ToUnixTimeMilliseconds(),
			"the instant a legacy entry carries survives being read through the current document class");
	}

	/// <summary>
	/// The storage-shape contract itself: an entry written by this version stores every instant as a BSON
	/// date, which is what makes it a date to an aggregation, to an index, to a TTL index, and to a
	/// consumer reading this collection from another language.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if a representation attribute is dropped or overridden by a registered
	/// serializer. A TTL index over a field that is not a date expires nothing at all, silently, so this
	/// assertion is what stands between the configured retention and it quietly not happening.
	/// </remarks>
	[Fact]
	public async Task StoreEveryInstantItWritesAsABsonDate()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var store = NewStore();
		_ = await store.CreateEntryAsync(
			"current-shape",
			HandlerType,
			"test.message",
			[1],
			new Dictionary<string, object>(StringComparer.Ordinal),
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkFailedAsync("current-shape", HandlerType, "boom", CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync("current-shape", HandlerType, CancellationToken.None).ConfigureAwait(false);

		var raw = await Database()
			.GetCollection<BsonDocument>(InboxCollectionName)
			.Find(new BsonDocument())
			.FirstAsync()
			.ConfigureAwait(false);

		foreach (var field in new[] { "receivedAt", "processedAt", "lastAttemptAt" })
		{
			raw.Contains(field).ShouldBeTrue($"'{field}' should have been written");
			raw[field].BsonType.ShouldBe(
				BsonType.DateTime,
				$"'{field}' must persist as a BSON date. Stored as a sub-document it is not a date to an "
				+ "aggregation, to a date index, to the TTL index declared over it, or to a consumer "
				+ "reading this collection from any other driver");
		}
	}
}
