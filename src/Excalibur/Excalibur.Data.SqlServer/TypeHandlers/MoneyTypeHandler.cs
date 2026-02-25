// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Domain.Model.ValueObjects;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Type handler for mapping the Money class to SQL Server database columns using Dapper. Handles non-nullable Money types.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MoneyTypeHandler" /> class. </remarks>
/// <param name="cultureName"> The name of the culture to use for parsing and formatting (e.g., "en-US"). </param>
/// <param name="precision"> The SQL precision for storing the decimal value. </param>
/// <param name="scale"> The SQL scale for storing the decimal value. </param>
/// <remarks>
/// The default culture is "en-US", the precision is 19, and the scale is 4. Ensure these match the database column definitions to avoid
/// truncation or errors.
/// </remarks>
public sealed class MoneyTypeHandler(string cultureName = "en-US", byte precision = 19, byte scale = 4) : SqlMapper.TypeHandler<Money>
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
			decimal decimalValue => Money.From(decimalValue, cultureName),
			double doubleValue => Money.From(doubleValue, cultureName),
			float floatValue => Money.From(floatValue, cultureName),
			int intValue => Money.From(intValue, cultureName),
			long longValue => Money.From(longValue, cultureName),
			string stringValue => Money.From(stringValue, cultureName),
			_ => throw new DataException($"Cannot convert type {value.GetType()} to Money."),
		};
	}

	/// <inheritdoc />
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
