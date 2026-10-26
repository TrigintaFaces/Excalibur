// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Globalization;

using Dapper;

namespace Excalibur.Data.SqlServer.TypeHandlers;

/// <summary>
/// Handles database interactions for non-nullable <see cref="DateOnly" /> types with Dapper.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DateOnlyTypeHandler" /> class. </remarks>
/// <param name="formatProvider">
/// The format provider used to parse a value the provider hands over as a string. Defaults to
/// <see cref="CultureInfo.InvariantCulture" />, because a value read from a database has no culture:
/// the same stored string must mean the same thing on every host that reads it. Supply one only for a
/// legacy column whose text really is written in a specific culture's format.
/// </param>
public sealed class DateOnlyTypeHandler(IFormatProvider? formatProvider = null) : SqlMapper.TypeHandler<DateOnly>
{
	private readonly IFormatProvider _formatProvider = formatProvider ?? CultureInfo.InvariantCulture;

	/// <summary>
	/// Parses a value from the database into a <see cref="DateOnly" /> instance.
	/// </summary>
	/// <param name="value"> The database value to parse. </param>
	/// <returns> A <see cref="DateOnly" /> instance. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if the value cannot be converted into a <see cref="DateOnly" />. </exception>
	public override DateOnly Parse(object value) => value switch
	{
		DateOnly dtOnly => dtOnly,
		DateTime dt => DateOnly.FromDateTime(dt),
		string strValue => string.IsNullOrEmpty(strValue) ? DateOnly.MinValue : ParseDate(strValue),
		_ => throw new InvalidOperationException($"Error during conversion of DateOnly: {value}"),
	};

	/// <summary>Reads a date from its stored text, accepting a trailing time component.</summary>
	/// <param name="value">The stored text.</param>
	/// <returns>The date it denotes.</returns>
	/// <exception cref="InvalidOperationException">The text is not a date in any accepted form.</exception>
	/// <remarks>
	/// A date-only grammar is tried first; the fall-back accepts a stored timestamp, because a column
	/// typed as text frequently holds one and discarding its time is the documented behaviour. A value
	/// in neither form is refused by this contract's own exception rather than escaping as the
	/// <see cref="FormatException" /> that <c>Parse</c> would otherwise throw from inside Dapper.
	/// </remarks>
	private DateOnly ParseDate(string value) =>
		DateOnly.TryParse(value, _formatProvider, DateTimeStyles.None, out var date)
			? date
			: DateTime.TryParse(value, _formatProvider, DateTimeStyles.None, out var timestamp)
				? DateOnly.FromDateTime(timestamp)
				: throw new InvalidOperationException(
					$"Error during conversion of DateOnly: '{value}' is not a date in any accepted form.");

	/// <summary>
	/// Sets a <see cref="DateOnly" /> value on a database parameter.
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
			_ => value,
		};
	}
}
