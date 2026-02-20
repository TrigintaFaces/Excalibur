// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Data.SqlServer.TypeHandlers;

namespace Excalibur.Data.Tests.SqlServer.TypeHandlers;

/// <summary>
/// Unit tests for <see cref="NullableTimeOnlyTypeHandler"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "TypeHandlers")]
public sealed class NullableTimeOnlyTypeHandlerShould : UnitTestBase
{
	private readonly NullableTimeOnlyTypeHandler _sut = new();

	[Fact]
	public void Parse_ReturnNull_WhenValueIsNull()
	{
		// Act
		var result = _sut.Parse(null!);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Parse_ReturnTimeOnly_WhenValueIsTimeSpan()
	{
		// Arrange
		var timeSpan = new TimeSpan(14, 30, 0);

		// Act
		var result = _sut.Parse(timeSpan);

		// Assert
		result.ShouldBe(new TimeOnly(14, 30, 0));
	}

	[Fact]
	public void Parse_ReturnTimeOnly_WhenValueIsDateTime()
	{
		// Arrange
		var dateTime = new DateTime(2026, 2, 11, 14, 30, 45);

		// Act
		var result = _sut.Parse(dateTime);

		// Assert
		result.ShouldBe(new TimeOnly(14, 30, 45));
	}

	[Fact]
	public void Parse_ReturnTimeOnly_WhenValueIsValidString()
	{
		// Arrange
		var handler = new NullableTimeOnlyTypeHandler(CultureInfo.InvariantCulture);

		// Act
		var result = handler.Parse("02:30:00 PM");

		// Assert
		result.ShouldBe(new TimeOnly(14, 30, 0));
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
		parameter.DbType.ShouldBe(DbType.Time);
		parameter.Value.ShouldBe(DBNull.Value);
	}

	[Fact]
	public void SetValue_SetTimeOnlyValue_WhenValueHasValue()
	{
		// Arrange
		var parameter = A.Fake<IDbDataParameter>();
		var timeOnly = new TimeOnly(14, 30, 0);

		// Act
		_sut.SetValue(parameter, timeOnly);

		// Assert
		parameter.DbType.ShouldBe(DbType.Time);
		parameter.Value.ShouldBe(timeOnly);
	}

	[Fact]
	public void SetValue_ThrowArgumentNullException_WhenParameterIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.SetValue(null!, new TimeOnly(14, 30, 0)));
	}

	[Fact]
	public void Parse_UseCustomFormatProvider()
	{
		// Arrange
		var handler = new NullableTimeOnlyTypeHandler(CultureInfo.GetCultureInfo("de-DE"));

		// Act
		var result = handler.Parse("14:30:00");

		// Assert
		result.ShouldBe(new TimeOnly(14, 30, 0));
	}
}
