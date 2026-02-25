// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Redis.Inbox;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using StackExchange.Redis;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Integration tests for <see cref="RedisInboxStore" /> using TestContainers. Tests real Redis database operations for inbox message deduplication.
/// </summary>
/// <remarks>
/// <para> Sprint 177 - Provider Testing Epic Phase 3. bd-nihc3: Redis InboxStore Tests (5 tests). </para>
/// <para>
/// These tests verify the RedisInboxStore implementation against a real Redis instance using TestContainers. Tests cover create, mark
/// processed, deduplication, is processed check, and failed entry handling.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Component", "Inbox")]
[Trait("Provider", "Redis")]
public sealed class RedisInboxStoreIntegrationShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStoreIntegrationShould" /> class.
	/// </summary>
	/// <param name="redisFixture"> The Redis container fixture. </param>
	public RedisInboxStoreIntegrationShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	/// <summary>
	/// Tests that an inbox entry can be created.
	/// </summary>
	[Fact]
	public async Task CreateEntry()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateInboxStore();
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "TestHandler";
		var messageType = "TestMessage";
		var payload = System.Text.Encoding.UTF8.GetBytes("{\"data\": \"test\"}");
		var metadata = new Dictionary<string, object> { { "correlationId", "test-123" } };

		// Act
		var entry = await store.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, TestCancellationToken).ConfigureAwait(true);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);
		entry.HandlerType.ShouldBe(handlerType);
		entry.MessageType.ShouldBe(messageType);
		entry.Status.ShouldBe(InboxStatus.Received);
	}

	/// <summary>
	/// Tests that an entry can be marked as processed.
	/// </summary>
	[Fact]
	public async Task MarkEntryAsProcessed()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateInboxStore();
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "TestHandler";
		var payload = System.Text.Encoding.UTF8.GetBytes("{\"data\": \"test\"}");

		_ = await store.CreateEntryAsync(messageId, handlerType, "TestMessage", payload, new Dictionary<string, object>(), TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkProcessedAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		var isProcessed = await store.IsProcessedAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);
		isProcessed.ShouldBeTrue();

		var entry = await store.GetEntryAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processed);
		_ = entry.ProcessedAt.ShouldNotBeNull();
	}

	/// <summary>
	/// Tests that TryMarkAsProcessed provides deduplication (returns false for duplicates).
	/// </summary>
	[Fact]
	public async Task DetectDuplicateWithTryMarkAsProcessed()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateInboxStore();
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "TestHandler";

		// Act - First call should succeed
		var firstResult = await store.TryMarkAsProcessedAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);

		// Act - Second call should detect duplicate
		var secondResult = await store.TryMarkAsProcessedAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		firstResult.ShouldBeTrue();
		secondResult.ShouldBeFalse();

		// Verify entry exists and is processed
		var isProcessed = await store.IsProcessedAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);
		isProcessed.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that IsProcessed returns false for unknown entries.
	/// </summary>
	[Fact]
	public async Task ReturnFalseForUnknownEntry()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateInboxStore();
		var unknownMessageId = Guid.NewGuid().ToString();
		var handlerType = "TestHandler";

		// Act
		var isProcessed = await store.IsProcessedAsync(unknownMessageId, handlerType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		isProcessed.ShouldBeFalse();

		var entry = await store.GetEntryAsync(unknownMessageId, handlerType, TestCancellationToken).ConfigureAwait(true);
		entry.ShouldBeNull();
	}

	/// <summary>
	/// Tests that failed entries can be marked and retrieved.
	/// </summary>
	[Fact]
	public async Task HandleFailedEntries()
	{
		// Arrange
		await CleanupRedisAsync().ConfigureAwait(true);
		await using var store = CreateInboxStore();
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "TestHandler";
		var payload = System.Text.Encoding.UTF8.GetBytes("{\"data\": \"test\"}");

		_ = await store.CreateEntryAsync(messageId, handlerType, "TestMessage", payload, new Dictionary<string, object>(), TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkFailedAsync(messageId, handlerType, "Test failure reason", TestCancellationToken).ConfigureAwait(true);

		// Assert
		var entry = await store.GetEntryAsync(messageId, handlerType, TestCancellationToken).ConfigureAwait(true);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe("Test failure reason");
		entry.RetryCount.ShouldBe(1);

		// Verify failed entries can be retrieved
		var failedEntries = await store.GetFailedEntriesAsync(3, null, 10, TestCancellationToken).ConfigureAwait(true);
		failedEntries.Count().ShouldBe(1);
		failedEntries.First().MessageId.ShouldBe(messageId);
	}

	private RedisInboxStore CreateInboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisInboxOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			KeyPrefix = $"inbox-test-{Guid.NewGuid():N}",
			DatabaseId = 0,
			DefaultTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});
		var logger = NullLogger<RedisInboxStore>.Instance;

		var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		return new RedisInboxStore(connection, options, logger);
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
