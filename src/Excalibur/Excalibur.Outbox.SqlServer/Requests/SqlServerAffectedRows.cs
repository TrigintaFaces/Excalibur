// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>Explicit mutation outcome for the reviewed single-DML SQL Server commands.</summary>
internal static class SqlServerAffectedRows
{
	internal const string ParameterName = "__ExcaliburAffectedRows";
	internal const string Capture = "\nSET @__ExcaliburAffectedRows = @@ROWCOUNT;";

	// Append Capture immediately after the guarded DML in its declared SQL, before any other statement.
	// This is not a parser or adapter for arbitrary multi-statement batches/stored procedures.
	internal static void Declare(DynamicParameters parameters) =>
		parameters.Add(ParameterName, dbType: DbType.Int32, direction: ParameterDirection.Output);

	internal static void Bind(DbCommand command) =>
		StaticDbCommand.AddOutputParameter(command, ParameterName, DbType.Int32);

	internal static int Read(DbCommand command, DynamicParameters? observableParameters = null)
	{
		if (command.Parameters[ParameterName].Value is not int count || count < 0)
		{
			throw new InvalidDataException("The database did not return an authoritative nonnegative mutation count.");
		}

		observableParameters?.Add(ParameterName, count, DbType.Int32, ParameterDirection.Output);
		return count;
	}
}
