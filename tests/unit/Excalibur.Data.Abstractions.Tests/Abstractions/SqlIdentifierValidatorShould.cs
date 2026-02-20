// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class SqlIdentifierValidatorShould
{
	[Theory]
	[InlineData("Users")]
	[InlineData("my_table")]
	[InlineData("Column1")]
	[InlineData("abc123")]
	[InlineData("_underscore")]
	[InlineData("A")]
	public void AcceptValidIdentifiers(string identifier)
	{
		// Act & Assert
		SqlIdentifierValidator.IsValid(identifier).ShouldBeTrue();
	}

	[Theory]
	[InlineData("Users")]
	[InlineData("my_table")]
	[InlineData("Column1")]
	public void NotThrowForValidIdentifiers(string identifier)
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SqlIdentifierValidator.ThrowIfInvalid(identifier, nameof(identifier)));
	}

	[Theory]
	[InlineData("table name")]
	[InlineData("drop;--")]
	[InlineData("Robert'); DROP TABLE Students;--")]
	[InlineData("table.name")]
	[InlineData("[brackets]")]
	[InlineData("name@domain")]
	[InlineData("select*")]
	[InlineData("col-name")]
	public void RejectInvalidIdentifiers(string identifier)
	{
		// Act & Assert
		SqlIdentifierValidator.IsValid(identifier).ShouldBeFalse();
	}

	[Theory]
	[InlineData("table name")]
	[InlineData("drop;--")]
	[InlineData("Robert'); DROP TABLE Students;--")]
	public void ThrowForInvalidIdentifiers(string identifier)
	{
		// Act & Assert
		var ex = Should.Throw<ArgumentException>(
			() => SqlIdentifierValidator.ThrowIfInvalid(identifier, "testParam"));
		ex.ParamName.ShouldBe("testParam");
		ex.Message.ShouldContain("SQL identifier contains invalid characters");
	}

	[Fact]
	public void RejectEmptyString()
	{
		SqlIdentifierValidator.IsValid(string.Empty).ShouldBeFalse();
	}
}
