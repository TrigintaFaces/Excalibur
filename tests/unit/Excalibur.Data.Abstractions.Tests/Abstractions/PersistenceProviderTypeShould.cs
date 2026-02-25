// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="PersistenceProviderType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class PersistenceProviderTypeShould : UnitTestBase
{
	[Fact]
	public void HaveSevenProviderTypes()
	{
		// Act
		var values = Enum.GetValues<PersistenceProviderType>();

		// Assert
		values.Length.ShouldBe(7);
	}

	[Fact]
	public void HaveSqlServerAsDefault()
	{
		// Assert
		PersistenceProviderType defaultValue = default;
		defaultValue.ShouldBe(PersistenceProviderType.SqlServer);
	}

	[Theory]
	[InlineData(PersistenceProviderType.SqlServer, 0)]
	[InlineData(PersistenceProviderType.Postgres, 1)]
	[InlineData(PersistenceProviderType.MongoDB, 2)]
	[InlineData(PersistenceProviderType.Elasticsearch, 3)]
	[InlineData(PersistenceProviderType.Redis, 4)]
	[InlineData(PersistenceProviderType.InMemory, 5)]
	[InlineData(PersistenceProviderType.Custom, 6)]
	public void HaveCorrectUnderlyingValues(PersistenceProviderType providerType, int expectedValue)
	{
		// Assert
		((int)providerType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("SqlServer", PersistenceProviderType.SqlServer)]
	[InlineData("Postgres", PersistenceProviderType.Postgres)]
	[InlineData("MongoDB", PersistenceProviderType.MongoDB)]
	[InlineData("Elasticsearch", PersistenceProviderType.Elasticsearch)]
	[InlineData("Redis", PersistenceProviderType.Redis)]
	[InlineData("InMemory", PersistenceProviderType.InMemory)]
	[InlineData("Custom", PersistenceProviderType.Custom)]
	public void ParseFromString(string input, PersistenceProviderType expected)
	{
		// Act
		var result = Enum.Parse<PersistenceProviderType>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Act & Assert
		foreach (var providerType in Enum.GetValues<PersistenceProviderType>())
		{
			Enum.IsDefined(providerType).ShouldBeTrue();
		}
	}
}
