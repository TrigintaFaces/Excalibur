// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds <see cref="SqlServerSagaTimeoutStore"/> to the shared <see cref="ISagaTimeoutStore"/> conformance
/// kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// The claim path is a single <c>UPDATE … OUTPUT INSERTED.*</c> over an ordered CTE under
/// <c>READPAST, UPDLOCK, ROWLOCK</c>. Its batch bound and its skip-locked lease are enforced by the
/// database, not by the driver, so only a real server can decide whether the contract holds. Never skipped:
/// when Docker is unavailable the fixture fails rather than passing silently.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
public sealed class SqlServerSagaTimeoutStoreConformanceShould
	: SagaTimeoutStoreConformanceTestBase, IClassFixture<SqlServerSagaTimeoutStoreContainerFixture>
{
	private readonly SqlServerSagaTimeoutStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSagaTimeoutStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server container fixture.</param>
	public SqlServerSagaTimeoutStoreConformanceShould(SqlServerSagaTimeoutStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaTimeoutStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// The single-tenant host shape. This suite used to pass no context at all, so the store resolved its
		// partition by folding an unset ambient value through the storage fold and stamped the reserved
		// untenanted sentinel -- a partition no other component in the framework addresses. A single-tenant
		// deployment operates as the one canonical tenant identity, and that is what these arms round-trip.
		return new SqlServerSagaTimeoutStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaTimeoutStore>.Instance,
			SingleTenantTestContext.Instance);
	}

	[Theory]
	[InlineData(32)]
	[InlineData(450)]
	public async Task PreserveLegalStringIdentifiersAndPayloadThroughClaimAndRetirement(int identifierLength)
	{
		var sagaId = new string('s', identifierLength);
		var timeoutId = new string('t', identifierLength);
		var asOf = DateTimeOffset.UtcNow;
		byte[] payload = [1, 2, 3];
		var timeout = new SagaTimeout(timeoutId, sagaId, "StringIdentitySaga", "StringIdentityTimeout",
			payload, asOf.AddMinutes(-1), asOf.AddMinutes(-2));

		await Store.ScheduleTimeoutAsync(timeout, CancellationToken.None).ConfigureAwait(false);

		var stored = (await Store.GetDueTimeoutsAsync(asOf, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
		stored.TimeoutId.ShouldBe(timeoutId);
		stored.SagaId.ShouldBe(sagaId);
		stored.TimeoutData.ShouldBe(payload);
		var claim = (await Store.ClaimDueTimeoutsAsync(asOf, 1, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
		claim.Timeout.TimeoutId.ShouldBe(timeoutId);
		claim.Timeout.SagaId.ShouldBe(sagaId);
		claim.Timeout.TimeoutData.ShouldBe(payload);
		(await Store.MarkDeliveredAsync(claim, CancellationToken.None).ConfigureAwait(false)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		(await Store.GetDueTimeoutsAsync(asOf, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("dbo")]
	[InlineData("other")]
	public async Task ProvisionTheDefaultTimeoutTableUnderTheDeclaredOwnerRegardlessOfLoginDefault(string defaultSchema)
	{
		_fixture.DockerAvailable.ShouldBeTrue("Real SQL Server provisioning is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var database = "TimeoutOwner_" + Guid.NewGuid().ToString("N");
		var databaseConnection = new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;
		var created = false;
		Exception? primaryFailure = null;
		try
		{
			await ChangeOwnedDatabaseAsync(_fixture.ConnectionString, database, create: true).ConfigureAwait(false);
			created = true;
			await using (var administrator = new SqlConnection(databaseConnection))
			{
				await administrator.OpenAsync().ConfigureAwait(false);
				const string setup = """
					EXEC(N'CREATE SCHEMA other AUTHORIZATION dbo');
					IF @DefaultSchema=N'dbo'
					 EXEC(N'CREATE USER TimeoutProvisioner WITHOUT LOGIN WITH DEFAULT_SCHEMA=dbo');
					ELSE IF @DefaultSchema=N'other'
					 EXEC(N'CREATE USER TimeoutProvisioner WITHOUT LOGIN WITH DEFAULT_SCHEMA=other');
					ELSE THROW 51001,'Unexpected owned fixture schema.',1;
					GRANT CREATE TABLE TO TimeoutProvisioner;
					GRANT ALTER ON SCHEMA::dbo TO TimeoutProvisioner;
					GRANT ALTER ON SCHEMA::other TO TimeoutProvisioner;
					""";
				await using var setupCommand = new SqlCommand(setup, administrator);
				setupCommand.Parameters.AddWithValue("@DefaultSchema", defaultSchema);
				await setupCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
			}

			SqlException? provisioningFailure = null;
			await using (var subject = new SqlConnection(databaseConnection))
			{
				await subject.OpenAsync().ConfigureAwait(false);
				const string impersonate = """
					EXECUTE AS USER=N'TimeoutProvisioner';
					SELECT USER_NAME(), SCHEMA_NAME(),
					 (SELECT default_schema_name FROM sys.database_principals WHERE name=USER_NAME()),
					 HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE TABLE'),
					 HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER'),
					 HAS_PERMS_BY_NAME(N'other',N'SCHEMA',N'ALTER'), IS_MEMBER(N'db_owner');
					""";
				await using (var preconditions = new SqlCommand(impersonate, subject))
				await using (var reader = await preconditions.ExecuteReaderAsync().ConfigureAwait(false))
				{
					(await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
					reader.GetString(0).ShouldBe("TimeoutProvisioner");
					reader.GetString(1).ShouldBe(defaultSchema);
					reader.GetString(2).ShouldBe(defaultSchema);
					reader.GetInt32(3).ShouldBe(1);
					reader.GetInt32(4).ShouldBe(1);
					reader.GetInt32(5).ShouldBe(1);
					reader.GetInt32(6).ShouldBe(0);
				}

				try
				{
					await SqlServerSagaTimeoutStoreContainerFixture.ExecuteShippedSchemaAsync(subject, CancellationToken.None).ConfigureAwait(false);
				}
				catch (SqlException exception)
				{
					provisioningFailure = exception;
				}
				finally
				{
					await using var revert = new SqlCommand("REVERT;", subject);
					await revert.ExecuteNonQueryAsync().ConfigureAwait(false);
				}
			}

			await using var observer = new SqlConnection(databaseConnection);
			await observer.OpenAsync().ConfigureAwait(false);
			const string catalog = """
				SELECT
				 (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name=N'SagaTimeouts'),
				 (SELECT COUNT(*) FROM sys.tables WHERE schema_id<>SCHEMA_ID(N'dbo') AND name=N'SagaTimeouts'),
				 (SELECT COUNT(*) FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'dbo.SagaTimeouts')
				   AND minor_id=0 AND name=N'Excalibur.TimeoutFailureTarget'
				   AND TRY_CONVERT(uniqueidentifier,value)<>CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000')),
				 COALESCE((SELECT STRING_AGG(name,N',') WITHIN GROUP(ORDER BY name COLLATE Latin1_General_BIN2) FROM sys.indexes
				   WHERE object_id=OBJECT_ID(N'dbo.SagaTimeouts') AND is_primary_key=0 AND is_disabled=0),N'');
				""";
			await using var catalogCommand = new SqlCommand(catalog, observer);
			await using var actual = await catalogCommand.ExecuteReaderAsync().ConfigureAwait(false);
			(await actual.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
			var dboTables = actual.GetInt32(0);
			var otherTables = actual.GetInt32(1);
			var stamps = actual.GetInt32(2);
			var indexes = actual.GetString(3);
			provisioningFailure.ShouldBeNull($"Preconditions passed; independent catalog dbo={dboTables}, other={otherTables}, stamps={stamps}, indexes={indexes}.");
			dboTables.ShouldBe(1);
			otherTables.ShouldBe(0);
			stamps.ShouldBe(1);
			indexes.ShouldBe("IX_SagaTimeouts_DueAt_ClaimedAt,IX_SagaTimeouts_TenantId_SagaId,IX_SagaTimeouts_TenantId_SagaId_TimeoutId,IX_TimeoutFailureHeld");
		}
		catch (Exception exception)
		{
			primaryFailure = exception;
			throw;
		}
		finally
		{
			try
			{
				using var ownPool = new SqlConnection(databaseConnection);
				SqlConnection.ClearPool(ownPool);
				if (created)
				{
					await ChangeOwnedDatabaseAsync(_fixture.ConnectionString, database, create: false).ConfigureAwait(false);
				}
			}
			catch (Exception cleanupFailure) when (primaryFailure is not null)
			{
				primaryFailure.Data["TimeoutOwnerCleanupFailure"] = cleanupFailure;
			}
		}
	}

	private static async Task ChangeOwnedDatabaseAsync(string connectionString, string database, bool create)
	{
		database.StartsWith("TimeoutOwner_", StringComparison.Ordinal).ShouldBeTrue();
		Guid.TryParseExact(database["TimeoutOwner_".Length..], "N", out _).ShouldBeTrue();
		await using var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		const string sql = """
			DECLARE @Sql nvarchar(max);
			IF @Create=1 SET @Sql=N'CREATE DATABASE '+QUOTENAME(@Database);
			ELSE IF DB_ID(@Database) IS NOT NULL SET @Sql=N'ALTER DATABASE '+QUOTENAME(@Database)+N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE '+QUOTENAME(@Database);
			IF @Sql IS NOT NULL EXEC sys.sp_executesql @Sql;
			""";
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@Database", database);
		command.Parameters.AddWithValue("@Create", create);
		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
