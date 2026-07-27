// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Author≠impl real-infra concurrent-claimer regression lock for 6icxgg — <see cref="MongoDbOutboxStore"/>'s
/// <c>GetUnsentMessagesAsync</c> is an <b>atomic disjoint lease-claim</b> against real MongoDB: two
/// independent processors (distinct lease owners) draining the same staged set never claim the same message,
/// so a message is dispatched exactly once even under concurrent pollers. The InMemory sibling proves the
/// in-process contract; this proves it survives the real server-side atomic claim.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real MongoDB (TestContainers) so the claim is
/// evaluated by the server's own atomic find-and-update — a mocked <c>IMongoCollection</c> cannot reproduce
/// the concurrent claim semantics and would certify a non-atomic (double-claiming) store.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes it NON-SKIPPED. Two <b>separate</b> store instances share
/// one real collection = two distinct lease owners.
/// <para>
/// <b>RED-on-mutant:</b> revert the claim to a non-atomic read-then-lease (select unclaimed, then record the
/// lease in a second round-trip) ⇒ both processors select overlapping messages before either records its
/// lease ⇒ <see cref="TwoProcessors_PartitionTheStagedSet_WithNoDoubleClaim"/> observes overlap → RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxConcurrentClaimShould : IClassFixture<MongoDbOutboxStoreContainerFixture>
{
	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	public MongoDbOutboxConcurrentClaimShould(MongoDbOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private MongoDbOutboxStore NewStore()
	{
		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});
		return new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance);
	}

	[Fact]
	public async Task TwoProcessors_PartitionTheStagedSet_WithNoDoubleClaim()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"6icxgg atomic-claim is an exactly-once dispatch safety control — this real-Mongo lock must never be skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		// Stage a set larger than one batch so both pollers see eligible messages at once (a genuine race).
		var seeder = NewStore();
		const int total = 40;
		const int batchSize = 20;
		for (var i = 0; i < total; i++)
		{
			await seeder.StageMessageAsync(
				new OutboundMessage("test.message", [(byte)i], "dest"),
				CancellationToken.None).ConfigureAwait(false);
		}

		// Two independent processors (distinct lease owners) claim concurrently from the SAME collection.
		var processorA = NewStore();
		var processorB = NewStore();
		var claims = await Task.WhenAll(
			Task.Run(async () =>
				(await processorA.GetUnsentMessagesAsync(batchSize, CancellationToken.None).ConfigureAwait(false)).Select(m => m.Id).ToList()),
			Task.Run(async () =>
				(await processorB.GetUnsentMessagesAsync(batchSize, CancellationToken.None).ConfigureAwait(false)).Select(m => m.Id).ToList()))
			.ConfigureAwait(false);

		var a = claims[0];
		var b = claims[1];

		var overlap = a.Intersect(b, StringComparer.Ordinal).ToList();
		overlap.ShouldBeEmpty(
			$"concurrent lease-claims must be disjoint on real Mongo — {overlap.Count} id(s) were claimed by both processors (double-dispatch)");

		var union = a.Concat(b).ToList();
		union.Count.ShouldBe(
			union.Distinct(StringComparer.Ordinal).Count(),
			"no message id may appear twice across the two concurrent claims");
	}
}
