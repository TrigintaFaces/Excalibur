// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure atomicity engage-test for <see cref="MongoDbInboxStore"/>'s
/// <see cref="IClaimableInboxStore.TryClaimAsync"/> claim-before-execute primitive against a live MongoDB container.
/// </summary>
/// <remarks>
/// A unit lock with a fake store proves the middleware admits one; only a real database proves the per-provider claim
/// primitive is itself atomic under genuine concurrency. N callers race the SAME (messageId, handlerType); exactly one
/// claim must win (first-writer-wins), the rest see <see langword="false"/>. Determinism comes from the provider's
/// atomic insert/unique-index, not timing — no sleep, no barrier — so the <c>== 1</c> assertion is non-vacuous (a racy
/// check-then-act would admit more than one). Never skipped: a missing Docker container fails fast.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "Inbox")]
public sealed class MongoDbInboxStoreClaimAtomicityShould : IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private const int Concurrency = 16;
	private readonly MongoDbInboxStoreContainerFixture _fixture;

	public MongoDbInboxStoreClaimAtomicityShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_claim_when_concurrent_callers_race_the_same_message()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra atomicity is never skipped.");

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});
		var store = new MongoDbInboxStore(options, NullLogger<MongoDbInboxStore>.Instance);

		const string messageId = "msg-claim-atomicity";
		const string handlerType = "TestHandler";

		// Race N concurrent claims of the SAME (messageId, handlerType).
		var tasks = Enumerable.Range(0, Concurrency)
			.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed).ShouldBe(
			1,
			$"the atomic claim must admit exactly one of {Concurrency} concurrent callers; got [{string.Join(",", results)}]");

		// A later claim on the now-held (non-terminal Processing) key is denied.
		(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeFalse(
			"a claim already held must be denied to a later caller");

		// Releasing the claim re-admits a redelivery (proven against the real provider).
		await store.ReleaseAsync(messageId, handlerType, CancellationToken.None);
		(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeTrue(
			"after release the message must be re-admitted on the real provider");

		await _fixture.CleanupAsync().ConfigureAwait(false);
	}
}
