// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Author≠impl regression lock for bd-grjjz0 FR-1 (MS-A4): <see cref="RedisOutboxStore.StageMessageAsync"/>
/// must dedup concurrent stages of the SAME message id atomically — exactly one of N racing stagers wins.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity (RED on the pre-fix code): the pre-fix path did a non-atomic check-then-set across multiple
/// round-trips (<c>HashGet</c> existence check → <c>HashSet</c> → re-verify). Two stagers could both pass
/// the existence check before either wrote, so BOTH would succeed (a duplicate stage slips through) — more
/// than one success. The fix claims the id with a single atomic <c>HSETNX</c>
/// (<c>HashSetAsync(..., When.NotExists)</c>): exactly one stager's claim returns <see langword="true"/>,
/// every other gets <see langword="false"/> and throws — so the success count is exactly one and the staged
/// index holds exactly one message. This test fires many concurrent stages of one shared id through a single
/// store (shared connection + key prefix) so the claim is genuinely contended.
/// </para>
/// <para>Serial (-m:1, real Redis via TestContainers). Per-test isolation via a unique key prefix + id.</para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait(TraitNames.Component, TestComponents.Outbox)]
[Trait("Database", "Redis")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RedisOutboxConcurrentStageDedupShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisOutboxConcurrentStageDedupShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task AllowExactlyOneWinnerWhenConcurrentlyStagingTheSameMessageId()
	{
		// Arrange — one store (shared connection + key prefix) so every stager contends on the same key.
		await using var store = CreateOutboxStore();
		var sharedId = Guid.NewGuid().ToString();
		const int concurrency = 8;

		// Act — race `concurrency` stages of the SAME id.
		var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
		{
			var message = new OutboundMessage(
				"TestMessage",
				System.Text.Encoding.UTF8.GetBytes("{\"data\":\"x\"}"),
				"test-destination")
			{ Id = sharedId };

			try
			{
				await store.StageMessageAsync(message, TestCancellationToken);
				return true; // won the claim
			}
			catch (InvalidOperationException)
			{
				return false; // lost the atomic claim — already staged
			}
		}, TestCancellationToken));

		var results = await Task.WhenAll(tasks);

		// Assert — exactly one stager succeeded, and exactly one message is staged.
		results.Count(static won => won).ShouldBe(1, "exactly one concurrent stage of the same id must win");

		var stats = await store.GetStatisticsAsync(TestCancellationToken);
		stats.StagedMessageCount.ShouldBe(1, "the duplicate stages must not produce more than one staged message");
	}

	private RedisOutboxStore CreateOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			KeyPrefix = $"outbox-dedup-{Guid.NewGuid():N}",
			DatabaseId = 0,
			SentMessageTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		return new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance);
	}
}
