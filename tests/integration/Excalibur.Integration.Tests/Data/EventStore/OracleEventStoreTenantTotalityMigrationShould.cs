// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// dmhvj3 — real-Oracle lock for the shipped
/// <c>Scripts/004_MakeEventTenantTotal.sql</c> upgrade script, which no test executed. Oracle DDL and
/// PL/SQL syntax is validated by the server and by nothing else; a defect in the shipped script is
/// otherwise found by a consumer running it.
/// </summary>
/// <remarks>
/// <para>
/// Follows the same shape as <c>OracleOutboxTenantTotalityShould</c>/<c>ShippedOracleOutboxSchema</c> (the
/// working pattern this bead names), including its PL/SQL-aware splitter (a lone <c>/</c> terminates an
/// anonymous block; <c>;</c> terminates a plain statement).
/// </para>
/// <para>
/// <b>A real defect this lock found and fixed, exactly per the bead's purpose</b>: the shipped script's
/// Step 3 used to be a bare <c>ALTER TABLE ... MODIFY (TENANTID ... NOT NULL)</c> with a comment claiming
/// re-running it against an already-NOT-NULL column is a no-op. Measured against real Oracle
/// (<c>BeSafeToRunTheMigrationTwice</c>, first written against the bare form): it is NOT a no-op — Oracle
/// raises ORA-01442 unconditionally. Fixed by wrapping Step 3 in an anonymous PL/SQL block that swallows
/// exactly that error code, mirroring the guard the SQL Server sibling script already used
/// (<c>IF EXISTS (... IS_NULLABLE = 'YES') BEGIN ALTER ... END</c>). That fix is what made the PL/SQL-aware
/// splitter necessary here, not a premise of the original bead.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreTenantTotalityMigrationShould(OracleEventStoreContainerFixture fixture)
{
	private const string Sentinel = "__untenanted__";

	private readonly OracleEventStoreContainerFixture _fixture = fixture;

	/// <summary>
	/// Both arms: a row written as legacy NULL before the migration reads back as the sentinel after it,
	/// a real tenant's row is untouched, and the column ends up closed (NOT NULL).
	/// </summary>
	[Fact]
	public async Task BackfillALegacyNullTenantWhileLeavingARealTenantAlone()
	{
		await PrepareLegacyShapeAsync().ConfigureAwait(false);

		await ExecuteAsync(
			$"INSERT INTO {_fixture.TableName} (EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, VERSION, EVENTTIMESTAMP, TENANTID) "
			+ "VALUES ('e-legacy', 'agg-legacy', 'T', 'Evt', 0, SYSTIMESTAMP, NULL)").ConfigureAwait(false);
		await ExecuteAsync(
			$"INSERT INTO {_fixture.TableName} (EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, VERSION, EVENTTIMESTAMP, TENANTID) "
			+ "VALUES ('e-tenanted', 'agg-tenanted', 'T', 'Evt', 0, SYSTIMESTAMP, 'acme')").ConfigureAwait(false);

		await RunShippedMigrationAsync().ConfigureAwait(false);

		(await TenantOfAsync("e-legacy").ConfigureAwait(false)).ShouldBe(
			Sentinel, "a row written as legacy NULL before the migration must read back as the sentinel after it");
		(await TenantOfAsync("e-tenanted").ConfigureAwait(false)).ShouldBe(
			"acme", "the backfill must touch only genuinely untenanted rows — a real tenant is not rewritten");
		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeFalse(
			"the migration must close the column, not merely rewrite the values in it");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>The shipped migration is safe to run twice — idempotent on an already-converged database.</summary>
	[Fact]
	public async Task BeSafeToRunTheMigrationTwice()
	{
		await PrepareLegacyShapeAsync().ConfigureAwait(false);

		await ExecuteAsync(
			$"INSERT INTO {_fixture.TableName} (EVENTID, AGGREGATEID, AGGREGATETYPE, EVENTTYPE, VERSION, EVENTTIMESTAMP, TENANTID) "
			+ "VALUES ('e-legacy', 'agg-legacy', 'T', 'Evt', 0, SYSTIMESTAMP, NULL)").ConfigureAwait(false);

		await RunShippedMigrationAsync().ConfigureAwait(false);
		await RunShippedMigrationAsync().ConfigureAwait(false);

		(await TenantOfAsync("e-legacy").ConfigureAwait(false)).ShouldBe(Sentinel, "a second run must leave converged data exactly as it was");
		(await ScalarAsync<decimal>($"SELECT COUNT(*) FROM {_fixture.TableName}").ConfigureAwait(false)).ShouldBe(
			1m, "a second run must not duplicate or drop rows");
		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeFalse("the column must remain closed after a second run");

		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>Brings up the fresh-install schema, then re-opens TENANTID to the pre-migration (nullable) shape.</summary>
	private async Task PrepareLegacyShapeAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and of Oracle's own dialect rules, and is "
			+ "deliberately never skipped — a green run that never reached a database would certify nothing.");
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await ExecuteAsync($"ALTER TABLE {_fixture.TableName} MODIFY (TENANTID DEFAULT NULL)").ConfigureAwait(false);
		await ExecuteAsync($"ALTER TABLE {_fixture.TableName} MODIFY (TENANTID NULL)").ConfigureAwait(false);

		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeTrue(
			"the legacy shape must actually be re-established, or the migration below proves nothing");
	}

	/// <summary>Loads and runs the shipped 004 script, splitting it the way SQL*Plus does.</summary>
	private async Task RunShippedMigrationAsync()
	{
		var path = ResolveShippedScriptPath();
		var sql = await File.ReadAllTextAsync(path).ConfigureAwait(false);

		foreach (var statement in SplitStatements(sql))
		{
			await ExecuteAsync(statement).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Splits an Oracle script the way SQL*Plus does: a lone <c>/</c> terminates a PL/SQL block, a
	/// <c>;</c> terminates a plain statement. A buffer that has begun a <c>DECLARE</c> or <c>BEGIN</c>
	/// ignores semicolons entirely and waits for its <c>/</c>. Mirrors
	/// <c>ShippedOracleOutboxSchema.SplitStatements</c> in the outbox test project.
	/// </summary>
	private static IEnumerable<string> SplitStatements(string script)
	{
		var buffer = new System.Text.StringBuilder();
		var inPlSqlBlock = false;

		foreach (var rawLine in script.Split('\n'))
		{
			var line = rawLine.TrimEnd('\r');
			var trimmed = line.Trim();

			// WHENEVER is a SQL*Plus directive, not SQL: skipped for the same reason as a comment.
			if (buffer.Length == 0
				&& (trimmed.Length == 0
					|| trimmed.StartsWith("--", StringComparison.Ordinal)
					|| trimmed.StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			if (trimmed == "/")
			{
				var block = buffer.ToString().Trim();
				buffer.Clear();
				inPlSqlBlock = false;
				if (block.Length > 0)
				{
					yield return block;
				}

				continue;
			}

			if (buffer.Length == 0
				&& (trimmed.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase)
					|| trimmed.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase)))
			{
				inPlSqlBlock = true;
			}

			_ = buffer.Append(line).Append('\n');

			if (!inPlSqlBlock && trimmed.EndsWith(';'))
			{
				var statement = buffer.ToString().Trim().TrimEnd(';').Trim();
				buffer.Clear();
				if (statement.Length > 0)
				{
					yield return statement;
				}
			}
		}

		var tail = buffer.ToString().Trim().TrimEnd(';').Trim();
		if (tail.Length > 0)
		{
			yield return tail;
		}
	}

	private static string ResolveShippedScriptPath()
	{
		const string RelativePath = "src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/004_MakeEventTenantTotal.sql";

		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, RelativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			$"The shipped Oracle event-store migration was not found by walking up from '{AppContext.BaseDirectory}' "
			+ $"looking for '{RelativePath}'.");
	}

	private Task<string?> TenantOfAsync(string eventId) =>
		ScalarAsync<string>($"SELECT TENANTID FROM {_fixture.TableName} WHERE EVENTID = '{eventId}'");

	private async Task<bool> IsTenantColumnNullableAsync() =>
		await ScalarAsync<string>(
			$"SELECT NULLABLE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = '{_fixture.TableName}' AND COLUMN_NAME = 'TENANTID'")
			.ConfigureAwait(false)
			== "Y";

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
#pragma warning disable CA2100 // the shipped script's own text and fixed literals in this file; not user input
		await using var command = new OracleCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private async Task<T?> ScalarAsync<T>(string sql)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
#pragma warning disable CA2100
		await using var command = new OracleCommand(sql, connection);
#pragma warning restore CA2100
		var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		return result is DBNull or null ? default : (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
	}
}
