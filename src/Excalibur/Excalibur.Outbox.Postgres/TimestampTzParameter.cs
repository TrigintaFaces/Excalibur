// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Npgsql;

using NpgsqlTypes;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Binds a <see cref="DateTimeOffset"/> parameter with an explicit Npgsql <c>timestamptz</c>
/// (<see cref="NpgsqlDbType.TimestampTz"/>) type, bypassing Dapper's CLR-type-based <see cref="DbType"/>
/// inference.
/// </summary>
/// <remarks>
/// <para>
/// Dapper infers a parameter's <see cref="DbType"/> from the boxed CLR value. A <see cref="DateTime"/>
/// (even <see cref="DateTimeKind.Utc"/>) infers as <see cref="DbType.DateTime"/>, which Npgsql maps to
/// <c>timestamp</c> — <em>without</em> time zone. Npgsql then applies a session-timezone conversion at the
/// boundary, so a timestamp persisted into a <c>timestamptz</c> column comes back shifted by the machine's
/// local UTC offset. Converting only the value's <see cref="DateTime.Kind"/> does not change the inferred
/// <see cref="DbType"/>, so the instant is still corrupted.
/// </para>
/// <para>
/// Implementing <see cref="SqlMapper.ICustomQueryParameter"/> lets the caller add an
/// <see cref="NpgsqlParameter"/> with an explicit <see cref="NpgsqlDbType.TimestampTz"/> type. The
/// underlying <see cref="DateTimeOffset"/> is written as a true <c>timestamptz</c>, preserving the exact
/// instant across the write→reload round-trip regardless of the host machine's local time zone.
/// </para>
/// </remarks>
/// <param name="value">The instant to bind, or <see langword="null"/> to bind SQL <c>NULL</c>.</param>
internal sealed class TimestampTzParameter(DateTimeOffset? value) : SqlMapper.ICustomQueryParameter
{
	/// <inheritdoc />
	public void AddParameter(IDbCommand command, string name)
	{
		ArgumentNullException.ThrowIfNull(command);

		// Npgsql writes a DateTimeOffset to timestamptz only when its offset is zero; any other offset
		// (for example DateTimeOffset.Now on a machine east or west of UTC) is rejected outright rather
		// than converted. Normalising to UTC here preserves the exact instant while satisfying that
		// constraint, so a caller may supply an instant in any offset.
		var parameter = new NpgsqlParameter(name, NpgsqlDbType.TimestampTz)
		{
			Value = value.HasValue ? value.Value.ToUniversalTime() : DBNull.Value,
		};

		_ = command.Parameters.Add(parameter);
	}
}
