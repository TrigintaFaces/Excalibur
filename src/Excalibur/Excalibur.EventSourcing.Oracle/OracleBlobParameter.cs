// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// Binds a <see cref="byte"/> array parameter with an explicit <see cref="OracleDbType.Blob"/> type,
/// bypassing Dapper's CLR-type-based <see cref="DbType"/> inference.
/// </summary>
/// <remarks>
/// <para>
/// Dapper infers a parameter's <see cref="DbType"/> from the boxed CLR value, and a <see cref="byte"/>
/// array infers as <see cref="DbType.Binary"/>. ODP.NET maps <see cref="DbType.Binary"/> to
/// <c>RAW</c>, which a SQL statement caps at <b>2000 bytes</b>. A payload above that limit is rejected
/// outright — <c>ORA-01460</c> or <c>ORA-12899</c> — rather than truncated, so the write fails at
/// runtime even though the target column is declared <c>BLOB</c> and is perfectly capable of holding it.
/// </para>
/// <para>
/// The failure is size-dependent, which is what makes it dangerous: small payloads succeed, so the
/// binding looks correct until a real aggregate produces a snapshot above the limit. Declaring the
/// column <c>BLOB</c> does not help, because the constraint is imposed by the <em>parameter</em> type
/// on the client side, not by the column.
/// </para>
/// <para>
/// Implementing <see cref="SqlMapper.ICustomQueryParameter"/> lets the caller add an
/// <see cref="OracleParameter"/> carrying an explicit <see cref="OracleDbType.Blob"/>, which streams
/// the full payload regardless of length.
/// </para>
/// </remarks>
/// <param name="value">The payload to bind, or <see langword="null"/> to bind SQL <c>NULL</c>.</param>
internal sealed class OracleBlobParameter(byte[]? value) : SqlMapper.ICustomQueryParameter
{
	/// <inheritdoc />
	public void AddParameter(IDbCommand command, string name)
	{
		ArgumentNullException.ThrowIfNull(command);

		var parameter = new OracleParameter(name, OracleDbType.Blob)
		{
			Value = (object?)value ?? DBNull.Value,
		};

		_ = command.Parameters.Add(parameter);
	}
}
