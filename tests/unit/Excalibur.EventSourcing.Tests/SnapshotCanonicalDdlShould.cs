// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

// rryq3n (S901 Lane A) — structural lock on the SHIPPED canonical snapshot DDL for Postgres and Oracle,
// mirroring PostgresInboxCanonicalDdlShould. The shipped Scripts/001_CreateSnapshotSchema.sql is code a
// consumer runs against their own database; nothing tested it (grep: 0 for CreateSnapshotSchema vs 4 for
// CreateInboxSchema). This binds the file that ships in the nupkg (csproj: <None Include="Scripts\*.sql"
// Pack="true" ...>) and FAILS RED — not skips — if the shipped DDL drifts from the columns the store writes.
//
// Also the regression lock for two shipped fixes: the Postgres `metadata` column (a3kqah — the store
// persists snapshot metadata; dropping it silently loses it) and the Oracle `DATA BLOB` (a7zf4r — RAW caps
// at 2000 bytes; BLOB does not). A future edit removing either reopens a data-fidelity defect.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotCanonicalDdlShould
{
	private static readonly string PostgresScriptPath = ResolveRepoRelative(
		"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql");

	private static readonly string OracleScriptPath = ResolveRepoRelative(
		"src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/001_CreateSnapshotSchema.sql");

	[Fact]
	public void Ship_every_Postgres_column_the_store_writes_so_a_consumer_running_it_can_save()
	{
		// LIVENESS — the shipped DDL must declare every column SaveSnapshotRequest's INSERT names, or a
		// consumer running it gets a missing-column error on the first save.
		var ddl = Normalize(File.ReadAllText(PostgresScriptPath));

		foreach (var column in new[] { "snapshot_id", "aggregate_id", "aggregate_type", "version", "data", "metadata", "created_at", "tenant_id" })
		{
			ddl.ShouldContain(column, Case.Insensitive,
				$"the shipped Postgres snapshot DDL must declare '{column}' — the store's INSERT names it.");
		}

		// a3kqah regression lock: metadata is a real persisted column, not RAW/absent.
		ddl.ShouldContain("metadata        bytea".Replace("        ", " "), Case.Insensitive,
			"the Postgres snapshot DDL must ship a nullable BYTEA 'metadata' column — the store persists "
			+ "snapshot metadata (a3kqah); dropping it silently loses the version metadata on every save.");
	}

	[Fact]
	public void Require_Postgres_tenant_id_not_null_in_the_primary_key_so_untenanted_rows_share_one_partition()
	{
		// SAFETY — tenant_id is a component of IDENTITY, in the primary key and NOT NULL. A nullable key
		// column lets Postgres treat NULLs as distinct (pre-PG15), so untenanted upserts accumulate
		// duplicate rows the read path never reconciles, and a missing tenant silently lands in the wrong
		// partition. RED if a future edit drops the NOT NULL or narrows the key.
		var ddl = Normalize(File.ReadAllText(PostgresScriptPath));

		ddl.ShouldContain("tenant_id       varchar(255) not null".Replace("       ", " "), Case.Insensitive,
			"the Postgres snapshot DDL must make tenant_id NOT NULL — it participates in the primary key.");
		ddl.ShouldContain("primary key (aggregate_id, aggregate_type, tenant_id)", Case.Insensitive,
			"the primary key must be the (aggregate_id, aggregate_type, tenant_id) triple — a narrower key "
			+ "lets one tenant's save overwrite another tenant's snapshot for the same aggregate.");
	}

	[Fact]
	public void Ship_Oracle_DATA_as_BLOB_not_RAW_so_large_snapshots_do_not_fail_the_write()
	{
		// SAFETY / a7zf4r regression lock — RAW caps at 2000 bytes; a serialized aggregate above that limit
		// fails to write with ORA-01460/ORA-12899. BLOB has no such ceiling. RED if a future edit ships RAW.
		var ddl = Normalize(File.ReadAllText(OracleScriptPath));

		ddl.ShouldContain("data           blob not null".Replace("           ", " "), Case.Insensitive,
			"the Oracle snapshot DDL must declare DATA as BLOB, not RAW — RAW caps at 2000 bytes and fails "
			+ "large snapshots (a7zf4r).");
		ddl.ShouldContain("metadata       blob".Replace("       ", " "), Case.Insensitive,
			"the Oracle snapshot DDL must ship a BLOB 'metadata' column — the store persists metadata.");
	}

	[Fact]
	public void Constrain_Oracle_untenanted_rows_to_one_partition_without_a_nullable_tenant()
	{
		// SAFETY — untenanted snapshots must occupy exactly ONE uniqueness class, or every untenanted save
		// inserts another row for the same aggregate and the single-row read fails at load time, long after
		// the write that caused it.
		//
		// This arm previously asserted the MECHANISM — a function-based unique index over
		// NVL(TENANTID, CHR(1)) — and went red when that mechanism was deliberately replaced. It was not
		// dropped: NVL existed only to compensate for a NULLABLE tenant, because Oracle treats NULLs as
		// DISTINCT in a unique index. The column is now NOT NULL and carries the reserved sentinel every
		// other provider uses, so there are no NULLs left to collapse and the workaround is dead. The DDL
		// says so in terms: "a sentinel invented to patch a schema hole must not outlive the hole."
		//
		// So the arm now binds the PROPERTY rather than the implementation that used to deliver it. A
		// future revision may satisfy it another way again; it will not go red for doing so, only for
		// letting untenanted rows become unconstrained.
		var ddl = Normalize(File.ReadAllText(OracleScriptPath));

		// The declaration is pinned EXACTLY — "NOT NULL" immediately after the type. That is deliberately
		// doing double duty: it fails if the column becomes nullable again (reopening the distinct-NULL
		// hole) AND if a DEFAULT is inserted between the two, which would silently land a tenant-less
		// INSERT in the untenanted partition and make "I forgot the tenant" indistinguishable from "this
		// row is deliberately untenanted". A separate ShouldNotContain("default") was considered and
		// rejected: it would false-fail the day an unrelated column gains a legitimate DEFAULT.
		ddl.ShouldContain("tenantid       varchar2(255) not null".Replace("       ", " "), Case.Insensitive,
			"TENANTID must be NOT NULL with no DEFAULT between the type and the constraint. While it was "
			+ "nullable, Oracle's NULL-distinct treatment left every untenanted row unconstrained by the "
			+ "unique index — the duplicate-snapshot defect this arm exists to catch.");

		ddl.ShouldContain("(aggregateid, aggregatetype, tenantid)", Case.Insensitive,
			"uniqueness must cover the (AGGREGATEID, AGGREGATETYPE, TENANTID) triple, so untenanted rows "
			+ "sharing the reserved sentinel collapse into exactly one uniqueness class.");
	}

	private static string Normalize(string sql)
	{
		var noComments = string.Join(
			'\n',
			sql.Split('\n').Select(static line =>
			{
				var idx = line.IndexOf("--", StringComparison.Ordinal);
				return idx >= 0 ? line[..idx] : line;
			}));

		// Case-insensitive comparisons downstream (Case.Insensitive), so normalize to upper (CA1308).
		return string.Join(' ', noComments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
			.ToUpperInvariant();
	}

	private static string ResolveRepoRelative(string relativePath)
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Excalibur.sln")))
		{
			dir = dir.Parent;
		}

		dir.ShouldNotBeNull("could not locate the solution root (Excalibur.sln) above the test assembly.");
		var full = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
		File.Exists(full).ShouldBeTrue(
			$"the canonical snapshot DDL script is a required shipped deliverable but was not found at '{full}'.");
		return full;
	}
}
