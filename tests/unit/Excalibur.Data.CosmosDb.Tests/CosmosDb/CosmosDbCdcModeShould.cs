// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Unit tests for <see cref="CosmosDbCdcMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbCdcModeShould : UnitTestBase
{
	[Fact]
	public void HaveTwoModes()
	{
		// Act
		var values = Enum.GetValues<CosmosDbCdcMode>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Fact]
	public void HaveLatestVersionAsDefault()
	{
		// Assert
		CosmosDbCdcMode defaultValue = default;
		defaultValue.ShouldBe(CosmosDbCdcMode.LatestVersion);
	}

	[Theory]
	[InlineData(CosmosDbCdcMode.LatestVersion, 0)]
	[InlineData(CosmosDbCdcMode.AllVersionsAndDeletes, 1)]
	public void HaveCorrectUnderlyingValues(CosmosDbCdcMode mode, int expectedValue)
	{
		// Assert
		((int)mode).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("LatestVersion", CosmosDbCdcMode.LatestVersion)]
	[InlineData("AllVersionsAndDeletes", CosmosDbCdcMode.AllVersionsAndDeletes)]
	public void ParseFromString(string input, CosmosDbCdcMode expected)
	{
		// Act
		var result = Enum.Parse<CosmosDbCdcMode>(input);

		// Assert
		result.ShouldBe(expected);
	}
}
