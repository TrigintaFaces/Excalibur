// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Testcontainers.Oracle;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Direct Dapper dependency diagnostic: measures default DateTimeOffset mapping against a real
/// Oracle TIMESTAMP WITH TIME ZONE column at three offsets. The current Oracle outbox provider uses
/// statically bound commands and OracleDataReader.GetDateTimeOffset instead of this generic mapper.
/// Run this class in a fresh isolated process: OracleOutboxStoreContainerFixture installs a global
/// Dapper DateTimeOffset type handler, so an assembly-wide run cannot establish default behavior.
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

		// WRITE — direct Dapper anonymous-object parameter binding, without a custom handler.
		await connection.ExecuteAsync(
			"INSERT INTO DTO_PROBE (ID, TS) VALUES (:Id, :Ts)",
			new { Id = id, Ts = written }).ConfigureAwait(false);

		// READ — direct Dapper scalar mapping; independent of the provider static-reader path.
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
				+ $"this direct dependency probe observed. Exception: {ex}", ex);
		}

		read.ToUniversalTime().ShouldBe(
			written.ToUniversalTime(),
			TimeSpan.FromMilliseconds(1),
			$"the instant must survive the round trip at offset={offsetHours:+00;-00}h under Dapper's default "
			+ "(no custom handler) DateTimeOffset mapping in this isolated dependency probe.");
	}

	private sealed class TestFixtureDiagnosticException(string message, Exception inner) : Exception(message, inner);
}
