using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.DataAccess.SqlServer.TypeHandlers;

/// <summary>
///     Type handler for mapping nullable <see cref="TimeOnly" /> values to SQL Server database columns using Dapper.
/// </summary>
public class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
{
	private readonly IFormatProvider _formatProvider;

	/// <summary>
	///     Initializes a new instance of the <see cref="NullableTimeOnlyTypeHandler" /> class.
	/// </summary>
	/// <param name="formatProvider">
	///     The format provider to use for parsing and formatting time values. Defaults to <see cref="CultureInfo.CurrentCulture" /> if not specified.
	/// </param>
	public NullableTimeOnlyTypeHandler(IFormatProvider? formatProvider = null)
	{
		_formatProvider = formatProvider ?? CultureInfo.CurrentCulture;
	}

	/// <inheritdoc />
	public override TimeOnly? Parse(object value) => value switch
	{
		null => null,
		TimeSpan ts => TimeOnly.FromTimeSpan(ts),
		DateTime dt => TimeOnly.FromDateTime(dt),
		string strValue => TimeOnly.FromDateTime(DateTime.Parse(strValue, _formatProvider)),
		_ => throw new InvalidOperationException($"Error during conversion of TimeOnly: {value}")
	};

	/// <inheritdoc />
	public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Time;
		parameter.Value = value.HasValue ? value.Value : DBNull.Value;
	}
}

/// <summary>
///     Type handler for mapping non-nullable <see cref="TimeOnly" /> values to SQL Server database columns using Dapper.
/// </summary>
public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
	private readonly IFormatProvider _formatProvider;

	/// <summary>
	///     Initializes a new instance of the <see cref="TimeOnlyTypeHandler" /> class.
	/// </summary>
	/// <param name="formatProvider">
	///     The format provider to use for parsing and formatting time values. Defaults to <see cref="CultureInfo.CurrentCulture" /> if not specified.
	/// </param>
	public TimeOnlyTypeHandler(IFormatProvider? formatProvider = null)
	{
		_formatProvider = formatProvider ?? CultureInfo.CurrentCulture;
	}

	/// <inheritdoc />
	public override TimeOnly Parse(object value) => value switch
	{
		TimeSpan ts => TimeOnly.FromTimeSpan(ts),
		DateTime dt => TimeOnly.FromDateTime(dt),
		string strValue => TimeOnly.FromDateTime(DateTime.Parse(strValue, _formatProvider)),
		_ => throw new InvalidOperationException($"Error during conversion of TimeOnly: {value}")
	};

	/// <inheritdoc />
	public override void SetValue(IDbDataParameter parameter, TimeOnly value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Time;
		parameter.Value = value;
	}
}
