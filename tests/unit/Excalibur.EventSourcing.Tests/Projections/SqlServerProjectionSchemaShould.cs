// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.SqlServer;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// The emitted projection-table DDL, and the shipped script that must agree with it.
/// </summary>
/// <remarks>
/// <para>
/// A projection table's columns are decided by the store, not by the projection type, and until this
/// script existed every consumer hand-wrote them from column names embedded in the store's C#. Three
/// of this repository's own test files did exactly that and had already drifted — one declared the id
/// column as <c>NVARCHAR(255)</c> where its sibling declared <c>NVARCHAR(450)</c>.
/// </para>
/// <para>
/// <b>Shipping both a generator and a script re-creates that risk between two of our own artifacts,
/// which is why the comparison below exists.</b> It is not a tidiness check: a consumer who runs the
/// script and a consumer who calls the generator must get the same table, or one of them has a
/// database the store cannot read.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SqlServerProjectionSchemaShould
{
	private const string ScriptPath =
		"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/005_CreateProjectionSchema.sql";

	[Fact]
	public void EmitTheSameDefinitionAsTheShippedScript()
	{
		var script = ReadShippedScript();

		// The script is a template: one token, replaced per projection. Substituting it is exactly what
		// a consumer following the file's own instructions does.
		var fromScript = Normalise(script.Replace(
			"{ProjectionTableName}", "OrderSummary", StringComparison.Ordinal));
		var fromGenerator = Normalise(SqlServerProjectionSchema.CreateTableScript("OrderSummary"));

		fromGenerator.ShouldBe(
			fromScript,
			customMessage:
				"the shipped script and the generated script must define the same table -- a consumer "
				+ "who runs one and a consumer who calls the other must not end up with different "
				+ "tables, because the store can only read one of them");
	}

	[Fact]
	public void DefaultThePositionToTheUnplaceableSentinelRatherThanZeroOrNull()
	{
		// THE COLUMN THAT CANNOT BE GUESSED. Zero is a legitimate stream position, so a zero default
		// would make a brand-new row claim it had already folded the first event; NULL compares equal to
		// nothing, and the compare-and-set that orders two concurrent writers compares against it. The
		// sentinel is read from the shared constant, so the DDL cannot disagree with the store's own
		// reading of the value.
		var ddl = SqlServerProjectionSchema.CreateTableScript("OrderSummary");

		var positionColumn = ddl.Split('\n')
			.Single(line => line.TrimStart().StartsWith("[LastAppliedPosition]", StringComparison.Ordinal));
		Normalise(positionColumn).ShouldBe(
			"[LastAppliedPosition] BIGINT NOT NULL CONSTRAINT [DF_OrderSummary_LastAppliedPosition] "
			+ $"DEFAULT ({ProjectionPosition.UnplaceableSentinel}),",
			customMessage: "the position column itself must be non-null and default to the UNPLACEABLE sentinel");
	}

	[Fact]
	public void CreateIdempotentlySoItCanRunOnEveryDeployment()
	{
		// A deployment script that fails the second time it runs is a script people stop running.
		SqlServerProjectionSchema.CreateTableScript("OrderSummary")
			.ShouldContain("IF OBJECT_ID(");
	}

	[Theory]
	[InlineData("Order Summary")]
	[InlineData("Order;DROP TABLE x--")]
	[InlineData("[OrderSummary]")]
	[InlineData("订单")]
	[InlineData("Order١")]
	[InlineData("")]
	[InlineData("   ")]
	public void RefuseAnIdentifierItWouldHaveToInterpolateBlindly(string tableName)
	{
		// SAFETY. The name is interpolated -- there is no parameterised CREATE TABLE -- so the identifier
		// is restricted rather than quoted and hoped. A table name is a deployment-time constant, so
		// refusing the unusual shapes costs a consumer nothing.
		_ = Should.Throw<ArgumentException>(
			() => SqlServerProjectionSchema.CreateTableScript(tableName));
	}

	[Theory]
	[InlineData("租户")]
	[InlineData("schema١")]
	public void RefuseASchemaNameTheStoreCannotAddress(string schemaName)
	{
		_ = Should.Throw<ArgumentException>(
			() => SqlServerProjectionSchema.CreateTableScript("OrderSummary", schemaName));
	}

	[Fact]
	public void AcceptTheNamesConsumersActuallyUse()
	{
		// LIVENESS, and the arm that stops the refusal above being satisfied by refusing everything.
		foreach (var name in new[] { "OrderSummary", "order_summary_v2", "Projection1" })
		{
			SqlServerProjectionSchema.CreateTableScript(name).ShouldContain(name);
		}
	}

	// Walks up to the repository root rather than assuming the test's working directory, so the arm
	// fails with "script not found" if the file moves instead of silently comparing nothing.
	private static string ReadShippedScript()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, ScriptPath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(candidate))
			{
				return File.ReadAllText(candidate);
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			$"The shipped projection schema script was not found at '{ScriptPath}' from any ancestor of "
			+ $"'{AppContext.BaseDirectory}'. If it moved, this comparison stops protecting anything.");
	}

	// Compares the DEFINITION, not the formatting: comments, blank lines, indentation and the trailing
	// GO are presentation. Column names, types, nullability, the default and the key are not.
	private static string Normalise(string sql)
	{
		var lines = sql.Split('\n')
			.Select(line => line.Trim())
			.Where(line => line.Length > 0)
			.Where(line => !line.StartsWith("--", StringComparison.Ordinal))
			.Where(line => !string.Equals(line, "GO", StringComparison.OrdinalIgnoreCase));

		return string.Join(
			' ',
			string.Join(' ', lines).Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public void KeepTheCombinedKeyWithinSqlServersClusteredIndexLimit()
	{
		// THE INPUT BOUNDARY NO EXISTING ARM MEASURED. The schema arms ran with short keys, so a table
		// whose declared key could not physically be stored still passed every one of them: SQL Server
		// CREATEs such a table with a warning and only refuses the INSERT whose actual key exceeds 900
		// bytes. The widths summed to 1,300 before this arm existed.
		//
		// Derived from the declared widths rather than asserted as a literal, so widening a column
		// reddens this instead of quietly spending a budget that is not ours to spend.
		var ddl = SqlServerProjectionSchema.CreateTableScript("OrderSummary");

		var widths = System.Text.RegularExpressions.Regex
			.Matches(ddl, @"\[(TenantId|Id)\]\s+NVARCHAR\((\d+)\)")
			.Select(match => int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
			.ToArray();

		widths.Length.ShouldBe(2, "both key columns must declare an explicit width");

		// NVARCHAR is two bytes per character; the clustered index key limit is 900 bytes.
		(widths.Sum() * 2).ShouldBeLessThanOrEqualTo(
			900,
			customMessage:
				"the primary key is clustered, so TenantId + Id must fit 900 bytes -- a wider pair "
				+ "creates the table and then rejects inserts on the consumer's database");
	}
}
