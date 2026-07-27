// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Wraps Dapper <see cref="DynamicParameters"/> so that, at execution time, the underlying
/// <see cref="OracleCommand"/> binds parameters <b>by name</b> rather than by position.
/// </summary>
/// <remarks>
/// Oracle's managed provider defaults to positional binding, which silently mis-binds any SQL that
/// references a bind variable more than once (e.g. a fenced compare-and-swap that reuses <c>:Token</c> or
/// <c>:ScopeKey</c> across the MERGE, the DUP_VAL_ON_INDEX fallback UPDATE, and the high-water read).
/// Setting <see cref="OracleCommand.BindByName"/> = <see langword="true"/> makes named reuse correct, so a
/// value can be supplied once under its name instead of once per positional occurrence — the invariant then
/// rests on the language rather than on a hand-maintained parameter-insertion order.
/// </remarks>
internal sealed class OracleDynamicParameters : SqlMapper.IDynamicParameters
{
	private readonly DynamicParameters _inner;

	public OracleDynamicParameters(DynamicParameters inner)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
	{
		if (command is OracleCommand oracleCommand)
		{
			oracleCommand.BindByName = true;
		}

		((SqlMapper.IDynamicParameters)_inner).AddParameters(command, identity);
	}
}
