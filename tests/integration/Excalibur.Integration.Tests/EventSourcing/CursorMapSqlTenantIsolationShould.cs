// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data;
using Excalibur.Integration.Tests.Data.DeadLetter;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.EventSourcing;

/// <summary>
/// Implements <see cref="ITenantContext"/> directly, inheriting no first-party base, so these arms bind
/// the interface's own requirement rather than re-testing an inherited convenience.
/// </summary>
internal sealed class FixedCursorTenantContext(string tenantId) : ITenantContext
{
	public string? TenantId { get; } = tenantId;

	public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}

/// <summary>
/// Real-Postgres tenant isolation for <see cref="PostgresCursorMapStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The unit arm for this contract constructs the in-memory store and says so plainly: it is not evidence
/// for the SQL providers, because no unit test can bind them without a real database. Until these arms
/// existed the claim that every implementation partitions was carried by a doc comment and nothing else,
/// so a regression in either SQL store left the unit arm green while one tenant's cursor advanced another's.
/// </para>
/// <para>
/// Direction of failure is what makes this worth a container. A cursor moved FORWARD by another tenant
/// makes the projector skip events it never processed - data missing from a read model, permanently, with
/// nothing to alert on. A cursor moved backward merely reprojects, which an idempotent projection absorbs.
/// </para>
/// </remarks>
[Collection(PostgresTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Postgres")]
public sealed class PostgresCursorMapTenantIsolationShould(PostgresContainerFixture fixture)
{
	private const string SharedProjectionName = "OrderSummaryProjection";
	private const string StreamId = "stream-1";

	private async Task<NpgsqlDataSource> CreateProvisionedDataSourceAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			fixture.InitializationError ?? "Postgres container must be available - this lock is never skipped.");

		var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

		// The store does not provision its own table; the DDL is published in its XML docs and a consumer
		// runs it. Creating it here from that same DDL is what makes this a test of the store's statements
		// rather than of a schema invented by the test.
		await using var command = dataSource.CreateCommand(
			"CREATE TABLE IF NOT EXISTS projection_cursor_maps ("
			+ "    tenant_id VARCHAR(256) NOT NULL,"
			+ "    projection_name VARCHAR(256) NOT NULL,"
			+ "    stream_id VARCHAR(256) NOT NULL,"
			+ "    position BIGINT NOT NULL,"
			+ "    CONSTRAINT pk_projection_cursor_maps PRIMARY KEY (tenant_id, projection_name, stream_id));");
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

		return dataSource;
	}

	private static PostgresCursorMapStore StoreFor(NpgsqlDataSource dataSource, string tenantId) =>
		new(dataSource, NullLogger<PostgresCursorMapStore>.Instance, new FixedCursorTenantContext(tenantId));

	[Fact]
	public async Task NotExposeOneTenantsCursorToAnotherTenant()
	{
		await using var dataSource = await CreateProvisionedDataSourceAsync();

		// Distinct ids per run, so the arm does not depend on the table being empty and two runs against
		// the same container cannot make each other pass.
		var suffix = Guid.NewGuid().ToString("N");
		var tenantA = StoreFor(dataSource, "tenant-a-" + suffix);
		var tenantB = StoreFor(dataSource, "tenant-b-" + suffix);

		await tenantA.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			TestContext.Current.CancellationToken);

		var tenantBCursor = await tenantB.GetCursorMapAsync(
			SharedProjectionName,
			TestContext.Current.CancellationToken);

		tenantBCursor.ShouldBeEmpty(
			"tenant B has projected nothing, so its cursor map must be empty. Reading tenant A's position "
			+ "of 500 makes B's projector skip every event below that mark - never projected, and nothing "
			+ "reports an error.");
	}

	[Fact]
	public async Task StillReturnATenantsOwnSavedCursor()
	{
		// The liveness half. A store that returned an empty map to everyone satisfies the arm above
		// perfectly while losing all projection progress, restarting every projection from zero.
		await using var dataSource = await CreateProvisionedDataSourceAsync();

		var store = StoreFor(dataSource, "tenant-a-" + Guid.NewGuid().ToString("N"));

		await store.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			TestContext.Current.CancellationToken);

		var cursor = await store.GetCursorMapAsync(
			SharedProjectionName,
			TestContext.Current.CancellationToken);

		cursor.ShouldContainKeyAndValue(StreamId, 500);
	}
}

/// <summary>
/// Real-SQL-Server tenant isolation for <see cref="SqlServerCursorMapStore"/>.
/// </summary>
/// <remarks>
/// The same contract and the same arms as the Postgres class. It is a separate class because the two
/// providers need different containers, and the setup is written out rather than shared because the DDL
/// differs and the point of these arms is to run each provider's own statements against its own engine.
/// </remarks>
[Collection(SqlServerDeadLetterTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCursorMapTenantIsolationShould(SqlServerContainerFixture fixture)
{
	private const string SharedProjectionName = "OrderSummaryProjection";
	private const string StreamId = "stream-1";

	private async Task<Func<SqlConnection>> CreateProvisionedFactoryAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue(
			fixture.InitializationError ?? "SQL Server container must be available - this lock is never skipped.");

		var connectionString = fixture.ConnectionString;

		await using (var connection = new SqlConnection(connectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = connection.CreateCommand();

			// From the DDL published in the store's own XML docs, for the same reason as the Postgres arm.
			command.CommandText =
				"IF OBJECT_ID('ProjectionCursorMaps', 'U') IS NULL "
				+ "CREATE TABLE ProjectionCursorMaps ("
				+ "    TenantId NVARCHAR(256) NOT NULL,"
				+ "    ProjectionName NVARCHAR(256) NOT NULL,"
				+ "    StreamId NVARCHAR(256) NOT NULL,"
				+ "    Position BIGINT NOT NULL,"
				+ "    CONSTRAINT PK_ProjectionCursorMaps PRIMARY KEY (TenantId, ProjectionName, StreamId));";
			_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		return () => new SqlConnection(connectionString);
	}

	private static SqlServerCursorMapStore StoreFor(Func<SqlConnection> factory, string tenantId) =>
		new(factory, NullLogger<SqlServerCursorMapStore>.Instance, new FixedCursorTenantContext(tenantId));

	[Fact]
	public async Task NotExposeOneTenantsCursorToAnotherTenant()
	{
		var factory = await CreateProvisionedFactoryAsync();

		var suffix = Guid.NewGuid().ToString("N");
		var tenantA = StoreFor(factory, "tenant-a-" + suffix);
		var tenantB = StoreFor(factory, "tenant-b-" + suffix);

		await tenantA.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			TestContext.Current.CancellationToken);

		var tenantBCursor = await tenantB.GetCursorMapAsync(
			SharedProjectionName,
			TestContext.Current.CancellationToken);

		tenantBCursor.ShouldBeEmpty(
			"tenant B has projected nothing, so its cursor map must be empty. Reading tenant A's position "
			+ "of 500 makes B's projector skip every event below that mark - never projected, and nothing "
			+ "reports an error.");
	}

	[Fact]
	public async Task StillReturnATenantsOwnSavedCursor()
	{
		var factory = await CreateProvisionedFactoryAsync();

		var store = StoreFor(factory, "tenant-a-" + Guid.NewGuid().ToString("N"));

		await store.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			TestContext.Current.CancellationToken);

		var cursor = await store.GetCursorMapAsync(
			SharedProjectionName,
			TestContext.Current.CancellationToken);

		cursor.ShouldContainKeyAndValue(StreamId, 500);
	}
}
