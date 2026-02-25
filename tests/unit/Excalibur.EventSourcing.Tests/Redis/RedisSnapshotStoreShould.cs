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
public sealed class RedisSnapshotStoreShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var connection = CreateUninitializedConnection();
		var options = Options.Create(new RedisSnapshotStoreOptions { ConnectionString = "localhost:6379" });
		var logger = NullLogger<RedisSnapshotStore>.Instance;

		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(null!, options, logger))
			.ParamName.ShouldBe("connection");
		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(connection, null!, logger))
			.ParamName.ShouldBe("options");
		Should.Throw<ArgumentNullException>(() => new RedisSnapshotStore(connection, options, null!))
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
			Data = [1, 2, 3, 4],
			Metadata = new Dictionary<string, object> { ["tenant"] = "acme" }
		};

		var toHash = typeof(RedisSnapshotStore).GetMethod("ToHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		var fromHash = typeof(RedisSnapshotStore).GetMethod("FromHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		toHash.ShouldNotBeNull();
		fromHash.ShouldNotBeNull();

		var entries = (HashEntry[])toHash!.Invoke(null, [snapshot])!;
		entries.Length.ShouldBeGreaterThanOrEqualTo(6);
		entries.Any(e => e.Name == "metadata").ShouldBeTrue();

		var roundTripped = (ISnapshot)fromHash!.Invoke(null, [entries])!;
		roundTripped.SnapshotId.ShouldBe("snap-1");
		roundTripped.AggregateId.ShouldBe("agg-1");
		roundTripped.AggregateType.ShouldBe("Order");
		roundTripped.Version.ShouldBe(11);
		roundTripped.Data.ShouldBe([1, 2, 3, 4]);
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
			Data = [5, 6]
		};

		var toHash = typeof(RedisSnapshotStore).GetMethod("ToHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		var fromHash = typeof(RedisSnapshotStore).GetMethod("FromHashEntries", BindingFlags.NonPublic | BindingFlags.Static);
		toHash.ShouldNotBeNull();
		fromHash.ShouldNotBeNull();

		var entries = (HashEntry[])toHash!.Invoke(null, [snapshot])!;
		entries.Any(e => e.Name == "metadata").ShouldBeFalse();

		var roundTripped = (ISnapshot)fromHash!.Invoke(null, [entries])!;
		roundTripped.Metadata.ShouldBeNull();
		roundTripped.SnapshotId.ShouldBe("snap-2");
	}

	private static ConnectionMultiplexer CreateUninitializedConnection() =>
		(ConnectionMultiplexer)RuntimeHelpers.GetUninitializedObject(typeof(ConnectionMultiplexer));
}
