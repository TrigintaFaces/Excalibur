using System.Data;

using Dapper;

using Excalibur.Domain.Model.ValueObjects;

using Microsoft.Data.SqlClient;

namespace Excalibur.DataAccess.SqlServer.TypeHandlers;

/// <summary>
///     Type handler for mapping the Money class to SQL Server database columns using Dapper. Handles non-nullable Money types.
/// </summary>
public class MoneyTypeHandler : SqlMapper.TypeHandler<Money>
{
	private readonly string _cultureName;
	private readonly byte _precision;
	private readonly byte _scale;

	/// <summary>
	///     Initializes a new instance of the <see cref="MoneyTypeHandler" /> class.
	/// </summary>
	/// <param name="cultureName"> The name of the culture to use for parsing and formatting (e.g., "en-US"). </param>
	/// <param name="precision"> The SQL precision for storing the decimal value. </param>
	/// <param name="scale"> The SQL scale for storing the decimal value. </param>
	/// <remarks>
	///     The default culture is "en-US", the precision is 19, and the scale is 4. Ensure these match the database column definitions to
	///     avoid truncation or errors.
	/// </remarks>
	public MoneyTypeHandler(string cultureName = "en-US", byte precision = 19, byte scale = 4)
	{
		_cultureName = cultureName;
		_precision = precision;
		_scale = scale;
	}

	/// <inheritdoc />
	public override Money Parse(object value)
	{
		if (value == null || value == DBNull.Value)
		{
			throw new DataException("Cannot convert null to Money.");
		}

		return value switch
		{
			decimal decimalValue => Money.From(decimalValue, _cultureName),
			double doubleValue => Money.From(doubleValue, _cultureName),
			float floatValue => Money.From(floatValue, _cultureName),
			int intValue => Money.From(intValue, _cultureName),
			long longValue => Money.From(longValue, _cultureName),
			string stringValue => Money.From(stringValue, _cultureName),
			_ => throw new DataException($"Cannot convert {value.GetType()} to Money.")
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
			sqlParameter.Precision = _precision;
			sqlParameter.Scale = _scale;
		}
		else
		{
			var precisionProperty = parameter.GetType().GetProperty("Precision");
			if (precisionProperty != null && precisionProperty.CanWrite)
			{
				precisionProperty.SetValue(parameter, _precision);
			}

			var scaleProperty = parameter.GetType().GetProperty("Scale");
			if (scaleProperty != null && scaleProperty.CanWrite)
			{
				scaleProperty.SetValue(parameter, _scale);
			}
		}
	}
}

/// <summary>
///     Type handler for mapping nullable Money types to SQL Server database columns using Dapper. Handles Money? (nullable Money) types.
/// </summary>
public class NullableMoneyTypeHandler : SqlMapper.TypeHandler<Money?>
{
	private readonly string _cultureName;
	private readonly byte _precision;
	private readonly byte _scale;

	/// <summary>
	///     Initializes a new instance of the <see cref="NullableMoneyTypeHandler" /> class.
	/// </summary>
	/// <param name="cultureName"> The name of the culture to use for parsing and formatting (e.g., "en-US"). </param>
	/// <param name="precision"> The SQL precision for storing the decimal value. </param>
	/// <param name="scale"> The SQL scale for storing the decimal value. </param>
	public NullableMoneyTypeHandler(string cultureName = "en-US", byte precision = 19, byte scale = 4)
	{
		_cultureName = cultureName;
		_precision = precision;
		_scale = scale;
	}

	/// <inheritdoc />
	public override Money? Parse(object? value)
	{
		if (value == null || value == DBNull.Value)
		{
			return null;
		}

		return value switch
		{
			decimal decimalValue => Money.From(decimalValue, _cultureName),
			double doubleValue => Money.From(doubleValue, _cultureName),
			float floatValue => Money.From(floatValue, _cultureName),
			int intValue => Money.From(intValue, _cultureName),
			long longValue => Money.From(longValue, _cultureName),
			string stringValue => Money.From(stringValue, _cultureName),
			_ => throw new DataException($"Cannot convert {value.GetType()} to Money.")
		};
	}

	/// <inheritdoc />
	public override void SetValue(IDbDataParameter parameter, Money? value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		if (value == null)
		{
			parameter.Value = DBNull.Value;
		}
		else
		{
			parameter.Value = value.Amount;
			parameter.DbType = DbType.Decimal;

			if (parameter is SqlParameter sqlParameter)
			{
				sqlParameter.Precision = _precision;
				sqlParameter.Scale = _scale;
			}
			else
			{
				var precisionProperty = parameter.GetType().GetProperty("Precision");
				if (precisionProperty != null && precisionProperty.CanWrite)
				{
					precisionProperty.SetValue(parameter, _precision);
				}

				var scaleProperty = parameter.GetType().GetProperty("Scale");
				if (scaleProperty != null && scaleProperty.CanWrite)
				{
					scaleProperty.SetValue(parameter, _scale);
				}
			}
		}
	}
}
