// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="SqlConnectionColumnEncryptionSetting"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Persistence")]
public sealed class SqlConnectionColumnEncryptionSettingShould : UnitTestBase
{
	[Fact]
	public void HaveDisabledAsDefaultValue()
	{
		// Assert
		((int)SqlConnectionColumnEncryptionSetting.Disabled).ShouldBe(0);
	}

	[Fact]
	public void HaveEnabledValue()
	{
		// Assert
		((int)SqlConnectionColumnEncryptionSetting.Enabled).ShouldBe(1);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<SqlConnectionColumnEncryptionSetting>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData("Disabled", SqlConnectionColumnEncryptionSetting.Disabled)]
	[InlineData("Enabled", SqlConnectionColumnEncryptionSetting.Enabled)]
	public void ParseFromString(string name, SqlConnectionColumnEncryptionSetting expected)
	{
		// Act
		var result = Enum.Parse<SqlConnectionColumnEncryptionSetting>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(SqlConnectionColumnEncryptionSetting.Disabled).ShouldBeTrue();
		Enum.IsDefined(SqlConnectionColumnEncryptionSetting.Enabled).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToDisabled()
	{
		// Arrange & Act
		var defaultValue = default(SqlConnectionColumnEncryptionSetting);

		// Assert
		defaultValue.ShouldBe(SqlConnectionColumnEncryptionSetting.Disabled);
	}
}
