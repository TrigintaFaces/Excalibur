// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Outbox.SqlServer.Requests;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerAffectedRowsShould
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(42)]
	public void ReadTheNamedOutputAndUpdateTheSamePublicParameterBag(int count)
	{
		using var command = new SqlCommand();
		var parameters = new DynamicParameters();
		SqlServerAffectedRows.Declare(parameters);
		SqlServerAffectedRows.Bind(command);
		var output = command.Parameters[SqlServerAffectedRows.ParameterName];
		output.DbType.ShouldBe(DbType.Int32);
		output.Direction.ShouldBe(ParameterDirection.Output);
		output.Value = count;
		SqlServerAffectedRows.Read(command, parameters).ShouldBe(count);
		parameters.Get<int>(SqlServerAffectedRows.ParameterName).ShouldBe(count);
	}

	[Fact]
	public void RefuseMissingNegativeOrWrongRepresentationOutcomes()
	{
		using var command = new SqlCommand();
		SqlServerAffectedRows.Bind(command);
		foreach (var value in new object[] { DBNull.Value, -1, 1L, "1" })
		{
			command.Parameters[SqlServerAffectedRows.ParameterName].Value = value;
			_ = Should.Throw<InvalidDataException>(() => SqlServerAffectedRows.Read(command));
		}
	}
}
