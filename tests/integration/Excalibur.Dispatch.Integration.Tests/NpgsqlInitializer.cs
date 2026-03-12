// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Runtime.CompilerServices;

using Dapper;

namespace Excalibur.Dispatch.Integration.Tests;

/// <summary>
/// Module initializer that configures Dapper type handling for Npgsql 9.x compatibility.
/// </summary>
/// <remarks>
/// <para>
/// Npgsql 9.x returns <see cref="DateTime"/> (UTC) for TIMESTAMPTZ columns instead of
/// <see cref="DateTimeOffset"/>. This breaks Dapper materialization of records like
/// <c>StoredEvent</c> which use <see cref="DateTimeOffset"/> parameters.
/// </para>
/// <para>
/// Registers a Dapper <see cref="SqlMapper.TypeHandler{T}"/> that converts
/// <see cref="DateTime"/> values to <see cref="DateTimeOffset"/> during materialization.
/// </para>
/// </remarks>
public static class NpgsqlInitializer
{
	[ModuleInitializer]
	public static void Initialize()
	{
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
		SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
	}

	/// <summary>
	/// Dapper type handler that converts DateTime (from Npgsql 9.x TIMESTAMPTZ) to DateTimeOffset.
	/// </summary>
	private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
	{
		public override DateTimeOffset Parse(object value)
		{
			return value switch
			{
				DateTimeOffset dto => dto,
				DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
				_ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTimeOffset"),
			};
		}

		public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
		{
			parameter.Value = value;
		}
	}
}
