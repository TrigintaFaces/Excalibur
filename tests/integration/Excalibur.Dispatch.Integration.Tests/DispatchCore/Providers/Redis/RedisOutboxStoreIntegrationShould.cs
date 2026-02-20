// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Redis.Outbox;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using StackExchange.Redis;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Integration tests for <see cref="RedisOutboxStore"/> using TestContainers.
/// Tests real Redis database operations for outbox message persistence.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-mj93p: Redis Outbox Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the RedisOutboxStore implementation against a real Redis
/// instance using TestContainers. Tests cover stage, retrieve, mark sent, mark failed, and statistics.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Component", "Outbox")]
[Trait("Provider", "Redis")]
public sealed class RedisOutboxStoreIntegrationShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisOutboxStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="redisFixture">The Redis container fixture.</param>
	public RedisOutboxStoreIntegrationShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	/// <summary>
	/// Tests that a message can be staged in the outbox.
	/// </summary>
	[Fact]
	public async Task StageMessage()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateOutboxStore();
		var message = CreateTestOutboundMessage();

		// Act
		await store.StageMessageAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Assert
		var stats = await store.GetStatisticsAsync(TestCancellationToken).ConfigureAwait(true);
		stats.StagedMessageCount.ShouldBe(1);
	}

	/// <summary>
	/// Tests that unsent messages can be retrieved.
	/// </summary>
	[Fact]
	public async Task RetrieveUnsentMessages()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateOutboxStore();

		// Stage 3 messages
		for (var i = 0; i < 3; i++)
		{
			await store.StageMessageAsync(CreateTestOutboundMessage(), TestCancellationToken).ConfigureAwait(true);
		}

		// Act
		var messages = await store.GetUnsentMessagesAsync(10, TestCancellationToken).ConfigureAwait(true);

		// Assert
		messages.Count().ShouldBe(3);
	}

	/// <summary>
	/// Tests that a message can be marked as sent.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsSent()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateOutboxStore();
		var message = CreateTestOutboundMessage();

		await store.StageMessageAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkSentAsync(message.Id, TestCancellationToken).ConfigureAwait(true);

		// Assert
		var stats = await store.GetStatisticsAsync(TestCancellationToken).ConfigureAwait(true);
		stats.StagedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(1);
	}

	/// <summary>
	/// Tests that a message can be marked as failed.
	/// </summary>
	[Fact]
	public async Task MarkMessageAsFailed()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateOutboxStore();
		var message = CreateTestOutboundMessage();

		await store.StageMessageAsync(message, TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkFailedAsync(message.Id, "Test failure", 1, TestCancellationToken).ConfigureAwait(true);

		// Assert
		var stats = await store.GetStatisticsAsync(TestCancellationToken).ConfigureAwait(true);
		stats.StagedMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(1);

		var failedMessages = await store.GetFailedMessagesAsync(3, null, 10, TestCancellationToken).ConfigureAwait(true);
		failedMessages.Count().ShouldBe(1);
		failedMessages.First().LastError.ShouldBe("Test failure");
		failedMessages.First().RetryCount.ShouldBe(1);
	}

	/// <summary>
	/// Tests that statistics are accurate.
	/// </summary>
	[Fact]
	public async Task RetrieveAccurateStatistics()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateOutboxStore();

		// Stage some messages
		var stagedMessages = new List<OutboundMessage>();
		for (var i = 0; i < 5; i++)
		{
			var msg = CreateTestOutboundMessage();
			await store.StageMessageAsync(msg, TestCancellationToken).ConfigureAwait(true);
			stagedMessages.Add(msg);
		}

		// Send 2 messages
		await store.MarkSentAsync(stagedMessages[0].Id, TestCancellationToken).ConfigureAwait(true);
		await store.MarkSentAsync(stagedMessages[1].Id, TestCancellationToken).ConfigureAwait(true);

		// Fail 1 message
		await store.MarkFailedAsync(stagedMessages[2].Id, "Test error", 1, TestCancellationToken).ConfigureAwait(true);

		// Act
		var stats = await store.GetStatisticsAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		stats.StagedMessageCount.ShouldBe(2);
		stats.SentMessageCount.ShouldBe(2);
		stats.FailedMessageCount.ShouldBe(1);
		stats.CapturedAt.ShouldNotBe(default); // CapturedAt is a struct, use default comparison
	}

	private static OutboundMessage CreateTestOutboundMessage()
	{
		return new OutboundMessage(
			"TestMessage",
			System.Text.Encoding.UTF8.GetBytes("{\"data\": \"test payload\"}"),
			"test-destination");
	}

	private RedisOutboxStore CreateOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			KeyPrefix = $"outbox-test-{Guid.NewGuid():N}",
			DatabaseId = 0,
			SentMessageTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});
		var logger = NullLogger<RedisOutboxStore>.Instance;

		var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		return new RedisOutboxStore(connection, options, logger);
	}

	private async Task CleanupRedisAsync()
	{
		// Connect with AllowAdmin for FLUSHDB command
		var options = ConfigurationOptions.Parse(_redisFixture.ConnectionString);
		options.AllowAdmin = true;
		var connection = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(true);
		var server = connection.GetServers().First();
		await server.FlushDatabaseAsync(0).ConfigureAwait(true);
		await connection.CloseAsync().ConfigureAwait(true);
		connection.Dispose();
	}
}
