// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure atomicity engage-test for <see cref="ElasticsearchInboxStore"/>'s
/// <see cref="IClaimableInboxStore.TryClaimAsync"/> claim-before-execute primitive against a live Elasticsearch container.
/// </summary>
/// <remarks>
/// N callers race the SAME (messageId, handlerType); exactly one claim must win (first-writer-wins via the provider's
/// create-if-absent/optimistic-concurrency primitive), the rest see <see langword="false"/>. Determinism comes from the
/// atomic primitive, not timing — so the <c>== 1</c> assertion is non-vacuous. Never skipped: a missing container fails fast.
/// </remarks>
[Collection(ElasticsearchInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Inbox")]
public sealed class ElasticsearchInboxStoreClaimAtomicityShould : IClassFixture<ElasticsearchInboxStoreContainerFixture>
{
	private const int Concurrency = 16;
	private readonly ElasticsearchInboxStoreContainerFixture _fixture;

	public ElasticsearchInboxStoreClaimAtomicityShould(ElasticsearchInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_claim_when_concurrent_callers_race_the_same_message()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Elasticsearch container must be available - real-infra atomicity is never skipped.");

		var options = Options.Create(new ElasticsearchInboxOptions
		{
			IndexName = _fixture.IndexName,
			RefreshPolicy = "wait_for",
		});
		var store = new ElasticsearchInboxStore(_fixture.Client, options, NullLogger<ElasticsearchInboxStore>.Instance);

		try
		{
			const string messageId = "msg-claim-atomicity";
			const string handlerType = "TestHandler";

			var tasks = Enumerable.Range(0, Concurrency)
				.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, CancellationToken.None).AsTask()))
				.ToArray();

			var results = await Task.WhenAll(tasks).ConfigureAwait(false);

			results.Count(claimed => claimed).ShouldBe(
				1,
				$"the atomic claim must admit exactly one of {Concurrency} concurrent callers; got [{string.Join(",", results)}]");

			(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeFalse(
				"a claim already held must be denied to a later caller");

			await store.ReleaseAsync(messageId, handlerType, CancellationToken.None);
			(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeTrue(
				"after release the message must be re-admitted on the real provider");
		}
		finally
		{
			await _fixture.DeleteIndexAsync().ConfigureAwait(false);
		}
	}
}
