// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Postgres;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// The emitted Postgres projection-table DDL, and the shipped script that must agree with it.
/// </summary>
/// <remarks>
/// <para>
/// A projection table's columns are decided by the store, not by the projection type, and until this
/// script existed every consumer hand-wrote them from column names embedded in the store's C#. Three
/// of this repository's own test files did exactly that and had already drifted — one declared the tenant
/// column as <c>VARCHAR(450)</c> where its siblings declared <c>VARCHAR(255)</c>.
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
public sealed class PostgresProjectionSchemaShould
{
	private const string ScriptPath =
		"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/005_CreateProjectionSchema.sql";

	[Fact]
	public void EmitTheSameDefinitionAsTheShippedScript()
	{
		var script = ReadShippedScript();

		// The script is a template: one token, replaced per projection. Substituting it is exactly what
		// a consumer following the file's own instructions does.
		var fromScript = Normalise(script.Replace(
			"{projection_table_name}", "order_summary", StringComparison.Ordinal));
		var fromGenerator = Normalise(PostgresProjectionSchema.CreateTableScript("order_summary"));

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
		var ddl = PostgresProjectionSchema.CreateTableScript("order_summary");

		var positionColumn = ddl.Split('\n')
			.Single(line => line.TrimStart().StartsWith("last_applied_position", StringComparison.Ordinal));
		Normalise(positionColumn).ShouldBe(
			$"last_applied_position BIGINT NOT NULL DEFAULT {ProjectionPosition.UnplaceableSentinel},",
			customMessage: "the position column itself must be non-null and default to the UNPLACEABLE sentinel");

		// Lowercase, because the store references these columns UNQUOTED and an unquoted identifier
		// folds to lower case -- a quoted mixed-case column would be unreachable from those queries.
		ddl.ShouldNotContain("LastAppliedPosition", Case.Sensitive);
	}

	[Fact]
	public void CreateIdempotentlySoItCanRunOnEveryDeployment()
	{
		// A deployment script that fails the second time it runs is a script people stop running.
		PostgresProjectionSchema.CreateTableScript("order_summary")
			.ShouldContain("CREATE TABLE IF NOT EXISTS");
	}

	[Theory]
	[InlineData("order summary")]
	[InlineData("order;DROP TABLE x--")]
	[InlineData("\"order_summary\"")]
	[InlineData("")]
	[InlineData("   ")]
	public void RefuseAnIdentifierItWouldHaveToInterpolateBlindly(string tableName)
	{
		// SAFETY. The name is interpolated -- there is no parameterised CREATE TABLE -- so the identifier
		// is restricted rather than quoted and hoped. A table name is a deployment-time constant, so
		// refusing the unusual shapes costs a consumer nothing.
		_ = Should.Throw<ArgumentException>(
			() => PostgresProjectionSchema.CreateTableScript(tableName));
	}

	[Fact]
	public void QualifyTheTableWithTheSchemaTheStoreAddresses()
	{
		// Every statement the store runs is schema-qualified, so a table created unqualified resolves
		// through search_path and is a table the store may not find. The default is the RESOLVED one.
		PostgresProjectionSchema.CreateTableScript("order_summary")
			.ShouldContain("""CREATE TABLE IF NOT EXISTS "public"."order_summary" """.TrimEnd());

		// Quoted, so the case is preserved exactly as configured -- matching the store, which quotes both.
		PostgresProjectionSchema.CreateTableScript("OrderSummary", "Reporting")
			.ShouldContain("""CREATE TABLE IF NOT EXISTS "Reporting"."OrderSummary" """.TrimEnd());
	}

	[Theory]
	[InlineData("public other")]
	[InlineData("\"public\"")]
	[InlineData("public.other")]
	[InlineData("")]
	public void RefuseASchemaItWouldHaveToInterpolateBlindly(string schema) =>
		_ = Should.Throw<ArgumentException>(
			() => PostgresProjectionSchema.CreateTableScript("order_summary", schema));

	[Fact]
	public void AcceptTheNamesConsumersActuallyUse()
	{
		// LIVENESS, and the arm that stops the refusal above being satisfied by refusing everything.
		foreach (var name in new[] { "order_summary", "order_summary_v2", "Projection1" })
		{
			PostgresProjectionSchema.CreateTableScript(name).ShouldContain(name);
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
}
