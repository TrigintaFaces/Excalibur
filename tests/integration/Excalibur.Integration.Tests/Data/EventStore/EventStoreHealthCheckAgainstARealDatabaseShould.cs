// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// The event-store health check reports Unhealthy against a REAL database whose event table is missing
/// a column the read statement binds.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this adds that the unit arms cannot.</b> The unit arms drive a fake reader: they establish
/// that a reader which THROWS is reported Unhealthy. They say nothing about the link the check actually
/// depends on — that a table missing a read-path column makes the reader throw at all. That link was
/// assumed, and it is the whole premise of probing for schema rather than for connectivity. These arms
/// bind it against a real database engine.
/// </para>
/// <para>
/// <b>SCOPE, and read it before citing these arms as the proof. They are not.</b> SQLite is a real
/// engine and needs no container, so these arms are inherently non-skipped — but SQLite is not the
/// engine whose parse-time behaviour the guarantee rests on. What these establish is that the CHECK
/// reports correctly over a truncated table on a provider that registers a global-stream reader. What
/// they do NOT establish is that SQL Server or PostgreSQL reject the statement at parse time on an empty
/// table, which is the claim the check's own documentation makes. That still owes an arm against those
/// engines, and this file does not discharge it.
/// </para>
/// <para>
/// <b>And there is a sharper reason the SQL Server arm is the one that matters.</b> SQLite's reader
/// binds TEN columns and <c>ArchivedAt</c> is not among them -- the exact column whose absence produced
/// the consumer incident this check was built for. So these arms cannot touch the originating defect's
/// column at all, not because of parse-time semantics but because on this provider that column is not
/// on the read path. Anyone deciding whether the remaining arm is worth the infrastructure should weigh
/// that rather than the engine-fidelity argument alone.
/// </para>
/// <para>
/// <b>They also cannot bind the absent-table case, because on this provider that case does not
/// exist.</b> The SQLite reader provisions the events table on its read path, so a missing table is
/// created and the check reports Healthy. The third arm below pins that rather than leaving it implied
/// by the healthy arm's reliance on the same auto-create.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Sqlite")]
public sealed class EventStoreHealthCheckAgainstARealDatabaseShould : IDisposable
{
	// Every column the SQLite global-stream read binds, EXCEPT Timestamp. Reproduced from the shipped
	// schema rather than invented, so the only difference from a correct database is the one column.
	//
	// DIFF THIS AGAINST SqliteTableInitializer.EventsTableDdl IF YOU TOUCH THE READ PATH. It is a
	// hand-copied schema with nothing tying it to its source, and the drift fails in the quiet
	// direction: if the reader gains a column, this fixture becomes "missing TWO columns", SQLite
	// reports whichever it resolves first, the Timestamp assertion very likely still passes, and the
	// arm silently stops testing the single-column case it was written for.
	//
	// THE PRIMARY KEY MUST STAY NAMED GlobalPosition. The initializer seeds the position counter by
	// reading MAX(GlobalPosition) from this pre-created table, so a differently named key fails
	// initialization FIRST and reds the arm on the wrong cause, with a message pointing nowhere near
	// the column under test.
	private const string TruncatedEventsDdl = """
		CREATE TABLE [Events] (
		    GlobalPosition INTEGER PRIMARY KEY,
		    EventId TEXT NOT NULL,
		    AggregateId TEXT NOT NULL,
		    AggregateType TEXT NOT NULL,
		    EventType TEXT NOT NULL,
		    EventData BLOB,
		    Metadata BLOB,
		    Version INTEGER NOT NULL,
		    TenantId TEXT NOT NULL
		);
		""";

	private readonly List<string> _paths = [];

	[Fact]
	public async Task ReportUnhealthyWhenTheEventTableIsMissingAReadPathColumn()
	{
		var connectionString = await NewDatabaseAsync(withTruncatedTable: true).ConfigureAwait(false);

		var result = await RunCheckAsync(connectionString).ConfigureAwait(false);

		result.Status.ShouldBe(
			HealthStatus.Unhealthy,
			"the database answered and the table exists, so a connectivity probe would have reported "
			+ "Healthy here. That is the defect this check was built to remove: a reachable database "
			+ "whose event table cannot serve the read path.");

		result.Description.ShouldContain(
			"missing a column",
			Shouldly.Case.Insensitive,
			"the description must point an operator at the schema, not at the network.");

		// THE DISCRIMINATING ASSERTION. Everything above is satisfied by an Unhealthy for ANY reason --
		// a missing position table, a locked file, a bad path -- because "missing a column" is our own
		// message text and appears on every Unhealthy we emit. Only the underlying engine error proves
		// the verdict came from the column this arm removed.
		_ = result.Exception.ShouldNotBeNull(
			"the engine's own error must be carried, or an operator cannot tell which column is absent.");

		result.Exception!.Message.ShouldContain(
			"Timestamp",
			Shouldly.Case.Insensitive,
			"the failure must name the column this arm deliberately omitted. Without this the arm passes "
			+ "for an Unhealthy caused by anything at all, which is the proxy-not-requirement trap.");
	}

	[Fact]
	public async Task ReportHealthyAgainstTheShippedSchemaOnAnEmptyDatabase()
	{
		// LIVENESS, and it is the arm that stops the one above being satisfied by a check that reports
		// Unhealthy against every SQLite database. It also pins the claim that the probe needs NO DATA:
		// the store auto-creates its table here and not one event is ever appended.
		var connectionString = await NewDatabaseAsync(withTruncatedTable: false).ConfigureAwait(false);

		var result = await RunCheckAsync(connectionString).ConfigureAwait(false);

		result.Status.ShouldBe(
			HealthStatus.Healthy,
			"a correctly provisioned, EMPTY event store must report Healthy. If this is Unhealthy the "
			+ "check is useless in the opposite direction, and a permanently-red check gets deleted "
			+ "from the readiness probe, leaving nothing.");

		result.Description.ShouldContain(
			"event table",
			Shouldly.Case.Insensitive,
			"a green must claim the right thing: that a read of the EVENT TABLE was served, which a "
			+ "bare connectivity probe would not establish.");

		result.Exception.ShouldBeNull("a healthy verdict must carry no fault.");
	}

	/// <summary>
	/// The absent-table case is NOT detected on this provider, and that is pinned rather than implied.
	/// </summary>
	/// <remarks>
	/// The SQLite reader provisions the events table on its read path, so a database that was never
	/// provisioned is created by the probe and reports Healthy -- truthfully, since a read was served.
	/// This arm exists because the healthy arm above relies on that same auto-create as a convenience,
	/// which makes it easy to read the absent-table case as covered when it is the opposite. Measured
	/// across the five shipped readers, SQLite is the only one that does this; the other four go
	/// straight to their select and would report Unhealthy. If that ever changes here, this arm fails
	/// and the claim in the check's own documentation must be revisited with it.
	/// </remarks>
	[Fact]
	public async Task ReportHealthyWhenTheTableIsAbsentBecauseThisProviderCreatesItOnRead()
	{
		var connectionString = await NewDatabaseAsync(withTruncatedTable: false).ConfigureAwait(false);

		// The arm's whole point is the TRANSITION, and without these two observations its body is
		// identical to the healthy arm's -- two tests with the same mechanics and different comments,
		// which detects nothing. Asserting absent-then-present is what makes this a test of the
		// provisioning behaviour rather than a second copy of the happy path.
		(await EventsTableExistsAsync(connectionString).ConfigureAwait(false)).ShouldBeFalse(
			"the fixture must start with NO events table, or this arm is not about an absent table.");

		var result = await RunCheckAsync(connectionString).ConfigureAwait(false);

		result.Status.ShouldBe(
			HealthStatus.Healthy,
			"not an endorsement -- a record. A provider that provisions on read cannot distinguish an "
			+ "unprovisioned deployment from a correctly provisioned empty one, so the absent-table half "
			+ "of the check's documented claim does not hold here.");

		(await EventsTableExistsAsync(connectionString).ConfigureAwait(false)).ShouldBeTrue(
			"and this is the mechanism: the HEALTH CHECK created the table it then reported on. A "
			+ "readiness probe with a side effect on the schema is the reason the absent-table case "
			+ "cannot be detected on this provider.");
	}

	// Reads SQLite's own catalogue rather than attempting a select, so the observation itself cannot
	// create or alter what it is observing.
	private static async Task<bool> EventsTableExistsAsync(string connectionString)
	{
		await using var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Events'";
		return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture) > 0;
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();

		foreach (var path in _paths)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException)
			{
				// A temp file left behind is not a test failure.
			}
		}
	}

	private static async Task<HealthCheckResult> RunCheckAsync(string connectionString)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseSqlite(sqlite =>
			sqlite.ConnectionString = connectionString)));
		_ = services.AddHealthChecks().AddEventStoreHealthCheck();

		await using var provider = services.BuildServiceProvider();

		// Resolved through the real registration rather than constructed, so the arm binds what a
		// consumer's host actually gets.
		var registration = provider
			.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.Single(r => r.Name == "event-store");

		return await registration.Factory(provider)
			.CheckHealthAsync(new HealthCheckContext { Registration = registration }, CancellationToken.None)
			.ConfigureAwait(false);
	}

	// When withTruncatedTable is false the table is NOT created here: the store auto-creates the shipped
	// schema on first use, so the healthy arm measures the real provisioning path rather than a copy of
	// it. When true, the table is pre-created WITHOUT one read-path column, and the store's
	// CREATE TABLE IF NOT EXISTS then leaves it alone -- which is how a consumer whose database predates
	// a column arrives in this state.
	private async Task<string> NewDatabaseAsync(bool withTruncatedTable)
	{
		var path = Path.Combine(Path.GetTempPath(), $"excalibur-health-{Guid.NewGuid():N}.db");
		_paths.Add(path);
		var connectionString = $"Data Source={path}";

		if (withTruncatedTable)
		{
			await using var connection = new SqliteConnection(connectionString);
			await connection.OpenAsync().ConfigureAwait(false);
			await using var command = connection.CreateCommand();

			// A compile-time constant declared in this file -- no caller supplies it.
			command.CommandText = TruncatedEventsDdl;
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		return connectionString;
	}
}
