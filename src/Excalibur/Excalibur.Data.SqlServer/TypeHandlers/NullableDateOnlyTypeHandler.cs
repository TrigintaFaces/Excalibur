// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Handles database interactions for nullable <see cref="DateOnly" /> types with Dapper.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="NullableDateOnlyTypeHandler" /> class. </remarks>
/// <param name="formatProvider"> The format provider to use for parsing dates. Defaults to <see cref="CultureInfo.CurrentCulture" />. </param>
public sealed class NullableDateOnlyTypeHandler(IFormatProvider? formatProvider = null) : SqlMapper.TypeHandler<DateOnly?>
{
	private readonly IFormatProvider _formatProvider = formatProvider ?? CultureInfo.CurrentCulture;

	/// <inheritdoc />
	/// <summary>
	/// Parses a value from the database into a nullable <see cref="DateOnly" /> instance.
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
		_ => throw new InvalidOperationException($"Error during conversion of DateOnly: {value}"),
	};

	/// <summary>
	/// Sets a nullable <see cref="DateOnly" /> value on a database parameter.
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
