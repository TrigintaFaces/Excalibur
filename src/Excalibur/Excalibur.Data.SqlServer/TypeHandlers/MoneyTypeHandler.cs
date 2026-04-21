// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Domain.Model.ValueObjects;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Type handler for mapping the <see cref="Money"/> class to SQL Server database columns using Dapper.
/// Handles non-nullable <see cref="Money"/> types.
/// </summary>
/// <param name="currencyCode">The ISO 4217 currency code (e.g., "USD", "EUR"). Defaults to "USD".</param>
/// <param name="precision">The SQL precision for storing the decimal value.</param>
/// <param name="scale">The SQL scale for storing the decimal value.</param>
/// <remarks>
/// The default currency code is "USD", the precision is 19, and the scale is 4.
/// Ensure precision and scale match the database column definitions to avoid truncation or errors.
/// </remarks>
public sealed class MoneyTypeHandler(string currencyCode = "USD", byte precision = 19, byte scale = 4) : SqlMapper.TypeHandler<Money>
{
	/// <inheritdoc />
	public override Money Parse(object value)
	{
		if (value == null || value == DBNull.Value)
		{
			throw new DataException("Cannot convert null to Money.");
		}

		return value switch
		{
			decimal decimalValue => Money.From(decimalValue, currencyCode),
			double doubleValue => Money.From(doubleValue, currencyCode),
			float floatValue => Money.From(floatValue, currencyCode),
			int intValue => Money.From(intValue, currencyCode),
			long longValue => Money.From(longValue, currencyCode),
			string stringValue => Money.Parse(stringValue, currencyCode),
			_ => throw new DataException($"Cannot convert type {value.GetType()} to Money."),
		};
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMemberTypes",
		Justification = "GetProperty is used for IDbDataParameter implementations that may not be SqlParameter. The Precision/Scale properties are well-known ADO.NET conventions.")]
	public override void SetValue(IDbDataParameter parameter, Money? value)
	{
		ArgumentNullException.ThrowIfNull(parameter);
		ArgumentNullException.ThrowIfNull(value);

		parameter.Value = value.Amount;
		parameter.DbType = DbType.Decimal;

		if (parameter is SqlParameter sqlParameter)
		{
			sqlParameter.Precision = precision;
			sqlParameter.Scale = scale;
		}
		else
		{
			var precisionProperty = parameter.GetType().GetProperty("Precision");
			if (precisionProperty?.CanWrite == true)
			{
				precisionProperty.SetValue(parameter, precision);
			}

			var scaleProperty = parameter.GetType().GetProperty("Scale");
			if (scaleProperty?.CanWrite == true)
			{
				scaleProperty.SetValue(parameter, scale);
			}
		}
	}
}
