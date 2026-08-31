// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class RedisSnapshotStoreShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var connection = CreateUninitializedConnection();
		var options = Options.Create(new RedisSnapshotStoreOptions { ConnectionString = "localhost:6379" });
		var logger = NullLogger<RedisSnapshotStore>.Instance;
		var tenantContext = TestTenantContext.SingleTenantDefault;

		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(null!, options, logger, tenantContext))
			.ParamName.ShouldBe("connection");
		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(connection, null!, logger, tenantContext))
			.ParamName.ShouldBe("options");
		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(connection, options, null!, tenantContext))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public async Task ValidateMethodGuardsBeforeRedisAccess()
	{
		var sut = (RedisSnapshotStore)RuntimeHelpers.GetUninitializedObject(typeof(RedisSnapshotStore));

		await Should.ThrowAsync<ArgumentException>(() => sut.GetLatestSnapshotAsync("", "Order", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.GetLatestSnapshotAsync("agg-1", " ", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentNullException>(() => sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.DeleteSnapshotsAsync("", "Order", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.DeleteSnapshotsAsync("agg-1", " ", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.DeleteSnapshotsOlderThanAsync("", "Order", 10, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.DeleteSnapshotsOlderThanAsync("agg-1", " ", 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public void ConvertSnapshotToHashAndBack()
	{
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-1",
			AggregateId = "agg-1",
			AggregateType = "Order",
			Version = 11,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new byte[] { 1, 2, 3, 4 },
			Metadata = new Dictionary<string, object> { ["tenant"] = "acme" }
		};

		var toHash = typeof(RedisSnapshotStore).GetMethod("ToHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		var fromHash = typeof(RedisSnapshotStore).GetMethod("FromHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		toHash.ShouldNotBeNull();
		fromHash.ShouldNotBeNull();

		// ToHashEntries is now (snapshot, tenantId) — a null tenant models a single-tenant host (e6t62k).
		var entries = (HashEntry[])toHash!.Invoke(null, [snapshot, (string?)null])!;
		entries.Length.ShouldBeGreaterThanOrEqualTo(6);
		entries.Any(e => e.Name == "metadata").ShouldBeTrue();

		var roundTripped = (ISnapshot)fromHash!.Invoke(null, [entries])!;
		roundTripped.SnapshotId.ShouldBe("snap-1");
		roundTripped.AggregateId.ShouldBe("agg-1");
		roundTripped.AggregateType.ShouldBe("Order");
		roundTripped.Version.ShouldBe(11);
		roundTripped.Data.ToArray().ShouldBe(new byte[] { 1, 2, 3, 4 });
		roundTripped.Metadata.ShouldNotBeNull();
		roundTripped.Metadata!.ShouldContainKey("tenant");
	}

	[Fact]
	public void ConvertSnapshotWithoutMetadata_ToHashAndBack()
	{
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-2",
			AggregateId = "agg-2",
			AggregateType = "Order",
			Version = 3,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new byte[] { 5, 6 }
		};

		var toHash = typeof(RedisSnapshotStore).GetMethod("ToHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		var fromHash = typeof(RedisSnapshotStore).GetMethod("FromHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		toHash.ShouldNotBeNull();
		fromHash.ShouldNotBeNull();

		// ToHashEntries is now (snapshot, tenantId) — a null tenant models a single-tenant host (e6t62k).
		var entries = (HashEntry[])toHash!.Invoke(null, [snapshot, (string?)null])!;
		entries.Any(e => e.Name == "metadata").ShouldBeFalse();

		var roundTripped = (ISnapshot)fromHash!.Invoke(null, [entries])!;
		roundTripped.Metadata.ShouldBeNull();
		roundTripped.SnapshotId.ShouldBe("snap-2");
	}

	[Fact]
	public void ConvertSnapshotWithTenant_EmitsTenantIdEntry_AndRoundTrips()
	{
		// STRENGTHEN (e6t62k): when a tenant is supplied, ToHashEntries persists it as a dedicated tenantId
		// hash entry, and FromHashEntries recovers it — so the snapshot key is tenant-scoped and two tenants
		// holding the same aggregate id never overwrite one another.
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-t",
			AggregateId = "agg-t",
			AggregateType = "Order",
			Version = 7,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new byte[] { 9, 8, 7 }
		};

		var toHash = typeof(RedisSnapshotStore).GetMethod("ToHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		var fromHash = typeof(RedisSnapshotStore).GetMethod("FromHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		toHash.ShouldNotBeNull();
		fromHash.ShouldNotBeNull();

		var entries = (HashEntry[])toHash!.Invoke(null, [snapshot, "acme"])!;
		entries.Any(e => e.Name == "tenantId").ShouldBeTrue("A tenant-scoped snapshot must persist a tenantId hash entry.");

		var roundTripped = (ISnapshot)fromHash!.Invoke(null, [entries])!;
		roundTripped.SnapshotId.ShouldBe("snap-t");
		roundTripped.AggregateId.ShouldBe("agg-t");

		// LIVENESS: an unscoped conversion emits NO tenantId entry, so single-tenant keys keep their shape.
		var unscopedEntries = (HashEntry[])toHash.Invoke(null, [snapshot, (string?)null])!;
		unscopedEntries.Any(e => e.Name == "tenantId").ShouldBeFalse("A single-tenant snapshot must not persist a tenantId entry.");
	}

	private static ConnectionMultiplexer CreateUninitializedConnection() =>
		(ConnectionMultiplexer)RuntimeHelpers.GetUninitializedObject(typeof(ConnectionMultiplexer));
}
