// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.LeaderElection;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new PostgresLeaderElectionOptions();
		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultLockKey()
	{
		var options = new PostgresLeaderElectionOptions();
		options.LockKey.ShouldBe(1L);
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		var options = new PostgresLeaderElectionOptions();
		options.CommandTimeoutSeconds.ShouldBe(5);
	}

	[Fact]
	public void AllowCustomConnectionString()
	{
		var options = new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=mydb;"
		};
		options.ConnectionString.ShouldBe("Host=localhost;Database=mydb;");
	}

	[Fact]
	public void AllowCustomLockKey()
	{
		var options = new PostgresLeaderElectionOptions { LockKey = 12345 };
		options.LockKey.ShouldBe(12345L);
	}

	[Fact]
	public void AllowCustomCommandTimeout()
	{
		var options = new PostgresLeaderElectionOptions { CommandTimeoutSeconds = 30 };
		options.CommandTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void Validate_ThrowsWhenConnectionStringIsEmpty()
	{
		var options = new PostgresLeaderElectionOptions();

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_ThrowsWhenConnectionStringIsWhitespace()
	{
		var options = new PostgresLeaderElectionOptions { ConnectionString = "   " };

		Should.Throw<InvalidOperationException>(() => options.Validate())
			.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void Validate_SucceedsWithValidConnectionString()
	{
		var options = new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=mydb;"
		};

		Should.NotThrow(() => options.Validate());
	}
}
