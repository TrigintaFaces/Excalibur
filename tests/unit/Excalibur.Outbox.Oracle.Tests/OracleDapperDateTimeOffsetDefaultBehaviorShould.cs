// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Testcontainers.Oracle;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// eg3gz8 diagnostic — measures Dapper's DEFAULT (no custom type handler) round-trip of
/// <see cref="DateTimeOffset"/> against a real Oracle <c>TIMESTAMP WITH TIME ZONE</c> column, which is
/// exactly what SHIPPED production code gets: <c>OracleOutboxStore</c>'s request classes (e.g.
/// <c>GetScheduledOutboxMessages</c>) map query results straight into a <c>DateTimeOffset</c>/<c>DateTimeOffset?</c>
/// property via <c>Dapper.SqlMapper.QueryAsync&lt;T&gt;</c> and register NO type handler anywhere in
/// <c>src/Excalibur/Excalibur.Outbox.Oracle</c>. <c>OracleOutboxStoreContainerFixture</c>'s static constructor
/// DOES register a custom <c>DateTimeOffsetTypeHandler</c> via <c>SqlMapper.AddTypeHandler</c> — a process-wide,
/// static Dapper registration — so every OTHER test in this assembly that has touched that fixture type runs
/// under a handler production code does not have. This class deliberately never references
/// <c>OracleOutboxStoreContainerFixture</c> and must be run in isolation
/// (<c>--filter FullyQualifiedName~OracleDapperDateTimeOffsetDefaultBehaviorShould</c>) so its own static
/// constructor is the only thing touched, and Dapper's built-in behavior — not the test fixture's
/// override — is what gets measured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleDapperDateTimeOffsetDefaultBehaviorShould : IAsyncLifetime
{
	private OracleContainer? _container;

	public async ValueTask InitializeAsync()
	{
		_container = new OracleBuilder()
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-dto-probe-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();
		await _container.StartAsync().ConfigureAwait(false);

		await using var connection = new OracleConnection(_container.GetConnectionString());
		await connection.OpenAsync().ConfigureAwait(false);
		await connection.ExecuteAsync(
			"CREATE TABLE DTO_PROBE (ID VARCHAR2(64) PRIMARY KEY, TS TIMESTAMP(7) WITH TIME ZONE NOT NULL)")
			.ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	private OracleConnection CreateConnection() => new(_container!.GetConnectionString());

	/// <summary>
	/// ARM 1 (non-zero offset) + ARM 2 (offset zero), combined: writes a value at each offset via a plain
	/// <c>OracleParameter</c> bind (mirrors <c>parameters.Add("ScheduledAt", scheduledAt, ...)</c> in
	/// <c>InsertOutboxMessage</c>/<c>ScheduleOutboxMessage</c>), then reads it back via Dapper's
	/// <c>QueryAsync&lt;T&gt;</c> generic object mapping — Dapper's DEFAULT handling, no custom type handler
	/// registered. Records whether this throws, silently coerces, or correctly round-trips the instant.
	/// </summary>
	[Theory]
	[InlineData(0)] // offset zero
	[InlineData(-5)] // non-zero offset
	[InlineData(9)] // non-zero offset, opposite sign
	public async Task RoundTripADateTimeOffsetThroughDapperDefaultMapping(int offsetHours)
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var id = $"probe-{offsetHours}-{Guid.NewGuid():N}";
		var written = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours));

		// WRITE — mirrors production: parameters.Add("X", DateTimeOffset value, ...), no custom handler.
		await connection.ExecuteAsync(
			"INSERT INTO DTO_PROBE (ID, TS) VALUES (:Id, :Ts)",
			new { Id = id, Ts = written }).ConfigureAwait(false);

		// READ — Dapper's generic QueryAsync<T> object mapping, exactly what GetScheduledOutboxMessages does.
		DateTimeOffset read;
		try
		{
			read = await connection.QuerySingleAsync<DateTimeOffset>(
				"SELECT TS AS Value FROM DTO_PROBE WHERE ID = :Id",
				new { Id = id }).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new TestFixtureDiagnosticException(
				$"Dapper's DEFAULT DateTimeOffset mapping THREW reading offset={offsetHours:+00;-00}h back from "
				+ $"Oracle TIMESTAMP WITH TIME ZONE, with no custom type handler registered — this is what "
				+ $"shipped production code (GetScheduledOutboxMessages et al.) would hit. Exception: {ex}", ex);
		}

		read.ToUniversalTime().ShouldBe(
			written.ToUniversalTime(),
			TimeSpan.FromMilliseconds(1),
			$"the instant must survive the round trip at offset={offsetHours:+00;-00}h under Dapper's default "
			+ "(no custom handler) DateTimeOffset mapping — the exact path production code runs.");
	}

	private sealed class TestFixtureDiagnosticException(string message, Exception inner) : Exception(message, inner);
}
