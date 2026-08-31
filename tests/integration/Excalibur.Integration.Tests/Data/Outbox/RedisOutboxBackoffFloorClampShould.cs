// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxBackoffFloorClampShould"/> against a live Redis container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. Redis advertises the backoff capability, so the processor prefers the
/// path these arms drive.
/// </para>
/// <para>
/// On this store the gate is a score in the scheduled sorted set, and the floor is measured from the
/// injected <see cref="TimeProvider"/> rather than from Redis's own clock. That is deliberate: the plain
/// failure path writes that score the same way and the scheduled-to-staged sweep compares it against the
/// same clock, so composing here adds no clock the provider was not already trusting. Cross-dispatcher skew
/// remains, exactly as for every other timestamp this store writes.
/// </para>
/// <para>
/// Each arm gets its own key prefix so the two do not share state through the container, which is shared
/// across the collection.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Redis)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxBackoffFloorClampShould : OutboxBackoffFloorClampShould, IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _fixture;
	private readonly string _keyPrefix = $"outbox-floor-{Guid.NewGuid():N}";

	/// <summary>Initializes a new instance of the <see cref="RedisOutboxBackoffFloorClampShould"/> class.</summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisOutboxBackoffFloorClampShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available - the backoff floor lock is never skipped.");

		var options = MsOptions.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _keyPrefix,
			DatabaseId = 0,
			ProcessorId = "floor-clamp-processor",
			SentMessageTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		var connection = ConnectionMultiplexer.Connect(_fixture.ConnectionString);
		return Task.FromResult<IOutboxStore>(
			new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Each arm already runs under its own key prefix, so there is no shared state to clear between them.
	/// </remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;
}
