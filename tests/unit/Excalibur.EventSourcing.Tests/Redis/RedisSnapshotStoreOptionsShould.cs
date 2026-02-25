// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
public sealed class RedisSnapshotStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		var options = new RedisSnapshotStoreOptions();

		options.ConnectionString.ShouldBe(string.Empty);
		options.KeyPrefix.ShouldBe("snap");
		options.SnapshotTtlSeconds.ShouldBe(0);
		options.DatabaseIndex.ShouldBe(-1);
	}

	[Fact]
	public void AllowOverridingConfiguration()
	{
		var options = new RedisSnapshotStoreOptions
		{
			ConnectionString = "localhost:6379",
			KeyPrefix = "snapshot",
			SnapshotTtlSeconds = 3600,
			DatabaseIndex = 2
		};

		options.ConnectionString.ShouldBe("localhost:6379");
		options.KeyPrefix.ShouldBe("snapshot");
		options.SnapshotTtlSeconds.ShouldBe(3600);
		options.DatabaseIndex.ShouldBe(2);
	}
}
