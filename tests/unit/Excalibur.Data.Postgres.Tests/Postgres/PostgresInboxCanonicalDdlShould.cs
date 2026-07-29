// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Data.Tests.Postgres;

// bd-itmzmr (S887 REVIEW_CODE, P1-b / SENTINEL re-review bar #2) — structural lock on the SHIPPED canonical Postgres
// inbox DDL, proving the "silent exactly-once bypass" is closed by construction in the artifact consumers actually
// run — not merely described in the README (SENTINEL: "confirm in the shipped Scripts/*.sql, not just README").
//
// THE BYPASS (the safety property this lock defends). In a MULTI-TENANT deployment the store dedups on
// ON CONFLICT (message_id, handler_type, tenant_id). If the shipped MT unique key allowed a NULLABLE tenant_id,
// pre-PG15 Postgres treats NULLs as DISTINCT, so two rows with a NULL tenant_id never collide → ON CONFLICT never
// fires → duplicates slip through → exactly-once silently broken. The canonical MT DDL therefore MUST make
// tenant_id NOT NULL, which makes the NULL-distinct duplicate path structurally unrepresentable
// (enforce-invariants-structurally). This lock fails RED if a future edit ships an MT triple key without the
// NOT NULL guard — the exact regression that reopens the bypass.
//
// The lock reads the real file that ships in the nupkg (csproj: <None Include="Scripts\*.sql" Pack="true" ...>), so
// it binds the consumer artifact. It resolves the repo-relative path by walking up to the solution root — a missing
// script FAILS (the canonical DDL is a required deliverable), it does not skip.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresInboxCanonicalDdlShould
{
	private static readonly string ScriptPath = ResolveRepoRelative(
		"src/Excalibur/Excalibur.Inbox.Postgres/Scripts/001_CreateInboxSchema.sql");

	// Design A split the canonical DDL into two shipped scripts: the default single-tenant (pair) script above, and
	// the multi-tenant (triple, tenant_id NOT NULL) script below. The triple-key safety arm binds the MT script.
	private static readonly string MultiTenantScriptPath = ResolveRepoRelative(
		"src/Excalibur/Excalibur.Inbox.Postgres/Scripts/001_CreateInboxSchema.MultiTenant.sql");

	[Fact]
	public void Ship_a_default_non_multi_tenant_pair_key_so_the_zero_config_store_matches()
	{
		// LIVENESS — the default (non-MT) deployment must have the pair key the store emits when scope is None, or a
		// single-tenant consumer running the script gets a 42P10 on the pair-key ON CONFLICT. Proves the DDL serves
		// the non-MT path, not only the MT one.
		var ddl = Normalize(File.ReadAllText(ScriptPath));

		ddl.ShouldContain(
			"primary key (message_id, handler_type)",
			Case.Insensitive,
			"the canonical DDL must ship the default single-tenant PAIR key — the store emits "
			+ "ON CONFLICT (message_id, handler_type) when no tenant is ambient.");
	}

	[Fact]
	public void Require_tenant_id_not_null_in_the_multi_tenant_triple_key_so_the_null_distinct_bypass_is_unrepresentable()
	{
		// SAFETY — the regression arm. The MT deployment section MUST both key on the triple AND mandate
		// tenant_id NOT NULL. Either omission reopens the silent exactly-once bypass (a nullable tenant_id in a triple
		// unique lets pre-PG15 NULLs stay distinct → ON CONFLICT never fires). RED if a future edit ships the MT
		// triple key without SET NOT NULL.
		var ddl = Normalize(File.ReadAllText(MultiTenantScriptPath));

		ddl.ShouldContain(
			"(message_id, handler_type, tenant_id)",
			Case.Insensitive,
			"the canonical DDL must document the MULTI-TENANT triple unique key — the store emits "
			+ "ON CONFLICT (message_id, handler_type, tenant_id) when a tenant is ambient.");

		ddl.ShouldContain(
			"tenant_id text not null",
			Case.Insensitive,
			"the MULTI-TENANT triple key MUST make tenant_id NOT NULL — a NULLABLE tenant_id in a triple unique key "
			+ "lets pre-PG15 Postgres treat NULLs as distinct, so ON CONFLICT never fires and duplicates slip through "
			+ "(exactly-once silently broken). Dropping this guard is the regression this lock forbids.");
	}

	// Collapse SQL comment markers/whitespace so the assertions bind the DDL text whether the MT block ships live or
	// as a documented (commented) alternative — the structural contract is the same either way.
	//
	// Case is deliberately NOT folded here: both call sites already assert with Case.Insensitive, so lowercasing the
	// DDL was redundant with the comparison that consumes it. Case-insensitivity belongs in the comparison, not in a
	// destructive rewrite of the text being compared.
	private static string Normalize(string sql)
	{
		var noComments = sql.Replace("--", " ", StringComparison.Ordinal);
		return string.Join(' ', noComments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
			$"the canonical inbox DDL script is a required shipped deliverable but was not found at '{full}'.");
		return full;
	}
}
