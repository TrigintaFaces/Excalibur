// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Dapper;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.EventSourcing.Migrations;

/// <summary>
/// Proves <see cref="SqlServerMigrator"/> compares the checksum it records against the script that
/// carries that migration id today, and refuses when they disagree.
/// </summary>
/// <remarks>
/// <para>
/// This runs against a real SQL Server because the checksum makes a round trip through an
/// <c>NVARCHAR(64)</c> column, and a hash that is truncated, padded, or re-cased on the way back is
/// indistinguishable in a mocked store from one that survived. The value only has to be right where it
/// is actually stored.
/// </para>
/// <para>
/// Both directions are bound. Refusing an edited body is the guarantee; ACCEPTING the same body under
/// the other line-ending convention is the one that protects consumers, because a false refusal here
/// stops a service from starting.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Migrations")]
public sealed class SqlServerMigrationDriftShould : IAsyncLifetime
{
	private readonly RequiredContainer _requiredContainer = new("SQL Server (Docker)");
	private MsSqlContainer? _container;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.WithName($"mssql-migration-drift-{Guid.NewGuid():N}")
				.WithPassword("Test@Pass123")
				.WithCleanUp(true)
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_requiredContainer.MarkStarted();
		}
		catch (Exception ex)
		{
			throw _requiredContainer.Failed(ex);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task RefuseToMigrateWhenAnAppliedScriptBodyHasChanged()
	{
		_requiredContainer.Require();

		var connectionString = await CreateDatabaseAsync().ConfigureAwait(false);

		var first = await MigratorFor(connectionString, MigrationDriftProbe.OriginalNamespace)
			.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		first.Success.ShouldBeTrue(first.ErrorMessage);
		first.AppliedMigrations.Count.ShouldBe(1);

		// The same migration id, a different body. This is the shape an operator is left in when a
		// numbered migration is edited after they have already run it.
		var second = await MigratorFor(connectionString, MigrationDriftProbe.EditedNamespace)
			.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		second.Success.ShouldBeFalse("An edited migration body must be refused, not applied on top of a database that ran the original.");
		second.ErrorMessage.ShouldNotBeNull();
		second.ErrorMessage.ShouldContain(MigrationDriftProbe.MigrationId);
		second.AppliedMigrations.ShouldBeEmpty();

		// Refusal must be total: the edited script adds a column, so its absence proves nothing ran.
		(await ProbeColumnCountAsync(connectionString).ConfigureAwait(false))
			.ShouldBe(1, "The refused migration must not have executed even partially.");
	}

	[Fact]
	public async Task AcceptTheSameScriptCheckedOutWithTheOtherLineEndings()
	{
		_requiredContainer.Require();
		MigrationDriftProbe.AssertRenderingsStillDiffer();

		var connectionString = await CreateDatabaseAsync().ConfigureAwait(false);

		var first = await MigratorFor(connectionString, MigrationDriftProbe.OriginalNamespace)
			.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		first.Success.ShouldBeTrue(first.ErrorMessage);

		// Byte-for-byte different, character-for-character identical: what `text=auto` hands a package
		// built on the other platform. Refusing this would stop a consumer's service on upgrade.
		var second = await MigratorFor(connectionString, MigrationDriftProbe.OriginalCrlfNamespace)
			.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		second.Success.ShouldBeTrue(
			$"A line-ending translation is not a schema change and must not be reported as drift. Reported: {second.ErrorMessage}");
		second.AppliedMigrations.ShouldBeEmpty("The migration was already applied; it must not run a second time.");
	}

	[Fact]
	public async Task RecordAChecksumThatSurvivesTheHistoryColumn()
	{
		_requiredContainer.Require();

		var connectionString = await CreateDatabaseAsync().ConfigureAwait(false);
		var migrator = MigratorFor(connectionString, MigrationDriftProbe.OriginalNamespace);

		_ = await migrator.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		var applied = await migrator.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		var recorded = applied.ShouldHaveSingleItem();

		// A hash the column silently truncated would still compare equal to itself in memory, so the
		// length is asserted where it has actually been through the database.
		recorded.Checksum.ShouldNotBeNullOrWhiteSpace();
		recorded.Checksum!.Length.ShouldBe(64, "A SHA-256 hex digest is 64 characters and the history column must hold all of them.");
	}

	private static SqlServerMigrator MigratorFor(string connectionString, string migrationNamespace) =>
		new(
			connectionString,
			Assembly.GetExecutingAssembly(),
			migrationNamespace,
			NullLoggerFactory.Instance.CreateLogger<SqlServerMigrator>());

	private async Task<string> CreateDatabaseAsync()
	{
		// One database per test: the migration history table is per-database, and a test that inherited
		// another's history would be asserting against a state it did not establish.
		var databaseName = "drift_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
		var adminConnectionString = _container!.GetConnectionString();

		await using (var admin = new SqlConnection(adminConnectionString))
		{
			await admin.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
			await admin.ExecuteAsync(new CommandDefinition(
				$"CREATE DATABASE [{databaseName}]",
				cancellationToken: TestContext.Current.CancellationToken)).ConfigureAwait(false);
		}

		return new SqlConnectionStringBuilder(adminConnectionString) { InitialCatalog = databaseName }.ConnectionString;
	}

	private static async Task<int> ProbeColumnCountAsync(string connectionString)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @TableName",
			new { TableName = MigrationDriftProbe.ProbeTableName },
			cancellationToken: TestContext.Current.CancellationToken)).ConfigureAwait(false);
	}
}
