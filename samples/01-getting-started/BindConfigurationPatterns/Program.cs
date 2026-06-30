// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// BindConfiguration Patterns Sample
// ============================================================================
//
// This sample demonstrates the four supported patterns for driving Excalibur
// subsystem configuration from IConfiguration:
//
//   1. appsettings.json BindConfiguration("<Section>")          (preferred)
//   2. appsettings.Environment.json overrides                    (per-env)
//   3. Environment-variable overrides (ASPNETCORE_ / ConnectionStrings__)
//   4. IConfiguration.GetConnectionString(...) for connection strings
//
// Run with different environments to see the effect:
//   dotnet run                                       -> Development (default)
//   ASPNETCORE_ENVIRONMENT=Production dotnet run     -> loads Production json
//   ConnectionStrings__EventStore=... dotnet run     -> env-var override
//
// ============================================================================

using System.Text.RegularExpressions;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Outbox.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Environment-variable configuration source is added automatically by
// WebApplication; an explicit AddEnvironmentVariables() is only needed for
// non-default prefixes.

// ----------------------------------------------------------------------------
// 1. Connection strings via IConfiguration.GetConnectionString(...)
// ----------------------------------------------------------------------------
// Reads "ConnectionStrings:EventStore" from appsettings.json and falls back
// through each configuration source (appsettings.Environment.json, env vars,
// user secrets, Azure App Configuration, etc.)

var eventStoreCs = builder.Configuration.GetConnectionString("EventStore")
	?? throw new InvalidOperationException(
		"Missing 'ConnectionStrings:EventStore'. Set it in appsettings.json, " +
		"appsettings.{Environment}.json, or as the ConnectionStrings__EventStore env var.");

// ----------------------------------------------------------------------------
// 2. Every subsystem supports .BindConfiguration("<Section>")
// ----------------------------------------------------------------------------
// Options classes are bound automatically via IOptions<T>. They participate in
// IValidateOptions<T> + ValidateOnStart() so missing / invalid values fail at
// startup rather than at first use.

builder.Services.AddExcalibur(excalibur =>
{
	// ES options (schema, tables, snapshot strategy) come from appsettings.json
	// section "EventSourcing:Sql". You can still override any value in code.
	excalibur.AddEventSourcing(es =>
	{
		es.UseSqlServer(sql =>
		{
			sql.ConnectionString(eventStoreCs);
			sql.BindConfiguration("EventSourcing:Sql");
		});
	});

	// Outbox options (polling interval, batch size, retry policy) bound from
	// "Outbox" section.
	excalibur.AddOutbox(outbox =>
	{
		outbox.UseSqlServer(sql =>
		{
			sql.ConnectionString(eventStoreCs);
			sql.BindConfiguration("Outbox:Sql");
		});
	});
});

// c6wd6f: register event types for secure-by-default resolution
builder.Services.AddEventTypesFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.MapGet("/", (IConfiguration cfg) => Results.Json(new
{
	Environment = app.Environment.EnvironmentName,
	ConnectionString_Preview = PreviewConnectionString(eventStoreCs),
	EventSourcing = cfg.GetSection("EventSourcing").AsEnumerable()
		.Where(kvp => kvp.Value is not null).ToDictionary(k => k.Key, v => v.Value),
	Outbox = cfg.GetSection("Outbox").AsEnumerable()
		.Where(kvp => kvp.Value is not null).ToDictionary(k => k.Key, v => v.Value),
	Message =
		"Change ASPNETCORE_ENVIRONMENT or set ConnectionStrings__EventStore " +
		"to see overrides in effect."
}));

app.Run();

static string PreviewConnectionString(string cs) =>
	cs.Contains("Password=", StringComparison.OrdinalIgnoreCase)
		? PasswordRedactor.PasswordRegex().Replace(cs, "Password=***")
		: cs;

// Compile-time source-generated regex. Pattern checked at compile time; no
// reflection at startup. Follows the framework pattern from
// AzureKeyVaultCredentialStore / AuditLoggingMiddleware.
internal static partial class PasswordRedactor
{
	[GeneratedRegex(
		@"Password=[^;]+",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
		matchTimeoutMilliseconds: 1000)]
	public static partial Regex PasswordRegex();
}
