// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Type handler for mapping non-nullable <see cref="TimeOnly" /> values to SQL Server database columns using Dapper.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimeOnlyTypeHandler" /> class. </remarks>
/// <param name="formatProvider">
/// The format provider used to parse a value the provider hands over as a string. Defaults to
/// <see cref="CultureInfo.InvariantCulture" />, because a value read from a database has no culture:
/// the same stored string must mean the same thing on every host that reads it. Supply one only for a
/// legacy column whose text really is written in a specific culture's format.
/// </param>
public sealed class TimeOnlyTypeHandler(IFormatProvider? formatProvider = null) : SqlMapper.TypeHandler<TimeOnly>
{
	private readonly IFormatProvider _formatProvider = formatProvider ?? CultureInfo.InvariantCulture;

	/// <inheritdoc />
	public override TimeOnly Parse(object value) => value switch
	{
		TimeSpan ts => TimeOnly.FromTimeSpan(ts),
		DateTime dt => TimeOnly.FromDateTime(dt),
		string strValue => ParseTime(strValue),
		_ => throw new InvalidOperationException($"Error during conversion of TimeOnly: {value}"),
	};

	/// <summary>Reads a time from its stored text, accepting a full timestamp.</summary>
	/// <param name="value">The stored text.</param>
	/// <returns>The time it denotes.</returns>
	/// <exception cref="InvalidOperationException">The text is not a time in any accepted form.</exception>
	/// <remarks>
	/// A time-only grammar is tried first; the fall-back accepts a stored timestamp and keeps its time
	/// of day. A value in neither form is refused by this contract's own exception rather than escaping
	/// as a <see cref="FormatException" /> from inside Dapper.
	/// </remarks>
	private TimeOnly ParseTime(string value) =>
		TimeOnly.TryParse(value, _formatProvider, DateTimeStyles.None, out var time)
			? time
			: DateTime.TryParse(value, _formatProvider, DateTimeStyles.None, out var timestamp)
				? TimeOnly.FromDateTime(timestamp)
				: throw new InvalidOperationException(
					$"Error during conversion of TimeOnly: '{value}' is not a time in any accepted form.");

	/// <inheritdoc />
	public override void SetValue(IDbDataParameter parameter, TimeOnly value)
	{
		ArgumentNullException.ThrowIfNull(parameter);

		parameter.DbType = DbType.Time;
		parameter.Value = value;
	}
}
