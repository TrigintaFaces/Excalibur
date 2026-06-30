// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Metapackage Usage Sample
// ============================================================================
//
// Two ways to wire Excalibur for SQL Server:
//
//   (A)  AddExcaliburSqlServer(...)   -- the metapackage  (single line)
//   (B)  AddExcalibur(excalibur => ...) -- granular composition
//
// Both are shown below with identical business behavior. Which one to pick is
// summarized in the README.
//
// ============================================================================

using Excalibur.EventSourcing.SqlServer;
using Excalibur.Outbox.SqlServer;
using Excalibur.SqlServer;   // Provides ExcaliburSqlServerOptions
using Excalibur.EventSourcing.DependencyInjection;

var mode = Environment.GetEnvironmentVariable("SAMPLE_MODE") ?? "metapackage";
var connection =
	"Server=localhost,1434;Database=EventStore;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";

var builder = WebApplication.CreateBuilder(args);

if (string.Equals(mode, "metapackage", StringComparison.OrdinalIgnoreCase))
{
	// --------------------------------------------------------------------
	// (A) METAPACKAGE -- the single-line setup
	// --------------------------------------------------------------------
	// One call wires the full SqlServer stack:
	//   * Dispatch + EventSourcing + Outbox
	//   * Inbox, Saga, LeaderElection   (toggleable)
	//   * Compliance + Audit            (toggleable)
	//   * Data access executors
	//
	// Best choice when you want sensible defaults and the full production
	// stack.

	builder.Services.AddExcaliburSqlServer(sql =>
	{
		sql.ConnectionString    = connection;
		sql.UseInbox            = true;
		sql.UseSaga             = true;
		sql.UseLeaderElection   = true;
		sql.UseAuditLogging     = true;
		sql.UseCompliance       = true;
	});
}
else
{
	// --------------------------------------------------------------------
	// (B) GRANULAR -- compose the pieces you need, one builder at a time
	// --------------------------------------------------------------------
	// Best choice when you want to:
	//   * Trim the surface (e.g. no saga/inbox)
	//   * Mix providers (e.g. SqlServer ES + Postgres outbox)
	//   * Integrate third-party implementations
	//   * Hand-tune every subsystem's options

	builder.Services.AddExcalibur(excalibur =>
	{
		excalibur
			.AddEventSourcing(es =>
			{
				es.UseSqlServer(sql => sql.ConnectionString(connection));
				es.UseIntervalSnapshots(100);
			})
			.AddOutbox(outbox =>
			{
				outbox.UseSqlServer(sql => sql.ConnectionString(connection));
			});

		// Inbox / Saga / LeaderElection / Compliance / Audit each remain
		// opt-in. Add them by hand only when you need them.
	});
}

// c6wd6f: register event types for secure-by-default resolution
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.MapGet("/", () => Results.Text(
	$"""
	Metapackage Usage Sample

	Current mode: {mode.ToUpperInvariant()}

	Pick (A) when you want everything wired with sensible defaults:
	  SAMPLE_MODE=metapackage dotnet run
	Pick (B) when you want to compose only what you need:
	  SAMPLE_MODE=granular    dotnet run
	"""));

app.MapGet("/health", () => Results.Ok(new { status = "running", mode }));

app.Run();
