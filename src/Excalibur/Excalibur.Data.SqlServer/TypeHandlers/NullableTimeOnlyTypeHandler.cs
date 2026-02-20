// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Type handler for mapping nullable <see cref="TimeOnly" /> values to SQL Server database columns using Dapper.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="NullableTimeOnlyTypeHandler" /> class. </remarks>
/// <param name="formatProvider">
/// The format provider to use for parsing and formatting time values. Defaults to <see cref="CultureInfo.CurrentCulture" /> if not specified.
/// </param>
public sealed class NullableTimeOnlyTypeHandler(IFormatProvider? formatProvider = null) : SqlMapper.TypeHandler<TimeOnly?>
{
	private readonly IFormatProvider _formatProvider = formatProvider ?? CultureInfo.CurrentCulture;

	/// <inheritdoc />
	public override TimeOnly? Parse(object value) => value switch
	{
		null => null,
		TimeSpan ts => TimeOnly.FromTimeSpan(ts),
		DateTime dt => TimeOnly.FromDateTime(dt),
		string strValue => TimeOnly.FromDateTime(DateTime.Parse(strValue, _formatProvider)),
		_ => throw new InvalidOperationException($"Error during conversion of TimeOnly: {value}"),
	};

	/// <inheritdoc />
	public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Time;
		parameter.Value = value.HasValue ? value.Value : DBNull.Value;
	}
}
