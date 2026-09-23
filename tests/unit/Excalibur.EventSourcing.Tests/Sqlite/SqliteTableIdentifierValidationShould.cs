// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// <see cref="SqliteEventStore"/> and <see cref="SqliteSnapshotStore"/> interpolate their table name
/// directly into DDL and statement text (an identifier cannot be parameterized in SQL), so an unvalidated
/// value is a SQL-injection surface. Both constructors now allowlist-validate the table name before it
/// reaches <see cref="SqliteTableInitializer"/> or any query -- matching the fix already shipped for the
/// SqlServer/Postgres/Oracle outbox providers' table-name inputs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteTableIdentifierValidationShould
{
	private const string ConnectionString = "Data Source=:memory:";
	private const string InjectionPayload = "Events]; DROP TABLE Events; --";

	// SAFETY: a table name outside the allowlist (letters, digits, underscore) must never reach the
	// constructor field, because every downstream use interpolates it verbatim.
	[Fact]
	public void ThrowWhenEventStoreTableNameIsNotAnAllowlistedIdentifier()
	{
		var act = () => new SqliteEventStore(
			ConnectionString,
			NullLogger<SqliteEventStore>.Instance,
			TestTenantContext.SingleTenantDefault,
			Options.Create(new TenantContextOptions()),
			InjectionPayload);

		Should.Throw<ArgumentException>(act);
	}

	[Fact]
	public void ThrowWhenSnapshotStoreTableNameIsNotAnAllowlistedIdentifier()
	{
		var act = () => new SqliteSnapshotStore(
			ConnectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			TestTenantContext.SingleTenantDefault,
			Options.Create(new TenantContextOptions()),
			InjectionPayload);

		Should.Throw<ArgumentException>(act);
	}

	// LIVENESS: a validator that rejects every table name would also satisfy the safety arm above, so the
	// default and a normal custom identifier must both be accepted.
	[Theory]
	[InlineData("Events")]
	[InlineData("Custom_Events_Table")]
	public void ConstructEventStoreWhenTableNameIsAValidIdentifier(string table)
	{
		var act = () => new SqliteEventStore(
			ConnectionString,
			NullLogger<SqliteEventStore>.Instance,
			TestTenantContext.SingleTenantDefault,
			Options.Create(new TenantContextOptions()),
			table);

		Should.NotThrow(act);
	}

	[Theory]
	[InlineData("Snapshots")]
	[InlineData("Custom_Snapshots_Table")]
	public void ConstructSnapshotStoreWhenTableNameIsAValidIdentifier(string table)
	{
		var act = () => new SqliteSnapshotStore(
			ConnectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			TestTenantContext.SingleTenantDefault,
			Options.Create(new TenantContextOptions()),
			table);

		Should.NotThrow(act);
	}
}
