// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.TypeHandlers;
using Excalibur.Domain.Model.ValueObjects;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.TypeHandlers;

/// <summary>
/// Unit tests for <see cref="NullableMoneyTypeHandler"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "TypeHandlers")]
public sealed class NullableMoneyTypeHandlerShould : UnitTestBase
{
	private readonly NullableMoneyTypeHandler _sut = new();

	[Fact]
	public void Parse_ReturnNull_WhenValueIsNull()
	{
		// Act
		var result = _sut.Parse(null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Parse_ReturnNull_WhenValueIsDBNull()
	{
		// Act
		var result = _sut.Parse(DBNull.Value);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsDecimal()
	{
		// Act
		var result = _sut.Parse(100.50m);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100.50m);
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsDouble()
	{
		// Act
		var result = _sut.Parse(100.50d);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100.50m);
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsFloat()
	{
		// Act
		var result = _sut.Parse(100.5f);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100.5m);
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsInt()
	{
		// Act
		var result = _sut.Parse(100);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100m);
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsLong()
	{
		// Act
		var result = _sut.Parse(100L);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100m);
	}

	[Fact]
	public void Parse_ReturnMoney_WhenValueIsValidString()
	{
		// Act
		var result = _sut.Parse("100.50");

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(100.50m);
	}

	[Fact]
	public void Parse_ThrowDataException_WhenValueIsUnsupportedType()
	{
		// Act & Assert
		Should.Throw<DataException>(() => _sut.Parse(true));
	}

	[Fact]
	public void Parse_UseCustomCulture()
	{
		// Arrange
		var handler = new NullableMoneyTypeHandler("fr-FR");

		// Act
		var result = handler.Parse(42.50m);

		// Assert
		result.ShouldNotBeNull();
		result.Amount.ShouldBe(42.50m);
		result.CultureName.ShouldBe("fr-FR");
	}

	[Fact]
	public void SetValue_SetDbNull_WhenValueIsNull()
	{
		// Arrange
		var parameter = A.Fake<IDbDataParameter>();

		// Act
		_sut.SetValue(parameter, null);

		// Assert
		parameter.Value.ShouldBe(DBNull.Value);
	}

	[Fact]
	public void SetValue_SetDecimalValueAndDbType_WhenValueIsNotNull()
	{
		// Arrange
		var parameter = A.Fake<IDbDataParameter>();
		var money = Money.USD(100.50m);

		// Act
		_sut.SetValue(parameter, money);

		// Assert
		parameter.Value.ShouldBe(100.50m);
		parameter.DbType.ShouldBe(DbType.Decimal);
	}

	[Fact]
	public void SetValue_SetPrecisionAndScale_WhenParameterIsSqlParameter()
	{
		// Arrange
		var parameter = new SqlParameter();
		var money = Money.USD(100.50m);

		// Act
		_sut.SetValue(parameter, money);

		// Assert
		parameter.Value.ShouldBe(100.50m);
		parameter.DbType.ShouldBe(DbType.Decimal);
		parameter.Precision.ShouldBe((byte)19);
		parameter.Scale.ShouldBe((byte)4);
	}

	[Fact]
	public void SetValue_UseCustomPrecisionAndScale()
	{
		// Arrange
		var handler = new NullableMoneyTypeHandler(precision: 10, scale: 2);
		var parameter = new SqlParameter();
		var money = Money.USD(50.25m);

		// Act
		handler.SetValue(parameter, money);

		// Assert
		parameter.Precision.ShouldBe((byte)10);
		parameter.Scale.ShouldBe((byte)2);
	}

	[Fact]
	public void SetValue_ThrowArgumentNullException_WhenParameterIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _sut.SetValue(null!, Money.USD(10m)));
	}
}
