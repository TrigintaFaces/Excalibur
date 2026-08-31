// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.EventSourcing.Tests.Migrations;

/// <summary>
/// Binds the single-tenant precondition of the shipped convergence script, for every provider that ships one.
/// </summary>
/// <remarks>
/// <para>
/// The script rewrites the stored tenant term from the untenanted sentinel to the single-tenant default
/// identity, across four tables, as one unresumable set-based UPDATE per table. That is correct on a
/// single-tenant deployment and destructive on a multi-tenant one, where the untenanted partition is a live
/// partition holding rows that belong to no tenant: converging it files ownerless data under one specific,
/// wrong tenant.
/// </para>
/// <para>
/// The precondition used to be a sentence in the script header. The header is read once, by whoever adopts
/// the script into a deployment's migration set; whatever applies that set later does not read it, and where
/// the set is applied automatically at startup nothing on that path asks whether the deployment is
/// single-tenant. The header also asserted the script is never invoked automatically, which is not a property
/// anything enforces. So the precondition is now a refusal the engine performs, and this suite binds it.
/// </para>
/// <para>
/// Both arms matter and neither implies the other. The safety arm proves the refusal exists and stands ahead
/// of the first rewrite — a guard placed after the UPDATE it guards is decoration. The liveness arm proves
/// the script still converges: a script that refuses everything, or that lost its UPDATE statements
/// altogether, satisfies the safety arm perfectly while doing nothing a single-tenant operator ran it for.
/// </para>
/// <para>
/// This suite reads text. The behaviour behind it was verified against a real PostgreSQL 17 and a real
/// SQL Server 2022: seeded with a named tenant, both scripts refuse and leave every untenanted row in place;
/// seeded single-tenant, both converge all four tables; and the previously shipped revision of each converged
/// the named-tenant database without complaint.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ShippedConvergenceScriptShould
{
	/// <summary>
	/// One row per provider that ships the script: the linked resource, the exact text of a rewrite's SET
	/// clause, the exact text of its WHERE clause, and the predicate that recognises a foreign tenant. The
	/// clauses are spelled out per provider rather than composed from a column name, because the two dialects
	/// differ in bracketing, in the N-prefix, and in how quotes are doubled inside the dynamic probe.
	/// </summary>
	public static TheoryData<string, string, string, string> Providers { get; } = new()
	{
		{
			"006_SqlServer_ConvergeUntenantedToDefaultTenant.sql",
			"SET [TenantId] = N'__default__'",
			"WHERE [TenantId] = N'__untenanted__';",
			"[TenantId] NOT IN (N''__untenanted__'', N''__default__'')"
		},
		{
			"006_Postgres_ConvergeUntenantedToDefaultTenant.sql",
			"SET tenant_id = '__default__'",
			"WHERE tenant_id = '__untenanted__';",
			"tenant_id NOT IN (''__untenanted__'', ''__default__'')"
		},
	};

	/// <summary>
	/// SAFETY. The script refuses when a row is filed under a tenant that is neither the untenanted sentinel
	/// nor the single-tenant default, and it does so before the first rewrite.
	/// </summary>
	/// <param name="resource">The linked script resource.</param>
	/// <param name="rewrite">The exact SET clause of a convergence rewrite.</param>
	/// <param name="restriction">The exact WHERE clause restricting a rewrite to the sentinel.</param>
	/// <param name="foreignTenant">The predicate that recognises a tenant outside the reserved pair.</param>
	[Theory]
	[MemberData(nameof(Providers))]
	public void RefuseAMultiTenantDeploymentBeforeRewritingAnything(
		string resource,
		string rewrite,
		string restriction,
		string foreignTenant)
	{
		_ = restriction;

		var script = Executable(resource);

		var refusal = script.IndexOf("006 REFUSED", StringComparison.Ordinal);

		refusal.ShouldBeGreaterThanOrEqualTo(
			0,
			$"{resource} carries no refusal. The single-tenant precondition is then only prose, and a host "
			+ "that migrates on startup applies the script at boot without reading it.");

		// The predicate that makes the refusal a test of the deployment rather than of one row: a tenant
		// outside {sentinel, default} exists only where real tenants do.
		script.ShouldContain(
			foreignTenant,
			Case.Sensitive,
			customMessage: $"{resource}'s refusal does not key on a tenant outside the reserved pair, so it "
				+ "is not testing whether this deployment has named tenants.");

		var firstRewrite = script.IndexOf(rewrite, StringComparison.Ordinal);

		firstRewrite.ShouldBeGreaterThanOrEqualTo(0, $"{resource} no longer rewrites the tenant term at all");
		refusal.ShouldBeLessThan(
			firstRewrite,
			$"{resource} places its refusal after the first rewrite. A guard that runs once the rows have "
			+ "already moved is decoration: the four tables converge in sequence and the migration is not "
			+ "resumable.");
	}

	/// <summary>
	/// LIVENESS. The script still converges every table it owns, so the refusal has not been bought by
	/// turning the migration into a no-op.
	/// </summary>
	/// <param name="resource">The linked script resource.</param>
	/// <param name="rewrite">The exact SET clause of a convergence rewrite.</param>
	/// <param name="restriction">The exact WHERE clause restricting a rewrite to the sentinel.</param>
	/// <param name="foreignTenant">Unused here; the refusal is the other arm's subject.</param>
	[Theory]
	[MemberData(nameof(Providers))]
	public void StillConvergeEveryTableOnASingleTenantDeployment(
		string resource,
		string rewrite,
		string restriction,
		string foreignTenant)
	{
		_ = foreignTenant;

		var script = Executable(resource);

		var rewrites = CountOccurrences(script, rewrite);

		rewrites.ShouldBe(
			4,
			$"{resource} rewrites {rewrites} table(s), not the four this package owns — the event stream, "
			+ "its snapshots, the materialized views and their positions. A single-tenant operator who runs "
			+ "this and gets three of four is left with data that reads as missing, which is the condition "
			+ "the script exists to end.");

		CountOccurrences(script, restriction).ShouldBe(
			4,
			$"{resource} has a rewrite that does not restrict itself to the untenanted sentinel");
	}

	/// <summary>
	/// The header no longer asserts the script cannot be invoked automatically, because nothing enforces that.
	/// </summary>
	/// <param name="resource">The linked script resource.</param>
	/// <param name="rewrite">Unused; the provider set is keyed by resource.</param>
	/// <param name="restriction">Unused; the provider set is keyed by resource.</param>
	/// <param name="foreignTenant">Unused; the provider set is keyed by resource.</param>
	[Theory]
	[MemberData(nameof(Providers))]
	public void NotClaimItIsNeverInvokedAutomatically(
		string resource,
		string rewrite,
		string restriction,
		string foreignTenant)
	{
		_ = (rewrite, restriction, foreignTenant);

		Load(resource).ShouldNotContain(
			"It is never invoked automatically",
			Case.Sensitive,
			customMessage: $"{resource}'s header claims nothing runs it, which nothing enforces once the "
				+ "script is adopted into a migration set — and that claim is what makes the precondition "
				+ "read as advice.");
	}

	private static int CountOccurrences(string haystack, string needle)
	{
		var count = 0;

		for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
			i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	// The executable half: the header names the sentinel and the default identity in prose and explains the
	// refusal in prose, so testing the raw file would pass on its own documentation.
	private static string Executable(string resource) => string.Join(
		'\n',
		Load(resource)
			.Split('\n')
			.Select(static line => line.TrimEnd('\r'))
			.Where(static line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

	private static string Load(string resource)
	{
		var assembly = Assembly.GetExecutingAssembly();

		var name = Array.Find(
			assembly.GetManifestResourceNames(),
			candidate => candidate.EndsWith(resource, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{resource}' is not embedded in {assembly.GetName().Name}. It is linked "
				+ "in by the test project's EmbeddedResource item; if that item was removed, this suite "
				+ "would silently stop looking at the script a consumer actually runs.");

		using var stream = assembly.GetManifestResourceStream(name)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
