// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="ServerlessPlatform" /> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerlessPlatformShould : UnitTestBase
{
	[Fact]
	public void Unknown_HasValueZero()
	{
		// Assert
		((int)ServerlessPlatform.Unknown).ShouldBe(0);
	}

	[Fact]
	public void AwsLambda_HasValueOne()
	{
		// Assert
		((int)ServerlessPlatform.AwsLambda).ShouldBe(1);
	}

	[Fact]
	public void AzureFunctions_HasValueTwo()
	{
		// Assert
		((int)ServerlessPlatform.AzureFunctions).ShouldBe(2);
	}

	[Fact]
	public void GoogleCloudFunctions_HasValueThree()
	{
		// Assert
		((int)ServerlessPlatform.GoogleCloudFunctions).ShouldBe(3);
	}

	[Fact]
	public void AllValues_AreDefined()
	{
		// Arrange
		var values = Enum.GetValues<ServerlessPlatform>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(ServerlessPlatform.Unknown);
		values.ShouldContain(ServerlessPlatform.AwsLambda);
		values.ShouldContain(ServerlessPlatform.AzureFunctions);
		values.ShouldContain(ServerlessPlatform.GoogleCloudFunctions);
	}
}
