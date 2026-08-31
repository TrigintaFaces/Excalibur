// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Outbox.Postgres;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Locks the TOTALITY of <c>public.outbox.tenant_id</c> against a real Postgres, for the fresh-install
/// schema, the upgrade script, and the staging path that has to keep working across both.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is that there is exactly ONE way to say "this message has no tenant" — the
/// reserved <c>__untenanted__</c> sentinel — rather than two (the sentinel and SQL <c>NULL</c>). While
/// both spellings exist, every read has to fold them together, and a fold applied on one path and not
/// another is the defect class this guards.
/// </para>
/// <para>
/// It runs against a real database because totality is a property of the SCHEMA, not of any C# type:
/// whether a <c>DEFAULT</c> fires on an omitted column, whether <c>SET NOT NULL</c> is accepted after a
/// backfill, and whether the upgrade is safe to re-run are answered by the server and by nothing else.
/// </para>
/// <para>
/// It provisions from the DDL the package actually SHIPS rather than from the container fixture's
/// hand-written copy — see <see cref="ShippedPostgresOutboxSchema"/> for why that distinction is the
/// whole point.
/// </para>
/// <para>
/// The schema change and the staging change are locked TOGETHER on purpose. Making the column total
/// while the staging path still binds the caller's raw null tenant would reject every untenanted stage;
/// the arm that stages through the real request type is what makes that inexpressible rather than
/// merely unlikely.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously — a schema lock that
/// goes green by never executing is the failure mode it exists to prevent.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxTenantTotalityShould(PostgresOutboxStoreContainerFixture fixture)
	: IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string Sentinel = "__untenanted__";

	private readonly PostgresOutboxStoreContainerFixture _fixture = fixture;

	private string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	/// FRESH INSTALL: a writer that omits the tenant column entirely gets the sentinel, not NULL.
	/// </summary>
	[Fact]
	public async Task DefaultAnOmittedTenantToTheSentinelOnAFreshInstall()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, occurred_on)
			VALUES ('m-1', 'T', '\x01'::bytea, NOW());
			""");

		var stored = await ScalarAsync<string>("SELECT tenant_id FROM public.outbox WHERE message_id = 'm-1';");

		stored.ShouldBe(
			Sentinel,
			"a message staged without a tenant must be stored as the untenanted PARTITION, not as an absent value");

		(await IsTenantColumnNullableAsync()).ShouldBeFalse(
			"the fresh-install schema must not be able to represent a NULL tenant at all");
	}

	/// <summary>
	/// FRESH INSTALL, liveness: a real tenant is stored verbatim and is not absorbed by the default.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above deliberately. A schema that stamped EVERY row with the sentinel would
	/// satisfy "no row holds NULL" perfectly while destroying tenant identity, and nothing else here
	/// would notice.
	/// </remarks>
	[Fact]
	public async Task StoreARealTenantVerbatimRatherThanApplyingTheDefault()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, tenant_id, occurred_on)
			VALUES ('m-1', 'T', '\x01'::bytea, 'acme', NOW());
			""");

		var stored = await ScalarAsync<string>("SELECT tenant_id FROM public.outbox WHERE message_id = 'm-1';");

		stored.ShouldBe("acme", "a real tenant must survive the write unchanged");
	}

	/// <summary>
	/// FRESH INSTALL: the closed column actually refuses NULL, rather than merely looking closed.
	/// </summary>
	[Fact]
	public async Task RejectAnExplicitNullTenantOnceTheColumnIsTotal()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		var write = async () => await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, tenant_id, occurred_on)
			VALUES ('m-null', 'T', '\x01'::bytea, NULL, NOW());
			""");

		_ = await write.ShouldThrowAsync<PostgresException>(
			"an explicit NULL tenant must be refused by the database, not silently accepted");
	}

	/// <summary>
	/// THE COUPLING ARM: the real staging request writes an UNTENANTED message into the total column.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that fails if the schema is made total without the staging path being changed to
	/// match. The request used to bind the caller's raw <c>tenantId</c> argument, which is null for an
	/// untenanted message; against a NOT NULL column that is a not-null violation on every untenanted
	/// stage — the outbox would stop accepting the most common message shape there is.
	/// </para>
	/// <para>
	/// It drives the shipped request type rather than hand-written SQL precisely because the binding is
	/// the thing under test. A hand-written INSERT here would pass regardless of what the store does.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task StageAnUntenantedMessageThroughTheRealRequestAndStoreTheSentinel()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		var request = NewInsertRequest("m-staged", tenantId: null);

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await request.ResolveAsync(connection);

		var stored = await ScalarAsync<string>("SELECT tenant_id FROM public.outbox WHERE message_id = 'm-staged';");

		stored.ShouldBe(
			Sentinel,
			"the staging path must bind the untenanted PARTITION term, not the caller's raw null — binding "
			+ "the raw argument would fail the NOT NULL constraint on every untenanted stage");
	}

	/// <summary>
	/// THE COUPLING ARM, liveness: the same request preserves a real tenant.
	/// </summary>
	[Fact]
	public async Task StageATenantedMessageThroughTheRealRequestAndPreserveTheTenant()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		var request = NewInsertRequest("m-tenanted", tenantId: "acme");

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await request.ResolveAsync(connection);

		var stored = await ScalarAsync<string>("SELECT tenant_id FROM public.outbox WHERE message_id = 'm-tenanted';");

		stored.ShouldBe("acme", "normalising the untenanted case must not touch a real tenant");
	}

	/// <summary>
	/// UPGRADE: rows written as NULL or blank before the migration read back as the sentinel after it, a
	/// real tenant's row is untouched, and the column ends up closed.
	/// </summary>
	/// <remarks>
	/// The blank row is included because the read path already treats a blank stored value as untenanted,
	/// so leaving it would satisfy NOT NULL while preserving the very split the migration exists to
	/// remove — a column that is total in the type system and still ambiguous in meaning.
	/// </remarks>
	[Fact]
	public async Task BackfillLegacyNullAndBlankTenantsWhileLeavingARealTenantAlone()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);
		await ShippedPostgresOutboxSchema.ReopenTenantColumnToLegacyShapeAsync(
			ConnectionString, TestContext.Current.CancellationToken);

		(await IsTenantColumnNullableAsync()).ShouldBeTrue(
			"the legacy shape must actually be re-established, or the migration below proves nothing");

		await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, tenant_id, occurred_on)
			VALUES ('legacy-null', 'T', '\x01'::bytea, NULL, NOW()),
			       ('legacy-blank', 'T', '\x01'::bytea, '   ', NOW()),
			       ('tenanted', 'T', '\x01'::bytea, 'acme', NOW());
			""");

		await ShippedPostgresOutboxSchema.RunMigrationAsync(ConnectionString, TestContext.Current.CancellationToken);

		(await TenantOfAsync("legacy-null")).ShouldBe(
			Sentinel, "a row written as NULL before the migration must read back as the sentinel after it");

		(await TenantOfAsync("legacy-blank")).ShouldBe(
			Sentinel, "a blank tenant is already READ as untenanted, so it must be STORED that way too");

		(await TenantOfAsync("tenanted")).ShouldBe(
			"acme", "the backfill must touch only genuinely untenanted rows — a real tenant is not rewritten");

		(await IsTenantColumnNullableAsync()).ShouldBeFalse(
			"the migration must close the column, not merely rewrite the values in it");
	}

	/// <summary>
	/// UPGRADE: the migration is safe to run twice, and against an already-converged database.
	/// </summary>
	/// <remarks>
	/// Deployment scripts get re-run — by a retried pipeline, or by an operator who cannot tell whether
	/// the first attempt finished. A migration that is only correct once is one that will be wrong in
	/// production.
	/// </remarks>
	[Fact]
	public async Task BeSafeToRunTheMigrationTwice()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);
		await ShippedPostgresOutboxSchema.ReopenTenantColumnToLegacyShapeAsync(
			ConnectionString, TestContext.Current.CancellationToken);

		await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, tenant_id, occurred_on)
			VALUES ('legacy-null', 'T', '\x01'::bytea, NULL, NOW());
			""");

		await ShippedPostgresOutboxSchema.RunMigrationAsync(ConnectionString, TestContext.Current.CancellationToken);
		await ShippedPostgresOutboxSchema.RunMigrationAsync(ConnectionString, TestContext.Current.CancellationToken);

		(await TenantOfAsync("legacy-null")).ShouldBe(
			Sentinel, "a second run must leave the converged data exactly as it was");

		(await ScalarAsync<long>("SELECT COUNT(*) FROM public.outbox;")).ShouldBe(
			1L, "a second run must not duplicate or drop rows");

		(await IsTenantColumnNullableAsync()).ShouldBeFalse("the column must remain closed after a second run");
	}

	/// <summary>
	/// UPGRADE: the migration is a no-op against a database that is already on the fresh shape.
	/// </summary>
	[Fact]
	public async Task BeANoOpAgainstAnAlreadyConvergedDatabase()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);

		await ExecuteAsync(
			"""
			INSERT INTO public.outbox (message_id, message_type, message_body, tenant_id, occurred_on)
			VALUES ('already', 'T', '\x01'::bytea, 'acme', NOW());
			""");

		await ShippedPostgresOutboxSchema.RunMigrationAsync(ConnectionString, TestContext.Current.CancellationToken);

		(await TenantOfAsync("already")).ShouldBe(
			"acme", "running the upgrade on a fresh install must change nothing at all");

		(await IsTenantColumnNullableAsync()).ShouldBeFalse("the column must still be closed");
	}

	private static InsertOutboxMessage NewInsertRequest(string messageId, string? tenantId) =>
		new(
			messageId: messageId,
			messageType: "T",
			messageMetadata: "{}",
			messageBody: [1],
			createdAt: DateTimeOffset.UtcNow,
			tenantId: tenantId,
			destination: null,
			correlationId: null,
			causationId: null,
			priority: 0,
			scheduledAt: null,
			partitionKey: null,
			groupKey: null,
			sequenceNumber: 0,
			targetTransports: null,
			isMultiTransport: false,
			outboxTableName: "public.outbox",
			sqlTimeOutSeconds: 30,
			cancellationToken: TestContext.Current.CancellationToken);

	private void RequireDocker() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and is deliberately never skipped — "
			+ "a green run that never reached a database would certify nothing.");

	private Task<string> TenantOfAsync(string messageId) =>
		ScalarAsync<string>($"SELECT tenant_id FROM public.outbox WHERE message_id = '{messageId}';");

	private async Task<bool> IsTenantColumnNullableAsync() =>
		await ScalarAsync<string>(
			"""
			SELECT is_nullable FROM information_schema.columns
			WHERE table_schema = 'public' AND table_name = 'outbox' AND column_name = 'tenant_id';
			""") == "YES";

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await connection.ExecuteAsync(sql);
	}

	private async Task<T> ScalarAsync<T>(string sql)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		return await connection.ExecuteScalarAsync<T>(sql);
	}
}
