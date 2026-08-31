// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Author≠impl real-infra lock: a message staged by a previously published version of the store is
/// judged on the instant it actually carries, not on the shape that instant is stored in.
/// </summary>
/// <remarks>
/// <para>
/// The store's instants were changed to be stored as BSON dates, which is what makes the claim
/// predicate expressible on the server's own clock. The driver's default shape for a
/// <see cref="DateTimeOffset"/> is a <c>{ DateTime, Ticks, Offset }</c> sub-document, so every message
/// already staged when a consumer upgrades is in the other shape — and BSON's canonical type ordering
/// puts every sub-document BELOW every date. Under the aggregation comparison the claim predicate uses,
/// <c>leasedAt &lt; $$NOW − leaseTimeout</c> is therefore true for such a message at every instant,
/// forever, so a dispatcher on the new version is handed a message a dispatcher on the old version is
/// still delivering, and re-claims it on every poll. That is an ordinary rolling upgrade: no crash, no
/// pause, no elapsed time, and a duplicate window bounded by neither the lease timeout nor the retry
/// floor.
/// </para>
/// <para>
/// <b>Why the existing shape assertion is silent here.</b> The sibling arm that pins the stored lease to
/// a BSON date describes a document this version has just written. The population at risk is the one
/// written before the upgrade, which that arm never constructs — so it passes whether or not this
/// defect is present, and so does the rest of the suite.
/// </para>
/// <para>
/// <b>Fixture shape.</b> <c>LegacyOutboxDocument</c> is the pre-change document class: the same element
/// names with no representation attribute on the instants. Inserting through it makes the driver itself
/// produce the genuine legacy encoding rather than a hand-built approximation of it, so these arms bind
/// the shape a consumer's collection really holds. The staging helper asserts that encoding before any
/// arm runs, so a future driver default cannot quietly turn all of this vacuous.
/// </para>
/// <para>
/// <b>RED-on-pre-fix-code:</b> drop the two-shape reads from the store — restore the bare
/// <c>"$leasedAt"</c>/<c>"$scheduledAt"</c>/<c>"$nextAttemptAt"</c> field paths in the claim predicate,
/// and the plain <c>Lt</c>/<c>Lte</c> builder filters on the admin queries — and every SAFETY arm below
/// goes RED on the claim side while every LIVENESS arm goes RED on the admin side. The two fail in
/// opposite directions because query operators are type-bracketed and aggregation comparisons are not:
/// a legacy instant always matches an aggregation comparison and never matches a query one.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> BSON type ordering is the server's behaviour and it is the
/// whole subject here — a mocked collection would return whatever it was told and could not exhibit it.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes these NON-SKIPPED.
/// </para>
/// </remarks>
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxLegacyInstantShapeShould
{
	private const int LeaseSeconds = 300;
	private const string OutboxCollectionName = "outbox_messages";

	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	public MongoDbOutboxLegacyInstantShapeShould(MongoDbOutboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// The outbox document exactly as a previously published version declared it: no representation
	/// attribute on any instant, so the driver applies its default sub-document encoding.
	/// </summary>
	private sealed class LegacyOutboxDocument
	{
		[BsonId]
		[BsonRepresentation(BsonType.String)]
		public string Id { get; set; } = string.Empty;

		[BsonElement("messageType")]
		public string MessageType { get; set; } = string.Empty;

		[BsonElement("payload")]
		public byte[] Payload { get; set; } = [];

		[BsonElement("headers")]
		public Dictionary<string, object> Headers { get; set; } = new(StringComparer.Ordinal);

		[BsonElement("destination")]
		public string Destination { get; set; } = string.Empty;

		[BsonElement("createdAt")]
		public DateTimeOffset CreatedAt { get; set; }

		[BsonElement("scheduledAt")]
		public DateTimeOffset? ScheduledAt { get; set; }

		[BsonElement("sentAt")]
		public DateTimeOffset? SentAt { get; set; }

		[BsonElement("status")]
		public int Status { get; set; }

		[BsonElement("retryCount")]
		public int RetryCount { get; set; }

		[BsonElement("lastAttemptAt")]
		public DateTimeOffset? LastAttemptAt { get; set; }

		[BsonElement("nextAttemptAt")]
		public DateTimeOffset? NextAttemptAt { get; set; }

		[BsonElement("leasedAt")]
		public DateTimeOffset? LeasedAt { get; set; }

		[BsonElement("leasedBy")]
		public string? LeasedBy { get; set; }
	}

	private MongoDbOutboxStore NewStore(string processorId) =>
		new(
			Options.Create(new MongoDbOutboxOptions
			{
				ConnectionString = _fixture.ConnectionString,
				DatabaseName = _fixture.DatabaseName,
				ProcessorId = processorId,
				LeaseTimeoutSeconds = LeaseSeconds,
			}),
			NullLogger<MongoDbOutboxStore>.Instance);

	private IMongoDatabase Database() =>
		new MongoClient(_fixture.ConnectionString).GetDatabase(_fixture.DatabaseName);

	/// <summary>
	/// Stages one message the way the previous version would have, and asserts the encoding really is
	/// the legacy one — otherwise every arm below would be exercising the new shape and proving nothing.
	/// </summary>
	private async Task<string> StageLegacyAsync(Action<LegacyOutboxDocument> configure)
	{
		var document = new LegacyOutboxDocument
		{
			Id = Guid.NewGuid().ToString("N"),
			MessageType = "test.message",
			Payload = [1],
			Destination = "dest",
			CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			Status = (int)OutboxStatus.Staged,
		};

		configure(document);

		await Database()
			.GetCollection<LegacyOutboxDocument>(OutboxCollectionName)
			.InsertOneAsync(document)
			.ConfigureAwait(false);

		var raw = await Database()
			.GetCollection<BsonDocument>(OutboxCollectionName)
			.Find(new BsonDocument("_id", document.Id))
			.FirstAsync()
			.ConfigureAwait(false);

		raw["createdAt"].BsonType.ShouldBe(
			BsonType.Document,
			"these arms are about the encoding a previously published version produced; if the driver "
			+ "wrote a date here the fixture is no longer reproducing that population and every "
			+ "assertion below is vacuous");

		return document.Id;
	}

	private static async Task<IReadOnlyCollection<string>> ClaimAsync(MongoDbOutboxStore store) =>
		(await store.GetUnsentMessagesAsync(20, CancellationToken.None).ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

	/// <summary>
	/// SAFETY. A message left under a LIVE lease by the previous version is not handed to a dispatcher
	/// running this one. This is the arm the defect was about.
	/// </summary>
	[Fact]
	public async Task ALiveLeaseInTheLegacyShape_IsNotStolenByThisVersion()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a rolling upgrade duplicating every in-flight message is an at-most-once dispatch failure — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		// Stamped now, against a five-minute lease: unambiguously live, and the previous version's
		// dispatcher is still delivering it.
		var id = await StageLegacyAsync(d =>
		{
			d.LeasedAt = DateTimeOffset.UtcNow;
			d.LeasedBy = "previous-version-dispatcher";
		}).ConfigureAwait(false);

		var claimed = await ClaimAsync(NewStore("upgraded-B")).ConfigureAwait(false);

		claimed.ShouldNotContain(
			id,
			"a lease stored in the shape the previous version wrote is still a lease; taking it hands one "
			+ "message to two dispatchers at one instant, and re-takes it on every subsequent poll");
	}

	/// <summary>
	/// LIVENESS. A message whose legacy lease has genuinely elapsed is still reclaimable.
	/// </summary>
	/// <remarks>
	/// Without this arm the assertion above is satisfied by a store that refuses every legacy document —
	/// which would strand a consumer's entire pre-upgrade backlog while reading as perfectly safe.
	/// </remarks>
	[Fact]
	public async Task AnExpiredLeaseInTheLegacyShape_IsStillReclaimable()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the arm that separates a correct claim from one that strands the pre-upgrade backlog — never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var id = await StageLegacyAsync(d =>
		{
			d.LeasedAt = DateTimeOffset.UtcNow.AddSeconds(-(LeaseSeconds * 2));
			d.LeasedBy = "crashed-previous-version-dispatcher";
		}).ConfigureAwait(false);

		var claimed = await ClaimAsync(NewStore("upgraded-B")).ConfigureAwait(false);

		claimed.ShouldContain(
			id,
			"a dispatcher that died holding a legacy-shaped lease must not strand the message forever");
	}

	/// <summary>
	/// SAFETY and LIVENESS on the send-time gate, which the same type ordering makes due immediately.
	/// </summary>
	[Fact]
	public async Task ALegacyScheduledMessage_IsClaimedOnlyOnceItsSendTimeArrives()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var future = await StageLegacyAsync(d => d.ScheduledAt = DateTimeOffset.UtcNow.AddDays(30)).ConfigureAwait(false);
		var past = await StageLegacyAsync(d => d.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1)).ConfigureAwait(false);

		var claimed = await ClaimAsync(NewStore("upgraded-B")).ConfigureAwait(false);

		claimed.ShouldNotContain(future, "a message scheduled for next month is not due now");
		claimed.ShouldContain(past, "a message whose send time has passed is due");
	}

	/// <summary>
	/// SAFETY and LIVENESS on the failure-backoff floor, which the same type ordering makes elapsed.
	/// </summary>
	[Fact]
	public async Task ALegacyBackoffFloor_IsHonouredUntilItElapses()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var held = await StageLegacyAsync(d =>
		{
			d.Status = (int)OutboxStatus.Failed;
			d.RetryCount = 1;
			d.LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
			d.NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1);
		}).ConfigureAwait(false);

		var due = await StageLegacyAsync(d =>
		{
			d.Status = (int)OutboxStatus.Failed;
			d.RetryCount = 1;
			d.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-2);
			d.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
		}).ConfigureAwait(false);

		var claimed = await ClaimAsync(NewStore("upgraded-B")).ConfigureAwait(false);

		claimed.ShouldNotContain(held, "a retry floor an hour out has not elapsed");
		claimed.ShouldContain(due, "a retry floor that has passed no longer gates the message");
	}

	/// <summary>
	/// LIVENESS and SAFETY on retention. Query operators are type-bracketed, so the same durable format
	/// change hides a legacy instant from these queries rather than always matching it: the message is
	/// never cleaned up, and the TTL index declared over the same field does not expire it either, so it
	/// is retained indefinitely.
	/// </summary>
	[Fact]
	public async Task ALegacySentMessage_IsCleanedUpByAgeRatherThanRetainedForever()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var old = await StageLegacyAsync(d =>
		{
			d.Status = (int)OutboxStatus.Sent;
			d.SentAt = DateTimeOffset.UtcNow.AddDays(-30);
		}).ConfigureAwait(false);

		var recent = await StageLegacyAsync(d =>
		{
			d.Status = (int)OutboxStatus.Sent;
			d.SentAt = DateTimeOffset.UtcNow;
		}).ConfigureAwait(false);

		var deleted = await NewStore("upgraded-B")
			.CleanupAllTenantsSentMessagesAsync(DateTimeOffset.UtcNow.AddDays(-7), 100, CancellationToken.None)
			.ConfigureAwait(false);

		deleted.ShouldBe(1, "exactly the message older than the cutoff is removed");

		var surviving = await Database()
			.GetCollection<BsonDocument>(OutboxCollectionName)
			.Find(new BsonDocument())
			.Project(new BsonDocument("_id", 1))
			.ToListAsync()
			.ConfigureAwait(false);

		var survivingIds = surviving.ConvertAll(d => d["_id"].AsString);
		survivingIds.ShouldNotContain(old, "a legacy sent message past the retention cutoff is not retained forever");
		survivingIds.ShouldContain(recent, "a legacy sent message inside the retention window is not deleted early");
	}

	/// <summary>
	/// LIVENESS on the scheduled-message listing, hidden by the same type bracketing.
	/// </summary>
	[Fact]
	public async Task ALegacyScheduledMessage_IsVisibleToTheScheduledListing()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var due = await StageLegacyAsync(d => d.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1)).ConfigureAwait(false);
		var later = await StageLegacyAsync(d => d.ScheduledAt = DateTimeOffset.UtcNow.AddDays(30)).ConfigureAwait(false);

		var listed = (await NewStore("upgraded-B")
			.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None)
			.ConfigureAwait(false))
			.Select(m => m.Id)
			.ToList();

		listed.ShouldContain(due, "a legacy scheduled message that is due must be listed, not invisible");
		listed.ShouldNotContain(later, "a message scheduled for next month is not due yet");
	}

	/// <summary>
	/// A legacy message reads back through the current document class without error, so the claim above
	/// settles cleanly rather than leaving a message leased to a dispatcher that cannot deliver it.
	/// </summary>
	/// <remarks>
	/// Recorded as an arm rather than assumed. The representation attribute governs how an instant is
	/// WRITTEN, and the driver's serializer accepts either shape on read. Were that not so, a claim would
	/// land its lease write and then throw while materialising the result, leaving the message leased to
	/// a dispatcher that will never deliver it.
	/// </remarks>
	[Fact]
	public async Task ALegacyMessage_RoundTripsThroughTheCurrentDocumentClass()
	{
		_fixture.DockerAvailable.ShouldBeTrue("never skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var created = DateTimeOffset.UtcNow.AddMinutes(-5);
		var id = await StageLegacyAsync(d => d.CreatedAt = created).ConfigureAwait(false);

		var store = NewStore("upgraded-B");
		var claimed = (await store.GetUnsentMessagesAsync(20, CancellationToken.None).ConfigureAwait(false)).ToList();

		var message = claimed.SingleOrDefault(m => m.Id == id);
		message.ShouldNotBeNull("an unleased, unscheduled legacy message is claimable");
		message.CreatedAt.ToUnixTimeMilliseconds().ShouldBe(
			created.ToUnixTimeMilliseconds(),
			"the instant a legacy message carries survives being read through the current document class");

		await store.MarkSentAsync(id, CancellationToken.None).ConfigureAwait(false);
	}
}
