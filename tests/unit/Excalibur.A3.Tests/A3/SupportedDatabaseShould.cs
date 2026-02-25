// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for <see cref="SupportedDatabase"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class SupportedDatabaseShould : UnitTestBase
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<SupportedDatabase>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(SupportedDatabase.Unknown);
		values.ShouldContain(SupportedDatabase.Postgres);
		values.ShouldContain(SupportedDatabase.SqlServer);
	}

	[Fact]
	public void Unknown_HasExpectedValue()
	{
		// Assert
		((int)SupportedDatabase.Unknown).ShouldBe(0);
	}

	[Fact]
	public void Postgres_HasExpectedValue()
	{
		// Assert
		((int)SupportedDatabase.Postgres).ShouldBe(1);
	}

	[Fact]
	public void SqlServer_HasExpectedValue()
	{
		// Assert
		((int)SupportedDatabase.SqlServer).ShouldBe(2);
	}

	[Fact]
	public void Unknown_IsDefaultValue()
	{
		// Arrange
		SupportedDatabase defaultDb = default;

		// Assert
		defaultDb.ShouldBe(SupportedDatabase.Unknown);
	}

	[Theory]
	[InlineData(SupportedDatabase.Unknown)]
	[InlineData(SupportedDatabase.Postgres)]
	[InlineData(SupportedDatabase.SqlServer)]
	public void BeDefinedForAllValues(SupportedDatabase database)
	{
		// Assert
		Enum.IsDefined(database).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, SupportedDatabase.Unknown)]
	[InlineData(1, SupportedDatabase.Postgres)]
	[InlineData(2, SupportedDatabase.SqlServer)]
	public void CastFromInt_ReturnsCorrectValue(int value, SupportedDatabase expected)
	{
		// Act
		var database = (SupportedDatabase)value;

		// Assert
		database.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Unknown", SupportedDatabase.Unknown)]
	[InlineData("Postgres", SupportedDatabase.Postgres)]
	[InlineData("SqlServer", SupportedDatabase.SqlServer)]
	public void ParseFromString(string input, SupportedDatabase expected)
	{
		// Act
		var database = Enum.Parse<SupportedDatabase>(input);

		// Assert
		database.ShouldBe(expected);
	}
}
