// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="ApplicationIntent"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Persistence")]
public sealed class ApplicationIntentShould : UnitTestBase
{
	[Fact]
	public void HaveReadWriteAsDefaultValue()
	{
		// Assert
		((int)ApplicationIntent.ReadWrite).ShouldBe(0);
	}

	[Fact]
	public void HaveReadOnlyValue()
	{
		// Assert
		((int)ApplicationIntent.ReadOnly).ShouldBe(1);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<ApplicationIntent>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData("ReadWrite", ApplicationIntent.ReadWrite)]
	[InlineData("ReadOnly", ApplicationIntent.ReadOnly)]
	public void ParseFromString(string name, ApplicationIntent expected)
	{
		// Act
		var result = Enum.Parse<ApplicationIntent>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(ApplicationIntent.ReadWrite).ShouldBeTrue();
		Enum.IsDefined(ApplicationIntent.ReadOnly).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToReadWrite()
	{
		// Arrange & Act
		var defaultValue = default(ApplicationIntent);

		// Assert
		defaultValue.ShouldBe(ApplicationIntent.ReadWrite);
	}
}
