// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
public sealed class SqlFencingReadAuthorityShould(SqlServerContainerFixture fixture)
    : IntegrationTestBase, IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task PreserveOneDboCounterAcrossDifferentLoginDefaultSchemas()
    {
        fixture.DockerAvailable.ShouldBeTrue("real SQL authority controls must not skip infrastructure");
        await using var scope = await OwnedDatabase.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        var name = SequenceName("resource");
        await scope.ArrangeAsync(Arrangement.Sequence, schema: "dbo", sequence: name, start: 7, advance: true);
        await scope.ArrangeAsync(Arrangement.Sequence, schema: "app", sequence: name, start: 41, advance: true);
        var before = await scope.ReadDboObjectIdAsync(name);
        var firstConnection = await scope.PrincipalAsync("app", canMint: true);
        var secondConnection = await scope.PrincipalAsync("other", canMint: true);
        using var first = BuildServices(firstConnection);
        using var second = BuildServices(secondConnection);

        (await first.GetRequiredService<IFencingTokenIssuer>().IssueTokenAsync("resource", TestCancellationToken)).ShouldBe(8L);
        (await first.GetRequiredService<IFencingTokenProvider>().GetTokenAsync("resource", TestCancellationToken)).ShouldBe(8L);
        (await second.GetRequiredService<IFencingTokenIssuer>().IssueTokenAsync("resource", TestCancellationToken)).ShouldBe(9L);
        (await scope.ReadDboMarkAsync(name)).ShouldBe(9L);
        (await scope.ReadDboObjectIdAsync(name)).ShouldBe(before);
    }

    [Fact]
    public async Task RefuseAReadOnlyAuthorityInsteadOfReportingAnAbsentMark()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedDatabase.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        await scope.ArrangeAsync(Arrangement.ReadOnly);
        using var services = BuildServices(scope.ConnectionString);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IFencingTokenProvider>().GetTokenAsync("absent", TestCancellationToken).AsTask());
        error.Message.ShouldContain("writable");
    }

    [Fact]
    public async Task RefuseHiddenMarksThenAllowAnObjectMetadataOnlyObserver()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedDatabase.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        var name = SequenceName("resource");
        await scope.ArrangeAsync(Arrangement.Sequence, schema: "dbo", sequence: name, start: 7, advance: true);
        var connection = await scope.PrincipalAsync("dbo", canMint: false);
        using var services = BuildServices(connection);
        var store = services.GetRequiredService<IFencingTokenIssuer>();

        var error = await Should.ThrowAsync<InvalidOperationException>(() => store.GetTokenAsync("resource", TestCancellationToken).AsTask());
        error.Message.ShouldContain("visibility");
        (await scope.ReadDboMarkAsync(name)).ShouldBe(7L);
        await scope.ArrangeAsync(Arrangement.ViewObject, sequence: name);
        (await store.GetTokenAsync("resource", TestCancellationToken)).ShouldBe(7L);
        (await store.ValidateTokenAsync("resource", 7, TestCancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<SqlException>(() => store.IssueTokenAsync("resource", TestCancellationToken).AsTask());
        (await scope.ReadDboMarkAsync(name)).ShouldBe(7L);
    }

    [Fact]
    public async Task RefuseEffectiveObjectMetadataDenialDespiteSchemaVisibility()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedDatabase.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        var name = SequenceName("resource");
        await scope.ArrangeAsync(Arrangement.Sequence, schema: "dbo", sequence: name, start: 7, advance: true);
        var connection = await scope.PrincipalAsync("dbo", canMint: false);
        await scope.ArrangeAsync(Arrangement.ViewSchema);
        await scope.ArrangeAsync(Arrangement.DenyObject, sequence: name);
        using var services = BuildServices(connection);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IFencingTokenProvider>().GetTokenAsync("resource", TestCancellationToken).AsTask());
        error.Message.ShouldContain("visibility");
        (await scope.ReadDboMarkAsync(name)).ShouldBe(7L);
    }

    [Fact]
    public async Task DistinguishAuthorizedAbsenceFromAnAuthorizedUnadvancedSequence()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedDatabase.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        var connection = await scope.PrincipalAsync("dbo", canMint: false);
        await scope.ArrangeAsync(Arrangement.ViewSchema);
        using var services = BuildServices(connection);
        var store = services.GetRequiredService<IFencingTokenProvider>();
        (await store.GetTokenAsync("absent", TestCancellationToken)).ShouldBeNull();
        await scope.ArrangeAsync(Arrangement.Sequence, schema: "dbo", sequence: SequenceName("unadvanced"), start: 7);
        (await store.GetTokenAsync("unadvanced", TestCancellationToken)).ShouldBeNull();
        (await store.ValidateTokenAsync("unadvanced", long.MaxValue, TestCancellationToken)).ShouldBeFalse();
    }

    private static string SequenceName(string resource) => "fencing_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));

    private static ServiceProvider BuildServices(string connection) => new ServiceCollection()
        .AddSqlServerFencingTokenProvider(connection).BuildServiceProvider();

    private enum Arrangement { Create, Principal, Sequence, ViewSchema, ViewObject, DenyObject, ReadOnly, Drop, DropLogin }

    // Owns only this case's database/logins; all SQL command text is constant and identifiers are server-quoted parameters.
    private sealed class OwnedDatabase(string master, string database, CancellationToken cancellationToken) : IAsyncDisposable
    {
        private const string Password = "Fencing-Test-Only-71!x";
        private readonly List<string> _logins = [];
        private readonly List<string> _connections = [];
        private string? _lastPrincipal;
        internal string ConnectionString => new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;

        internal static async Task<OwnedDatabase> CreateAsync(string connection, CancellationToken token)
        {
            var scope = new OwnedDatabase(new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString,
                "Fence_" + Guid.NewGuid().ToString("N"), token);
            await scope.ArrangeAsync(Arrangement.Create);
            return scope;
        }

        internal async Task<string> PrincipalAsync(string schema, bool canMint)
        {
            _lastPrincipal = "FenceReader_" + Guid.NewGuid().ToString("N");
            _logins.Add(_lastPrincipal);
            await ArrangeAsync(Arrangement.Principal, schema: schema, canMint: canMint);
            var result = new SqlConnectionStringBuilder(ConnectionString) { UserID = _lastPrincipal, Password = Password, IntegratedSecurity = false }.ConnectionString;
            _connections.Add(result);
            return result;
        }

        internal async Task ArrangeAsync(Arrangement action, string? schema = null, string? sequence = null, long start = 1, bool advance = false, bool canMint = false)
        {
            const string sql = @"
DECLARE @statement nvarchar(max);
IF @action = 0 SET @statement = N'CREATE DATABASE ' + QUOTENAME(@database);
IF @action = 1
BEGIN
 IF SCHEMA_ID(@schema) IS NULL BEGIN SET @statement = N'CREATE SCHEMA ' + QUOTENAME(@schema); EXEC sp_executesql @statement; END;
 SET @statement = N'CREATE LOGIN ' + QUOTENAME(@principal) + N' WITH PASSWORD=N''Fencing-Test-Only-71!x'', CHECK_POLICY=OFF; CREATE USER '
   + QUOTENAME(@principal) + N' FOR LOGIN ' + QUOTENAME(@principal) + N' WITH DEFAULT_SCHEMA=' + QUOTENAME(@schema) + N';';
 IF @canMint = 1 SET @statement += N' GRANT ALTER ON SCHEMA::dbo TO '
   + QUOTENAME(@principal) + N'; GRANT ALTER ON SCHEMA::' + QUOTENAME(@schema) + N' TO ' + QUOTENAME(@principal)
   + N'; GRANT UPDATE ON SCHEMA::dbo TO ' + QUOTENAME(@principal) + N'; GRANT UPDATE ON SCHEMA::' + QUOTENAME(@schema) + N' TO ' + QUOTENAME(@principal) + N';';
END;
IF @action = 2
BEGIN
 IF SCHEMA_ID(@schema) IS NULL BEGIN SET @statement = N'CREATE SCHEMA ' + QUOTENAME(@schema); EXEC sp_executesql @statement; END;
 SET @statement = N'CREATE SEQUENCE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@sequence) + N' AS bigint START WITH ' + CONVERT(nvarchar(30), @start);
 IF @advance = 1 SET @statement += N'; SELECT NEXT VALUE FOR ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@sequence);
END;
IF @action = 3 SET @statement = N'GRANT VIEW DEFINITION ON SCHEMA::dbo TO ' + QUOTENAME(@principal);
IF @action = 4 SET @statement = N'GRANT VIEW DEFINITION ON OBJECT::dbo.' + QUOTENAME(@sequence) + N' TO ' + QUOTENAME(@principal);
IF @action = 5 SET @statement = N'DENY VIEW DEFINITION ON OBJECT::dbo.' + QUOTENAME(@sequence) + N' TO ' + QUOTENAME(@principal);
IF @action = 6 SET @statement = N'ALTER DATABASE ' + QUOTENAME(@database) + N' SET READ_ONLY WITH ROLLBACK IMMEDIATE';
IF @action = 7 SET @statement = N'ALTER DATABASE ' + QUOTENAME(@database) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@database);
IF @action = 8
BEGIN
 IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @principal) RETURN;
 SET @statement = N'DROP LOGIN ' + QUOTENAME(@principal);
END;
EXEC sp_executesql @statement;";
            await using var connection = new SqlConnection(action is Arrangement.Create or Arrangement.ReadOnly or Arrangement.Drop or Arrangement.DropLogin ? master : ConnectionString);
            await connection.OpenAsync(action is Arrangement.Drop or Arrangement.DropLogin ? CancellationToken.None : cancellationToken);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@action", (int)action);
            command.Parameters.AddWithValue("@database", database);
            command.Parameters.AddWithValue("@schema", (object?)schema ?? DBNull.Value);
            command.Parameters.AddWithValue("@principal", (object?)_lastPrincipal ?? DBNull.Value);
            command.Parameters.AddWithValue("@sequence", (object?)sequence ?? DBNull.Value);
            command.Parameters.AddWithValue("@start", start);
            command.Parameters.AddWithValue("@advance", advance);
            command.Parameters.AddWithValue("@canMint", canMint);
            await command.ExecuteNonQueryAsync(action is Arrangement.Drop or Arrangement.DropLogin ? CancellationToken.None : cancellationToken);
        }

        internal async Task<long?> ReadDboMarkAsync(string name)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT CAST(last_used_value AS bigint) FROM sys.sequences WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name", connection);
            command.Parameters.AddWithValue("@name", name);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? null : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        internal async Task<int?> ReadDboObjectIdAsync(string name)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT object_id FROM sys.sequences WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name", connection);
            command.Parameters.AddWithValue("@name", name);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public async ValueTask DisposeAsync()
        {
            using var ownPool = new SqlConnection(ConnectionString);
            SqlConnection.ClearPool(ownPool);
            foreach (var value in _connections)
            {
                using var pool = new SqlConnection(value);
                SqlConnection.ClearPool(pool);
            }
            await ArrangeAsync(Arrangement.Drop);
            foreach (var login in _logins) { _lastPrincipal = login; await ArrangeAsync(Arrangement.DropLogin); }
        }
    }
}
