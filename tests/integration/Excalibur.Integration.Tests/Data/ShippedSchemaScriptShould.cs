// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Locks the reader every SQL fixture provisions its schema through, against the scripts the packages
/// actually ship.
/// </summary>
/// <remarks>
/// <para>
/// The scripts are sqlcmd and psql templates, so they carry lines those tools interpret and never send
/// to a server. A reader that leaves one in hands the driver something it cannot parse, and the fixture
/// dies before it has provisioned anything: <c>Incorrect syntax near ':'</c> from SqlCommand,
/// <c>42601 syntax error at or near "\"</c> from Npgsql. That took out two whole integration surfaces
/// while the reader stripped <c>:setvar</c> and nothing else.
/// </para>
/// <para>
/// The safety arm is deliberately written against the WHOLE shipped corpus and against the general
/// shape of a directive rather than the three that exist today. A test naming those three would pass on
/// the day a fourth is added to a script, which is the same day every fixture reading that script goes
/// red — and it would go red in an integration run, hours downstream of the one-line change that caused
/// it. This arm fails at the point the directive is added, without a container.
/// </para>
/// <para>
/// No container: this reads files. It lives beside the fixtures it protects rather than in a unit
/// project because that is where its failure is felt.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ShippedSchemaScriptShould
{
	/// <summary>
	/// Gets every SQL script the packages ship, as repository-relative paths.
	/// </summary>
	public static TheoryData<string> ShippedScripts
	{
		get
		{
			var data = new TheoryData<string>();
			var root = RepositoryRoot();

			foreach (var file in Directory.EnumerateFiles(
				Path.Combine(root, "src"), "*.sql", SearchOption.AllDirectories))
			{
				var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');

				// A build copies the shipped scripts into bin/ and obj/. Those are the same bytes, so
				// including them would multiply every case without covering anything, and would make the
				// set depend on whether anyone had built the solution.
				if (relative.Contains("/bin/", StringComparison.Ordinal)
					|| relative.Contains("/obj/", StringComparison.Ordinal))
				{
					continue;
				}

				data.Add(relative);
			}

			return data;
		}
	}

	[Theory]
	[MemberData(nameof(ShippedScripts))]
	public void Leave_NoClientDirective_InAnyShippedScript(string script)
	{
		// A directive is any line whose first non-blank character is ':' or '\'. No statement in either
		// dialect can begin with one, so anything still matching here is a client meta-command the
		// drivers will reject -- whether or not it is one of the ones that existed when this was written.
		var survivors = ShippedSchemaScript.Read(script)
			.Split('\n')
			.Select(static line => line.TrimStart())
			.Where(static line => line.StartsWith(':') || line.StartsWith('\\'))
			.ToList();

		survivors.ShouldBeEmpty(
			$"'{script}' still carries client meta-commands after reading: {string.Join(" | ", survivors)}. "
			+ "sqlcmd and psql interpret these and never send them to the server, so a fixture executing "
			+ "this script through a driver fails before provisioning anything. Teach ShippedSchemaScript "
			+ "the directive rather than working around it in the fixture.");
	}

	[Theory]
	[MemberData(nameof(ShippedScripts))]
	public void Carry_NoClientDirective_InTheBytesItShips(string script)
	{
		// THE ARM ABOVE READS THROUGH ShippedSchemaScript.Read, WHICH STRIPS DIRECTIVES, so it can only
		// ever prove that OUR READER copes with one. It cannot fail on a shipped script that carries a
		// directive, because the directive is gone before it looks -- and it was green over six scripts
		// that no consumer could run.
		//
		// A consumer has no reader. They point Npgsql, JDBC, Flyway or Liquibase at the file as shipped,
		// and a line the tool does not interpret goes to the server: '42601 syntax error at or near "\"'
		// from Npgsql, 'Incorrect syntax near ":"' from SqlClient. Either way nothing is provisioned.
		//
		// So this arm reads the FILE, not the reader's opinion of it. The predicate is the property the
		// consumer depends on: no line of a shipped script begins a client meta-command. Both arms are
		// kept -- they answer different questions, and only this one answers the consumer's.
		var onDisk = File.ReadAllText(ShippedSchemaScript.Resolve(script));

		var directives = onDisk
			.Split('\n')
			.Select(static (line, index) => (Number: index + 1, Text: line.Trim()))
			.Where(static line => line.Text.StartsWith(':') || line.Text.StartsWith('\\'))
			.Select(static line => $"line {line.Number}: {line.Text}")
			.ToList();

		directives.ShouldBeEmpty(
			$"'{script}' ships with client meta-commands in it: {string.Join(" | ", directives)}. "
			+ "No statement in either dialect begins with ':' or '\\', so every runner that is not the one "
			+ "tool that owns the directive sends it to the server and the script dies having provisioned "
			+ "nothing. A client setting belongs on the INVOCATION -- 'psql -v ON_ERROR_STOP=1 -f <script>', "
			+ "'sqlcmd -b -i <script>' -- documented in the script's header, never in a line the server has "
			+ "to parse.");
	}

	[Theory]
	[MemberData(nameof(ShippedScripts))]
	public void Keep_TheSql_WhileRemovingTheDirectives(string script)
	{
		// LIVENESS. Without this, a reader that returned the empty string would satisfy the arm above for
		// every script in the corpus, and every fixture would then provision nothing and report success
		// until a test asked the database for a table. Stripping is only correct if what is left is the
		// script.
		var read = ShippedSchemaScript.Read(script);
		var onDisk = File.ReadAllText(ShippedSchemaScript.Resolve(script));

		var kept = read.Split('\n').Count(static line => line.Trim().Length > 0);
		var directives = onDisk.Split('\n')
			.Select(static line => line.Trim())
			.Count(static line => line.StartsWith(':') || line.StartsWith('\\') || IsSqlPlusErrorDirective(line));
		var comments = onDisk.Split('\n')
			.Select(static line => line.Trim())
			.Count(static line => line.StartsWith("--", StringComparison.Ordinal));
		var onDiskContent = onDisk.Split('\n').Count(static line => line.Trim().Length > 0);

		kept.ShouldBe(
			onDiskContent - directives - comments,
			$"'{script}' lost lines that are neither a comment nor a client directive. The reader must "
			+ "remove what the driver cannot parse and nothing else -- a test running against a schema "
			+ "quietly narrower than the shipped one is worse than the loud failure it replaces.");
	}

	[Theory]
	[MemberData(nameof(ShippedScripts))]
	public void Remove_TheSqlPlusErrorDirective_SoADriverNeverSeesIt(string script)
	{
		// An Oracle script that can REFUSE carries WHENEVER SQLERROR EXIT FAILURE, because SQL*Plus
		// otherwise exits 0 on the refusal and an unattended runner records a declined migration as
		// applied. Oracle has no invocation flag for that -- unlike 'psql -v ON_ERROR_STOP=1' and
		// 'sqlcmd -b' -- so the setting can only live in the file, and every script carrying one already
		// requires SQL*Plus or SQLcl for its '/' block terminators. It is still a CLIENT directive: a
		// driver sending it to the server gets ORA-00900, so the reader must remove it like any other.
		var survivors = ShippedSchemaScript.Read(script)
			.Split('\n')
			.Select(static line => line.Trim())
			.Where(IsSqlPlusErrorDirective)
			.ToList();

		survivors.ShouldBeEmpty(
			$"'{script}' still carries a SQL*Plus error directive after reading: {string.Join(" | ", survivors)}. "
			+ "A driver cannot parse it, so a fixture executing this script provisions nothing.");
	}

	private static bool IsSqlPlusErrorDirective(string line) =>
		line.StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase);

	[Fact]
	public void Substitute_SqlCmdVariables_FromTheScriptsOwnDefaults()
	{
		// ReadSqlCmdBatches stays in the helper because a provider may still ship a sqlcmd template, and a
		// fixture reading one must resolve its variables rather than create objects under a literal
		// '$(...)' name. These arms are written against a fixture this test writes rather than against a
		// shipped script, so the shipped corpus is free to carry no sqlcmd syntax at all -- which is what
		// the directive arms above now require of it.
		var fixture = WriteSqlCmdFixture(
			":setvar Schema \"app\"\n"
			+ ":setvar Table \"Widgets\"\n"
			+ "\n"
			+ "CREATE TABLE [$(Schema)].[$(Table)] (Id INT NOT NULL PRIMARY KEY);\n"
			+ "GO\n"
			+ "\n"
			+ "CREATE INDEX IX_Widgets ON [$(Schema)].[$(Table)] (Id);\n");

		var batches = ShippedSchemaScript.ReadSqlCmdBatches(fixture);

		batches.Count.ShouldBe(2, "GO separates the batches and is never sent to the server itself.");
		batches.ShouldAllBe(static batch => !batch.Contains("$(", StringComparison.Ordinal));
		batches.ShouldAllBe(static batch => !batch.Contains(":setvar", StringComparison.Ordinal));
		batches[0].ShouldContain("[app].[Widgets]");
	}

	[Fact]
	public void Prefer_AnOverride_ToTheScriptsOwnDefault()
	{
		// LIVENESS for the override path: a ReadSqlCmdBatches that ignored `overrides` altogether would
		// satisfy the arm above, because that one substitutes the declared default either way.
		var fixture = WriteSqlCmdFixture(
			":setvar Schema \"app\"\n"
			+ "\n"
			+ "SELECT * FROM [$(Schema)].[Widgets];\n");

		var batches = ShippedSchemaScript.ReadSqlCmdBatches(
			fixture,
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Schema"] = "tenant7" });

		batches.ShouldHaveSingleItem().ShouldContain("[tenant7].[Widgets]");
	}

	[Fact]
	public void Refuse_AVariable_ThatHasNoDeclaredDefaultAndNoOverride()
	{
		// The failure that matters here is the silent one: a token left unresolved creates an object
		// literally named '$(Schema)'. Refusing loudly is the behaviour, so it gets an arm of its own.
		var fixture = WriteSqlCmdFixture("SELECT * FROM [$(Undeclared)].[Widgets];\n");

		_ = Should.Throw<InvalidOperationException>(() => ShippedSchemaScript.ReadSqlCmdBatches(fixture));
	}

	[Fact]
	public void Keep_APlSqlBlock_Whole_WhenSplittingAnOracleScript()
	{
		// The load-bearing property: a DECLARE...END block's own statements AND its END are
		// semicolon-terminated, so a naive ';' split shreds it into fragments no driver can execute.
		// The block is delimited by a line holding only '/', which is what SQL*Plus uses.
		var fixture = WriteOracleFixture(
			"""
			CREATE TABLE Widgets (id NUMBER);
			DECLARE
			  v NUMBER;
			BEGIN
			  SELECT COUNT(*) INTO v FROM Widgets;
			  IF v = 0 THEN
			    EXECUTE IMMEDIATE 'ALTER TABLE Widgets ADD (name VARCHAR2(10))';
			  END IF;
			END;
			/
			CREATE INDEX IX_Widgets ON Widgets (id);
			""");

		var units = ShippedSchemaScript.ReadOracleUnits(fixture);

		units.Count.ShouldBe(3, "the table, the block sent whole, and the index");
		units[0].ShouldContain("CREATE TABLE Widgets");
		units[1].ShouldStartWith("DECLARE");
		units[1].ShouldEndWith("END;", Case.Sensitive);
		units[1].ShouldContain(
			"EXECUTE IMMEDIATE",
			Case.Sensitive,
			"the block must arrive whole; a ';' split would have severed its body");
		units[2].ShouldContain("CREATE INDEX IX_Widgets");
	}

	[Fact]
	public void Split_TheShippedOracleOutboxScripts_IntoExecutableUnits()
	{
		// LIVENESS against the REAL shipped scripts, not a fixture: the helper exists so the Oracle
		// outbox fixture can provision from them, and a splitter that works only on a hand-written
		// example is no use. These scripts carry both plain DDL and PL/SQL blocks.
		var units = new[]
		{
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/001_CreateOutboxSchema.sql",
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/002_MakeOutboxTenantTotal.sql",
			"src/Excalibur/Excalibur.Outbox.Oracle/Scripts/003_CarryTenantOnDeadLetters.sql",
		}.SelectMany(ShippedSchemaScript.ReadOracleUnits).ToList();

		units.ShouldNotBeEmpty();

		// No unit may be a fragment. A shredded block shows up as a piece that opens a construct it
		// never closes, which is exactly what a ';' split produces and what this guards against.
		foreach (var unit in units)
		{
			if (unit.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase)
				|| unit.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase))
			{
				unit.ShouldEndWith(
					"END;",
					Case.Sensitive,
					$"a PL/SQL unit must arrive whole, but this one was cut: {unit[..Math.Min(60, unit.Length)]}");
			}
			else
			{
				unit.ShouldNotContain(
					"END;",
					Case.Sensitive,
					$"a plain statement carrying END; is a severed block fragment: {unit[..Math.Min(60, unit.Length)]}");
			}
		}

		units.ShouldContain(
			static u => u.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase),
			"001 creates the outbox tables, so at least one unit must be a CREATE TABLE");
	}

	/// <summary>Writes an Oracle-script fixture beside the test binary and returns its name.</summary>
	/// <param name="sql">The fixture's contents.</param>
	/// <returns>The name to pass to <see cref="ShippedSchemaScript.ReadOracleUnits"/>.</returns>
	private static string WriteOracleFixture(string sql)
	{
		var name = $"oracle-script-fixture-{Guid.NewGuid():N}.sql";
		File.WriteAllText(Path.Combine(AppContext.BaseDirectory, name), sql);
		return name;
	}

	/// <summary>
	/// Writes a sqlcmd-template fixture beside the test binary and returns the name
	/// <see cref="ShippedSchemaScript.Resolve"/> locates it by, since that walks up from this directory.
	/// </summary>
	/// <param name="sql">The fixture's contents.</param>
	/// <returns>The name to pass to <see cref="ShippedSchemaScript.ReadSqlCmdBatches"/>.</returns>
	private static string WriteSqlCmdFixture(string sql)
	{
		var name = $"sqlcmd-template-fixture-{Guid.NewGuid():N}.sql";
		File.WriteAllText(Path.Combine(AppContext.BaseDirectory, name), sql);
		return name;
	}

	private static string RepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Excalibur.sln")))
			{
				return directory.FullName;
			}
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}'. This suite reads the shipped scripts "
			+ "from the source tree, so it fails rather than reporting success over an empty set.");
	}
}
