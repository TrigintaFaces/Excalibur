// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class RedisEventStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		var options = new RedisEventStoreOptions();

		options.ConnectionString.ShouldBe(string.Empty);
		options.StreamKeyPrefix.ShouldBe("es");
		options.DatabaseIndex.ShouldBe(-1);
	}

	[Fact]
	public void AllowOverridingConfiguration()
	{
		var options = new RedisEventStoreOptions
		{
			ConnectionString = "localhost:6379",
			StreamKeyPrefix = "events",
			DatabaseIndex = 3
		};

		options.ConnectionString.ShouldBe("localhost:6379");
		options.StreamKeyPrefix.ShouldBe("events");
		options.DatabaseIndex.ShouldBe(3);
	}
}
