// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="DatabaseProvider"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class DatabaseProviderShould : UnitTestBase
{
	[Fact]
	public void HaveSqlServerAsZero()
	{
		// Assert
		((int)DatabaseProvider.SqlServer).ShouldBe(0);
	}

	[Fact]
	public void HavePostgresAsOne()
	{
		// Assert
		((int)DatabaseProvider.Postgres).ShouldBe(1);
	}

	[Fact]
	public void HaveMySqlAsTwo()
	{
		// Assert
		((int)DatabaseProvider.MySql).ShouldBe(2);
	}

	[Fact]
	public void HaveSqliteAsThree()
	{
		// Assert
		((int)DatabaseProvider.Sqlite).ShouldBe(3);
	}

	[Fact]
	public void HaveMongoDbAsFour()
	{
		// Assert
		((int)DatabaseProvider.MongoDb).ShouldBe(4);
	}

	[Fact]
	public void HaveRedisAsFive()
	{
		// Assert
		((int)DatabaseProvider.Redis).ShouldBe(5);
	}

	[Fact]
	public void HaveSixDefinedValues()
	{
		// Assert
		Enum.GetValues<DatabaseProvider>().Length.ShouldBe(6);
	}

	[Theory]
	[InlineData(DatabaseProvider.SqlServer, "SqlServer")]
	[InlineData(DatabaseProvider.Postgres, "Postgres")]
	[InlineData(DatabaseProvider.MySql, "MySql")]
	[InlineData(DatabaseProvider.Sqlite, "Sqlite")]
	[InlineData(DatabaseProvider.MongoDb, "MongoDb")]
	[InlineData(DatabaseProvider.Redis, "Redis")]
	public void HaveCorrectStringRepresentation(DatabaseProvider provider, string expected)
	{
		// Assert
		provider.ToString().ShouldBe(expected);
	}

	[Theory]
	[InlineData("SqlServer", DatabaseProvider.SqlServer)]
	[InlineData("Postgres", DatabaseProvider.Postgres)]
	[InlineData("MySql", DatabaseProvider.MySql)]
	[InlineData("Sqlite", DatabaseProvider.Sqlite)]
	[InlineData("MongoDb", DatabaseProvider.MongoDb)]
	[InlineData("Redis", DatabaseProvider.Redis)]
	public void ParseFromString(string value, DatabaseProvider expected)
	{
		// Act
		var parsed = Enum.Parse<DatabaseProvider>(value);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Fact]
	public void ParseCaseInsensitive()
	{
		// Act
		var parsed = Enum.Parse<DatabaseProvider>("sqlserver", ignoreCase: true);

		// Assert
		parsed.ShouldBe(DatabaseProvider.SqlServer);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		foreach (var value in Enum.GetValues<DatabaseProvider>())
		{
			Enum.IsDefined(value).ShouldBeTrue();
		}
	}

	[Fact]
	public void DefaultToSqlServer()
	{
		// Act
		var defaultValue = default(DatabaseProvider);

		// Assert
		defaultValue.ShouldBe(DatabaseProvider.SqlServer);
	}
}
