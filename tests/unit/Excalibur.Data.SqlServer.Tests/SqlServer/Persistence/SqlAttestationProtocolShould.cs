// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="SqlAttestationProtocol"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Persistence")]
public sealed class SqlAttestationProtocolShould : UnitTestBase
{
	[Fact]
	public void HaveNotSpecifiedAsDefaultValue()
	{
		// Assert
		((int)SqlAttestationProtocol.NotSpecified).ShouldBe(0);
	}

	[Fact]
	public void HaveAASValue()
	{
		// Assert
		((int)SqlAttestationProtocol.AAS).ShouldBe(1);
	}

	[Fact]
	public void HaveHGSValue()
	{
		// Assert
		((int)SqlAttestationProtocol.HGS).ShouldBe(2);
	}

	[Fact]
	public void HaveNoneValue()
	{
		// Assert
		((int)SqlAttestationProtocol.None).ShouldBe(3);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<SqlAttestationProtocol>().Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("NotSpecified", SqlAttestationProtocol.NotSpecified)]
	[InlineData("AAS", SqlAttestationProtocol.AAS)]
	[InlineData("HGS", SqlAttestationProtocol.HGS)]
	[InlineData("None", SqlAttestationProtocol.None)]
	public void ParseFromString(string name, SqlAttestationProtocol expected)
	{
		// Act
		var result = Enum.Parse<SqlAttestationProtocol>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(SqlAttestationProtocol.NotSpecified).ShouldBeTrue();
		Enum.IsDefined(SqlAttestationProtocol.AAS).ShouldBeTrue();
		Enum.IsDefined(SqlAttestationProtocol.HGS).ShouldBeTrue();
		Enum.IsDefined(SqlAttestationProtocol.None).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToNotSpecified()
	{
		// Arrange & Act
		var defaultValue = default(SqlAttestationProtocol);

		// Assert
		defaultValue.ShouldBe(SqlAttestationProtocol.NotSpecified);
	}
}
