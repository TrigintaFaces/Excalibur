// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Data.SqlServer.TypeHandlers;

namespace Excalibur.Data.Tests.SqlServer.TypeHandlers;

/// <summary>
/// Unit tests for <see cref="NullableDateOnlyTypeHandler"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "TypeHandlers")]
public sealed class NullableDateOnlyTypeHandlerShould : UnitTestBase
{
	private readonly NullableDateOnlyTypeHandler _sut = new();

	[Fact]
	public void Parse_ReturnNull_WhenValueIsNull()
	{
		// Act
		var result = _sut.Parse(null!);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Parse_ReturnDateOnly_WhenValueIsDateOnly()
	{
		// Arrange
		var expected = new DateOnly(2026, 2, 11);

		// Act
		var result = _sut.Parse(expected);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_ReturnDateOnly_WhenValueIsDateTime()
	{
		// Arrange
		var dateTime = new DateTime(2026, 2, 11, 14, 30, 0);

		// Act
		var result = _sut.Parse(dateTime);

		// Assert
		result.ShouldBe(new DateOnly(2026, 2, 11));
	}

	[Fact]
	public void Parse_ReturnDateOnly_WhenValueIsValidString()
	{
		// Arrange
		var handler = new NullableDateOnlyTypeHandler(CultureInfo.InvariantCulture);

		// Act
		var result = handler.Parse("02/11/2026");

		// Assert
		result.ShouldBe(new DateOnly(2026, 2, 11));
	}

	[Fact]
	public void Parse_ReturnNull_WhenValueIsEmptyString()
	{
		// Act
		var result = _sut.Parse(string.Empty);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Parse_ThrowInvalidOperationException_WhenValueIsUnsupportedType()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _sut.Parse(42));
	}

	[Fact]
	public void SetValue_SetDbNull_WhenValueIsNull()
	{
		// Arrange
		var parameter = A.Fake<IDbDataParameter>();

		// Act
		_sut.SetValue(parameter, null);

		// Assert
		parameter.DbType.ShouldBe(DbType.Date);
		parameter.Value.ShouldBe(DBNull.Value);
	}

	[Fact]
	public void SetValue_SetDateOnlyValue_WhenValueIsNotNull()
	{
		// Arrange
		var parameter = A.Fake<IDbDataParameter>();
		var dateOnly = new DateOnly(2026, 2, 11);

		// Act
		_sut.SetValue(parameter, dateOnly);

		// Assert
		parameter.DbType.ShouldBe(DbType.Date);
		parameter.Value.ShouldBe((DateOnly?)dateOnly);
	}

	[Fact]
	public void SetValue_ThrowArgumentNullException_WhenParameterIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.SetValue(null!, new DateOnly(2026, 1, 1)));
	}

	[Fact]
	public void Parse_UseCustomFormatProvider()
	{
		// Arrange
		var handler = new NullableDateOnlyTypeHandler(CultureInfo.GetCultureInfo("de-DE"));

		// Act
		var result = handler.Parse("11.02.2026");

		// Assert
		result.ShouldBe(new DateOnly(2026, 2, 11));
	}
}
