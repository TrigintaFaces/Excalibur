// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A tripwire on <see cref="ErasureCertificatePayload"/>'s field set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a tripwire and not a round-trip.</b> The real proof that a store loses nothing is a round-trip
/// against real infrastructure, and that lives in the conformance kit. This arm covers the failure that
/// happens <i>before</i> anyone runs the kit: a claim is added to the payload, no store gains a column for
/// it, and the loss is silent. Because the signature covers the payload whole, such a field does not merely
/// go missing — the reassembled payload stops matching what was signed and the certificate reports as
/// TAMPERED.
/// </para>
/// <para>
/// <b>This arm cannot tell you a store is correct.</b> It can only stop a new claim being added that no
/// store can hold. When it fails there is nothing here to edit: the column set is DERIVED from the DDL, so
/// the only way to make it green is to add the column where it is missing.
/// </para>
/// <para>
/// <b>It reads the SHIPPED SCRIPT, not the store's auto-create text, and that distinction is the whole
/// point.</b> One schema has two independent definitions — the <c>CREATE TABLE</c> inside
/// <c>SqlServerErasureStore</c>, used when the store provisions its own tables, and
/// <c>Scripts/001_CreateComplianceSchema.sql</c>, which is what a consumer applies by hand and what the
/// package ships. Every automated path exercised the first one, because the conformance suites run against
/// per-suite suffixed tables the store creates itself. So a claim could have a column in the store and
/// none in the script with nothing anywhere noticing — which is what happened to <c>UnreachedData</c>,
/// through a published release. This arm points at the artifact a consumer runs.
/// </para>
/// <para>
/// <b>Scoped to SQL Server, and the scope is the finding.</b> The Postgres store persists the certificate's
/// canonical form verbatim in a payload column and restores it whole, so a claim added to the payload is
/// carried there by construction and no column is owed. SQL Server still reassembles the document from a
/// column per claim, which is what this arm guards. The real proof for both is
/// <c>SaveCertificateAsync_ShouldRoundTripACertificateThatStillVerifies</c> in the conformance kit, which
/// runs against real engines; this one is the cheap tripwire that fires without a database.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EveryPayloadClaimHasSomewhereToLiveShould
{
	// The shipped SQL Server DDL, linked in by this project's EmbeddedResource item. A missing resource
	// throws below rather than yielding an empty set: a schema we could not read is not a schema with no
	// columns, and the difference is the difference between this arm failing and this arm lying.
	private const string ShippedSqlServerSchema = "SqlServer.001_CreateComplianceSchema.sql";

	/// <summary>
	/// The claims whose column is deliberately NOT named after them, and the column that holds each.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is a map, not an exemption, and the distinction is what keeps the arm sharp.</b> A claim
	/// listed here still has to have a column -- it is merely allowed to be a column of a different name. A
	/// claim with no column AT ALL still fails, and so does one listed here whose mapped column does not
	/// exist, because the mapped name is what gets looked up.
	/// </para>
	/// <para>
	/// <b>Why any name differs.</b> The legal-basis enumerations are persisted as their ordinal, and
	/// inserting a zero member meaning "not established" shifted every other member up by one -- so every
	/// row written before that change holds a value which now names a different legal basis. The column was
	/// renamed as part of the migration precisely so the rename is load-bearing: the store binds the new
	/// name, which makes an unmigrated database fail at startup naming the missing column rather than
	/// starting and silently misreading every stored basis. Renaming the column back to the claim's own name
	/// would remove that protection, which is why this map exists instead.
	/// </para>
	/// <para>
	/// Adding an entry here is a design decision, not a way to quiet this arm. If a new claim has no column,
	/// add the column.
	/// </para>
	/// </remarks>
	private static readonly Dictionary<string, string> ColumnHoldingClaim = new(StringComparer.Ordinal)
	{
		["LegalBasis"] = "LegalBasisV2",
	};

	[Fact]
	public void Fail_when_a_payload_claim_has_no_column_in_the_shipped_sql_server_schema()
	{
		var declared = typeof(ErasureCertificatePayload)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(static p => p.Name)
			.Where(static n => !string.Equals(n, "EqualityContract", StringComparison.Ordinal))
			.ToHashSet(StringComparer.Ordinal);

		// LIVENESS. A payload that reflected to nothing, or a schema that parsed to nothing, would satisfy
		// the safety assertion below while measuring nothing at all.
		declared.ShouldNotBeEmpty("the payload type reflected to no properties, so nothing was checked");

		var columns = CertificateColumnsInShippedSchema();
		columns.ShouldNotBeEmpty(
			"no columns were parsed out of the shipped ErasureCertificates table, so this arm measured "
			+ "nothing. The DDL's shape changed, not the schema — fix the scan, do not delete the arm.");

		var unpersisted = declared
			.Select(static n => ColumnHoldingClaim.GetValueOrDefault(n, n))
			.Except(columns, StringComparer.Ordinal)
			.OrderBy(static n => n, StringComparer.Ordinal);

		string.Join(", ", unpersisted).ShouldBeEmpty(
			"these payload claims are signed but have no column in the SHIPPED SQL Server schema "
			+ "(Scripts/001_CreateComplianceSchema.sql), which is the script a consumer applies by hand. "
			+ "The SQL Server store reassembles a certificate from its columns, so a claim with no column "
			+ "comes back absent and the certificate reports as TAMPERED — and because the store's "
			+ "fail-closed schema probe requires the column, a consumer who applied this script cannot "
			+ "start at all. Add the column to the script; do not narrow this arm.");
	}

	/// <summary>
	/// Reads the column names out of the shipped <c>ErasureCertificates</c> CREATE TABLE.
	/// </summary>
	/// <remarks>
	/// Scanned line by line rather than with one pattern spanning the table body: the body is delimited by
	/// lines, and a single pattern would depend on the exact whitespace around the closing parenthesis.
	/// </remarks>
	private static HashSet<string> CertificateColumnsInShippedSchema()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			n => n.EndsWith(ShippedSqlServerSchema, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"Embedded resource ending '{ShippedSqlServerSchema}' was not found. It is linked in by "
				+ "this test project's EmbeddedResource item; if that item was removed, restore it rather "
				+ "than relaxing this arm — without it the arm cannot fail.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);
		var sql = reader.ReadToEnd();

		var lines = sql.Split('\n');
		var open = Array.FindIndex(
			lines,
			l => l.Contains("CREATE TABLE [compliance].[ErasureCertificates]", StringComparison.Ordinal));

		if (open < 0)
		{
			return [];
		}

		var columns = new HashSet<string>(StringComparer.Ordinal);
		for (var i = open + 1; i < lines.Length; i++)
		{
			var line = lines[i].Trim();

			if (line.StartsWith(")", StringComparison.Ordinal))
			{
				break;
			}

			if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal))
			{
				continue;
			}

			// A column definition opens with its name followed by a type. CONSTRAINT and INDEX lines are
			// skipped, so a name that appears only inside an index is NOT counted as a column — which is
			// what keeps a missing column detectable instead of incidentally matched.
			var name = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];

			if (name is "CONSTRAINT" or "INDEX" or "PRIMARY" or "UNIQUE")
			{
				continue;
			}

			_ = columns.Add(name);
		}

		return columns;
	}
}
