// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Dapper;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>Closed string-ID expansion for the two existing SQL Server bulk mutation templates.</summary>
internal static class SqlServerOutboxIdList
{
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Only the single @Ids marker in framework-owned bulk templates is replaced, with generated parameter names or a fixed empty-set expression; all caller values remain DbParameters.")]
	internal static void Bind(DbCommand command, IReadOnlyList<string> ids)
	{
		ArgumentNullException.ThrowIfNull(ids);
		const string marker = "@Ids";
		var markerAt = command.CommandText.IndexOf(marker, StringComparison.Ordinal);
		if (markerAt < 0 || command.CommandText.IndexOf(marker, markerAt + marker.Length, StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("A bulk outbox command must contain exactly one string-ID marker.");
		}

		var count = 0;
		string? last = null;
		foreach (var id in ids)
		{
			if (count == 0 && id is null)
			{
				throw new NotSupportedException("A bulk outbox ID list cannot start with null.");
			}

			count++;
			Add(command, "Ids" + count.ToString(CultureInfo.InvariantCulture), id, id is { Length: > 4000 } ? -1 : 4000);
			if (id is not null) { last = id; }
		}

		if (count == 0)
		{
			var empty = command.CreateParameter();
			empty.ParameterName = "Ids";
			empty.Value = DBNull.Value;
			_ = command.Parameters.Add(empty);
			command.CommandText = command.CommandText.Replace(marker, "(SELECT @Ids WHERE 1 = 0)", StringComparison.Ordinal);
			return;
		}

		if (SqlMapper.Settings.PadListExpansions && last is not null)
		{
			var factor = count switch { <= 5 => 1, <= 150 => 10, <= 750 => 50, <= 2000 => 100, <= 2070 => 10, <= 2100 => 1, _ => 200 };
			var padding = (factor - (count % factor)) % factor;
			for (var index = 0; index < padding; index++)
			{
				count++;
				Add(command, "Ids" + count.ToString(CultureInfo.InvariantCulture), last, 4000);
			}
		}

		var names = new StringBuilder("(");
		for (var index = 1; index <= count; index++)
		{
			if (index != 1) { _ = names.Append(','); }
			_ = names.Append("@Ids").Append(index.ToString(CultureInfo.InvariantCulture));
		}
		_ = names.Append(')');
		command.CommandText = command.CommandText.Replace(marker, names.ToString(), StringComparison.Ordinal);
	}

	private static void Add(DbCommand command, string name, string? value, int size)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.Size = size;
		parameter.Value = value is null ? DBNull.Value : value;
		parameter.DbType = DbType.String;
		_ = command.Parameters.Add(parameter);
	}
}
