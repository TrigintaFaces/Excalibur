// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Globalization;

namespace Excalibur.Data;

/// <summary>Closed primitive conversions for framework-owned database projections.</summary>
internal static class StaticDbValue
{
	internal static string? String(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
	internal static byte[]? Bytes(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : (byte[])reader.GetValue(ordinal);
	internal static int Int32(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 :
		reader.GetFieldType(ordinal) == typeof(int) ? reader.GetInt32(ordinal) : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
	internal static long Int64(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 :
		reader.GetFieldType(ordinal) == typeof(long) ? reader.GetInt64(ordinal) : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
	internal static long Int64(object? value) => value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
	internal static bool Boolean(DbDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) &&
		(reader.GetFieldType(ordinal) == typeof(bool) ? reader.GetBoolean(ordinal) : Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture));
	internal static DateTimeOffset? DateTimeOffset(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
}
