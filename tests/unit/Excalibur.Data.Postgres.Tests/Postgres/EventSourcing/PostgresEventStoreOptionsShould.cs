// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.EventSourcing;

namespace Excalibur.Data.Tests.Postgres.EventSourcing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresEventStoreOptionsShould
{
	[Fact]
	public void HaveDefaultEventsTableName()
	{
		var options = new PostgresEventStoreOptions();

		options.EventsTableName.ShouldBe("event_store_events");
	}

	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresEventStoreOptions();

		options.SchemaName.ShouldBe("public");
	}
}
