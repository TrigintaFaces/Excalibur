// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="RedisSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against a live Redis container.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract using TestContainers. They are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The store binds the default
/// <see cref="ConnectionMultiplexer"/> client surface a consumer would use.
/// </remarks>
[Collection(RedisSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Redis")]
public sealed class RedisSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<RedisSnapshotStoreContainerFixture>
{
	private readonly RedisSnapshotStoreContainerFixture _fixture;
	private ConnectionMultiplexer? _storeConnection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisSnapshotStoreConformanceShould(RedisSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available - real-infra conformance is never skipped.");

		// Bind the DEFAULT client surface (no admin, default options) the way a consumer would.
		_storeConnection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var options = Options.Create(new RedisSnapshotStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
		});

		var logger = NullLogger<RedisSnapshotStore>.Instance;

		// Ambient context, not the default null: the tenant-isolation arms use
		// TenantContextHolder.BeginScope, and FromContext(null) collapses every tenant to the
		// untenanted sentinel so they overwrite each other's snapshots.
		return new RedisSnapshotStore(_storeConnection, options, logger, new AmbientTenantContext());
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		if (_storeConnection is not null)
		{
			await _storeConnection.DisposeAsync().ConfigureAwait(false);
			_storeConnection = null;
		}

		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the tenant established by <see cref="TenantContextHolder.BeginScope"/>. The production
	/// equivalent is internal to Excalibur.Dispatch, so a directly-constructed store needs this here.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
