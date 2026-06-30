// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Real-infrastructure atomicity engage-test for <see cref="RedisInboxStore"/>'s
/// <see cref="IClaimableInboxStore.TryClaimAsync"/> claim-before-execute primitive against a live Redis container.
/// </summary>
/// <remarks>
/// N callers race the SAME (messageId, handlerType); exactly one claim must win (first-writer-wins via the provider's
/// atomic add), the rest see <see langword="false"/>. Determinism comes from the atomic primitive, not timing — no
/// sleep, no barrier — so the <c>== 1</c> assertion is non-vacuous. Never skipped: the collection fixture fails fast
/// when Docker is unavailable.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreClaimAtomicityShould
{
	private const int Concurrency = 16;
	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreClaimAtomicityShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_claim_when_concurrent_callers_race_the_same_message()
	{
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = $"inbox-claim-atomicity-{Guid.NewGuid():N}",
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance);

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
}
