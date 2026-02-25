// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="SqlAuthenticationMethod"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Persistence")]
public sealed class SqlAuthenticationMethodShould : UnitTestBase
{
	[Fact]
	public void HaveNotSpecifiedAsDefaultValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.NotSpecified).ShouldBe(0);
	}

	[Fact]
	public void HaveSqlPasswordValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.SqlPassword).ShouldBe(1);
	}

	[Fact]
	public void HaveActiveDirectoryIntegratedValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryIntegrated).ShouldBe(2);
	}

	[Fact]
	public void HaveActiveDirectoryPasswordValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryPassword).ShouldBe(3);
	}

	[Fact]
	public void HaveActiveDirectoryInteractiveValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryInteractive).ShouldBe(4);
	}

	[Fact]
	public void HaveActiveDirectoryServicePrincipalValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryServicePrincipal).ShouldBe(5);
	}

	[Fact]
	public void HaveActiveDirectoryDeviceCodeFlowValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow).ShouldBe(6);
	}

	[Fact]
	public void HaveActiveDirectoryManagedIdentityValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryManagedIdentity).ShouldBe(7);
	}

	[Fact]
	public void HaveActiveDirectoryMSIValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryMSI).ShouldBe(8);
	}

	[Fact]
	public void HaveActiveDirectoryDefaultValue()
	{
		// Assert
		((int)SqlAuthenticationMethod.ActiveDirectoryDefault).ShouldBe(9);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<SqlAuthenticationMethod>().Length.ShouldBe(10);
	}

	[Theory]
	[InlineData("NotSpecified", SqlAuthenticationMethod.NotSpecified)]
	[InlineData("SqlPassword", SqlAuthenticationMethod.SqlPassword)]
	[InlineData("ActiveDirectoryIntegrated", SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
	[InlineData("ActiveDirectoryPassword", SqlAuthenticationMethod.ActiveDirectoryPassword)]
	[InlineData("ActiveDirectoryInteractive", SqlAuthenticationMethod.ActiveDirectoryInteractive)]
	[InlineData("ActiveDirectoryServicePrincipal", SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
	[InlineData("ActiveDirectoryDeviceCodeFlow", SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
	[InlineData("ActiveDirectoryManagedIdentity", SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
	[InlineData("ActiveDirectoryMSI", SqlAuthenticationMethod.ActiveDirectoryMSI)]
	[InlineData("ActiveDirectoryDefault", SqlAuthenticationMethod.ActiveDirectoryDefault)]
	public void ParseFromString(string name, SqlAuthenticationMethod expected)
	{
		// Act
		var result = Enum.Parse<SqlAuthenticationMethod>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(SqlAuthenticationMethod.NotSpecified).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.SqlPassword).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryIntegrated).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryPassword).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryInteractive).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryServicePrincipal).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryMSI).ShouldBeTrue();
		Enum.IsDefined(SqlAuthenticationMethod.ActiveDirectoryDefault).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToNotSpecified()
	{
		// Arrange & Act
		var defaultValue = default(SqlAuthenticationMethod);

		// Assert
		defaultValue.ShouldBe(SqlAuthenticationMethod.NotSpecified);
	}
}
