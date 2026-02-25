// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Tests.ServerlessAbstractions;

/// <summary>
/// Unit tests for ServerlessPlatform enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerlessPlatformShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		((int)ServerlessPlatform.Unknown).ShouldBe(0);
		((int)ServerlessPlatform.AwsLambda).ShouldBe(1);
		((int)ServerlessPlatform.AzureFunctions).ShouldBe(2);
		((int)ServerlessPlatform.GoogleCloudFunctions).ShouldBe(3);
	}

	[Theory]
	[InlineData(ServerlessPlatform.Unknown)]
	[InlineData(ServerlessPlatform.AwsLambda)]
	[InlineData(ServerlessPlatform.AzureFunctions)]
	[InlineData(ServerlessPlatform.GoogleCloudFunctions)]
	public void BeDefinedForAllValues(ServerlessPlatform platform)
	{
		// Act & Assert
		Enum.IsDefined(platform).ShouldBeTrue();
	}

	[Fact]
	public void Unknown_BeDefaultValue()
	{
		// Arrange & Act
		var defaultPlatform = default(ServerlessPlatform);

		// Assert
		defaultPlatform.ShouldBe(ServerlessPlatform.Unknown);
	}

	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ServerlessPlatform>();

		// Assert
		values.Length.ShouldBe(4);
		values.Distinct().Count().ShouldBe(4);
	}
}
