// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Conformance.Outbox;

namespace Excalibur.Integration.Tests.Redis.Outbox;

/// <summary>
/// Conformance tests for <see cref="RedisOutboxStore"/> using the Outbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the
/// IOutboxStore interface contract using Redis via TestContainers.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class RedisOutboxStoreConformanceShould : OutboxStoreConformanceTestBase
{
	private readonly RedisContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;
	private ConnectionMultiplexer? _floorConnection;
	private ConnectionMultiplexer? _foreignConnection;
	private string _keyPrefix = string.Empty;
	private string _floorKeyPrefix = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisOutboxStoreConformanceShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_keyPrefix = $"outbox-test-{Guid.NewGuid():N}";
		var connectionString = _fixture.ConnectionString;
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = connectionString,
			KeyPrefix = _keyPrefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		// Create connection for test cleanup
		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var logger = NullLogger<RedisOutboxStore>.Instance;
		var store = new RedisOutboxStore(_connection, options, logger);

		return store;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// wseau9 (SA seam ruling): opt real Redis into the universal re-claim-floor property arms. Uses the DEFAULT
	/// client (a real <see cref="ConnectionMultiplexer"/>) and the REAL system clock (default
	/// <see cref="System.TimeProvider"/>) so the base arms' real-time floor poll (F=1s) exercises the store's
	/// actual next-visible gate — a fake clock would deadlock the wall-clock poll. RED against pre-fix Redis,
	/// which moved a failed message to an index the claim never read → stranded (§1.5).
	/// </remarks>
	protected override async Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available - real-infra re-claim-floor conformance is never skipped.");

		_floorKeyPrefix = $"outbox-floor-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _floorKeyPrefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
			ProcessorId = "conformance-owner",
			FailureBackoffFloorSeconds = floorSeconds,
		});

		_floorConnection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		return new RedisOutboxStore(_floorConnection, options, NullLogger<RedisOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Reserve the message under a FOREIGN <c>ProcessorId</c> — a second store over the SAME key prefix whose
	/// owner token differs from the store that calls <c>MarkFailedAsync</c> — so the R2 ownership guard
	/// (<c>LeasedBy</c> null or <c>== ProcessorId</c>, enforced inside the mark-failed Lua) is actually exercised.
	/// </remarks>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		var foreignOptions = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _floorKeyPrefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
			ProcessorId = "conformance-foreign-leader",
		});

		_foreignConnection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var foreignStore = new RedisOutboxStore(_foreignConnection, foreignOptions, NullLogger<RedisOutboxStore>.Instance);
		var reserved = await foreignStore.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		return reserved.Any(m => m.Id == messageId);
	}

	/// <inheritdoc/>
	/// <summary>
	/// Pre-test reset: delete this suite's keys WITHOUT tearing down the multiplexer the store just
	/// opened. <see cref="CleanupAsync"/> closes and disposes connections, which is correct as teardown
	/// but fatal as setup -- running it before a test handed every arm a disposed multiplexer
	/// (ObjectDisposedException from SE.Redis on the first store call).
	/// </summary>
	/// <returns>A task that completes when this suite's keys have been deleted.</returns>
	protected override async Task ResetDataAsync()
	{
		if (_connection is null)
		{
			return;
		}

		var server = _connection.GetServer(_connection.GetEndPoints().First());
		var database = _connection.GetDatabase();

		await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
		{
			_ = await database.KeyDeleteAsync(key).ConfigureAwait(false);
		}
	}

	protected override async Task CleanupAsync()
	{
		// Clean up test keys
		if (_connection != null)
		{
			var server = _connection.GetServer(_connection.GetEndPoints().First());
			var database = _connection.GetDatabase();

			// Find and delete all test keys matching our prefix
			await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
			{
				_ = await database.KeyDeleteAsync(key).ConfigureAwait(false);
			}

			// Close connection after cleanup
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
			_connection = null;
		}

		// Close the auxiliary connections opened by the re-claim-floor / foreign-owner overrides.
		foreach (var auxiliary in new[] { _floorConnection, _foreignConnection })
		{
			if (auxiliary is not null)
			{
				await auxiliary.CloseAsync().ConfigureAwait(false);
				auxiliary.Dispose();
			}
		}

		_floorConnection = null;
		_foreignConnection = null;
	}

	/// <summary>
	/// Redis documented-pending conformance gap (tracked 03koal, fix scheduled S895): full multi-state statistics
	/// tracking. A required contract behaviour, NOT a capability-gate — skipped pending the S895 fix so mainline
	/// carries no committed-RED; every other provider still runs and must pass it.
	/// </summary>
	protected override System.Collections.Generic.IReadOnlyDictionary<string, string> PendingConformanceGaps =>
		new System.Collections.Generic.Dictionary<string, string>
		{
			[nameof(GetStatistics_TracksAllStates)] = "03koal",
		};
}
