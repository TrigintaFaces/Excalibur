// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.ErrorHandling;

namespace Excalibur.Data.Tests.Postgres.ErrorHandling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresDeadLetterOptionsShould
{
	[Fact]
	public void HaveDefaultConnectionString()
	{
		var options = new PostgresDeadLetterOptions();

		options.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresDeadLetterOptions();

		options.SchemaName.ShouldBe("public");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		var options = new PostgresDeadLetterOptions();

		options.TableName.ShouldBe("dead_letter_messages");
	}
}
