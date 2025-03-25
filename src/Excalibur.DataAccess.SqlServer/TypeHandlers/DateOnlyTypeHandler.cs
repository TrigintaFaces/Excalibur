using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.DataAccess.SqlServer.TypeHandlers;

/// <summary>
///     Handles database interactions for nullable <see cref="DateOnly" /> types with Dapper.
/// </summary>
public class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
	private readonly IFormatProvider _formatProvider;

	/// <summary>
	///     Initializes a new instance of the <see cref="NullableDateOnlyTypeHandler" /> class.
	/// </summary>
	/// <param name="formatProvider"> The format provider to use for parsing dates. Defaults to <see cref="CultureInfo.CurrentCulture" />. </param>
	public NullableDateOnlyTypeHandler(IFormatProvider? formatProvider = null)
	{
		_formatProvider = formatProvider ?? CultureInfo.CurrentCulture;
	}

	/// <inheritdoc />
	/// <summary>
	///     Parses a value from the database into a nullable <see cref="DateOnly" /> instance.
	/// </summary>
	/// <param name="value"> The database value to parse. </param>
	/// <returns> A nullable <see cref="DateOnly" /> instance if the value is successfully converted; otherwise, <c> null </c>. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if the value cannot be converted into a <see cref="DateOnly" />. </exception>
	public override DateOnly? Parse(object value) => value switch
	{
		null => null,
		DateOnly dtOnly => dtOnly,
		DateTime dt => DateOnly.FromDateTime(dt),
		string strValue => string.IsNullOrEmpty(strValue)
			? null
			: DateOnly.FromDateTime(DateTime.Parse(strValue, _formatProvider)),
		_ => throw new InvalidOperationException($"Error during conversion of DateOnly: {value}")
	};

	/// <summary>
	///     Sets a nullable <see cref="DateOnly" /> value on a database parameter.
	/// </summary>
	/// <param name="parameter"> The database parameter to set. </param>
	/// <param name="value"> The nullable <see cref="DateOnly" /> value to set. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="parameter" /> is <c> null </c>. </exception>
	public override void SetValue(IDbDataParameter parameter, DateOnly? value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Date;

		switch (value)
		{
			case null:
				parameter.Value = DBNull.Value;
				break;

			default:
				{
					if (parameter.DbType == DbType.DateTime)
					{
						parameter.Value = value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
					}
					else
					{
						parameter.Value = value;
					}

					break;
				}
		}
	}
}

/// <summary>
///     Handles database interactions for non-nullable <see cref="DateOnly" /> types with Dapper.
/// </summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
	private readonly IFormatProvider _formatProvider;

	/// <summary>
	///     Initializes a new instance of the <see cref="DateOnlyTypeHandler" /> class.
	/// </summary>
	/// <param name="formatProvider"> The format provider to use for parsing dates. Defaults to <see cref="CultureInfo.CurrentCulture" />. </param>
	public DateOnlyTypeHandler(IFormatProvider? formatProvider = null)
	{
		_formatProvider = formatProvider ?? CultureInfo.CurrentCulture;
	}

	/// <summary>
	///     Parses a value from the database into a <see cref="DateOnly" /> instance.
	/// </summary>
	/// <param name="value"> The database value to parse. </param>
	/// <returns> A <see cref="DateOnly" /> instance. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if the value cannot be converted into a <see cref="DateOnly" />. </exception>
	public override DateOnly Parse(object value) => value switch
	{
		DateOnly dtOnly => dtOnly,
		DateTime dt => DateOnly.FromDateTime(dt),
		string strValue => string.IsNullOrEmpty(strValue)
			? DateOnly.MinValue
			: DateOnly.FromDateTime(DateTime.Parse(strValue, _formatProvider)),
		_ => throw new InvalidOperationException($"Error during conversion of DateOnly: {value}")
	};

	/// <summary>
	///     Sets a <see cref="DateOnly" /> value on a database parameter.
	/// </summary>
	/// <param name="parameter"> The database parameter to set. </param>
	/// <param name="value"> The <see cref="DateOnly" /> value to set. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="parameter" /> is <c> null </c>. </exception>
	public override void SetValue(IDbDataParameter parameter, DateOnly value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Date;

		parameter.Value = parameter.DbType switch
		{
			DbType.DateTime => value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
			_ => value
		};
	}
}
