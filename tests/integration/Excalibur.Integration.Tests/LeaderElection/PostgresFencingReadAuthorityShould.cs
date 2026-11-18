// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
[Trait("Database", "Postgres")]
public sealed class PostgresFencingReadAuthorityShould(PostgresContainerFixture fixture)
    : IntegrationTestBase, IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task RefuseAReadOnlySessionRatherThanReportAnAbsentMark()
    {
        fixture.DockerAvailable.ShouldBeTrue("real PostgreSQL authority controls must not skip infrastructure");
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Options = "-c default_transaction_read_only=on",
        }.ConnectionString;
        using var services = BuildServices(connection);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IFencingTokenProvider>().GetTokenAsync("absent", TestCancellationToken).AsTask());
        error.Message.ShouldContain("writable");
    }

    [Fact]
    public async Task RefuseAnUnreadableMarkThenAllowSelectWithoutMintPrivileges()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedSchema.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        var name = SequenceName("resource");
        await scope.PlantAsync(name, advance: true);
        using var services = BuildServices(scope.ReaderConnection);
        var store = services.GetRequiredService<IFencingTokenIssuer>();

        var error = await Should.ThrowAsync<InvalidOperationException>(() => store.GetTokenAsync("resource", TestCancellationToken).AsTask());
        error.Message.ShouldContain("unreadable");
        (await scope.ReadRawMarkAsync(name)).ShouldBe(9L);
        await scope.GrantSelectAsync();
        (await store.GetTokenAsync("resource", TestCancellationToken)).ShouldBe(9L);
        (await store.ValidateTokenAsync("resource", 9, TestCancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<PostgresException>(() => store.IssueTokenAsync("resource", TestCancellationToken).AsTask());
        (await scope.ReadRawMarkAsync(name)).ShouldBe(9L);
    }

    [Fact]
    public async Task PreserveAuthorizedAbsentAndUnadvancedStates()
    {
        fixture.DockerAvailable.ShouldBeTrue();
        await using var scope = await OwnedSchema.CreateAsync(fixture.ConnectionString, TestCancellationToken);
        await scope.PlantAsync(SequenceName("unadvanced"), advance: false);
        await scope.GrantSelectAsync();
        using var services = BuildServices(scope.ReaderConnection);
        var store = services.GetRequiredService<IFencingTokenProvider>();

        (await store.GetTokenAsync("absent", TestCancellationToken)).ShouldBeNull();
        (await store.GetTokenAsync("unadvanced", TestCancellationToken)).ShouldBeNull();
        (await store.ValidateTokenAsync("unadvanced", long.MaxValue, TestCancellationToken)).ShouldBeFalse();
    }

    private static string SequenceName(string resource) => "fencing_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resource)))[..55];

    private static ServiceProvider BuildServices(string connection) => new ServiceCollection()
        .AddPostgresFencingTokenProvider(connection).BuildServiceProvider();

    private sealed class OwnedSchema(string administrator, string schema, string user, CancellationToken cancellationToken) : IAsyncDisposable
    {
        internal string ReaderConnection => new NpgsqlConnectionStringBuilder(administrator)
        {
            Username = user,
            Password = "Fencing-Test-Only-71!x",
            SearchPath = schema,
        }.ConnectionString;

        internal static async Task<OwnedSchema> CreateAsync(string administrator, CancellationToken token)
        {
            var scope = new OwnedSchema(administrator, "fence_" + Guid.NewGuid().ToString("N"), "fencereader_" + Guid.NewGuid().ToString("N"), token);
            await scope.ConfigureAsync(0);
            return scope;
        }

        internal Task GrantSelectAsync() => ConfigureAsync(1);
        internal Task PlantAsync(string sequence, bool advance) => ConfigureAsync(2, sequence, advance);

        // All command text is constant; server format(%I) quotes only this case's owned identifiers.
        private async Task ConfigureAsync(int action, string? sequence = null, bool advance = false)
        {
            const string sql = @"
SELECT set_config('excalibur.fence_schema', @schema, false),
       set_config('excalibur.fence_user', @user, false),
       set_config('excalibur.fence_action', @action::text, false),
       set_config('excalibur.fence_sequence', @sequence, false),
       set_config('excalibur.fence_advance', @advance::text, false);
DO $setup$
BEGIN
 IF current_setting('excalibur.fence_action') = '0' THEN
  EXECUTE format('CREATE SCHEMA %I', current_setting('excalibur.fence_schema'));
  EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', current_setting('excalibur.fence_user'), 'Fencing-Test-Only-71!x');
  EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', current_setting('excalibur.fence_schema'), current_setting('excalibur.fence_user'));
 ELSIF current_setting('excalibur.fence_action') = '1' THEN
  EXECUTE format('GRANT SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', current_setting('excalibur.fence_schema'), current_setting('excalibur.fence_user'));
 ELSIF current_setting('excalibur.fence_action') = '2' THEN
  EXECUTE format('CREATE SEQUENCE %I.%I START WITH 9', current_setting('excalibur.fence_schema'), current_setting('excalibur.fence_sequence'));
  IF current_setting('excalibur.fence_advance') = 'true' THEN
   EXECUTE format('SELECT nextval(%L)', format('%I.%I', current_setting('excalibur.fence_schema'), current_setting('excalibur.fence_sequence')));
  END IF;
 ELSE
  EXECUTE format('DROP SCHEMA %I CASCADE', current_setting('excalibur.fence_schema'));
  EXECUTE format('DROP ROLE %I', current_setting('excalibur.fence_user'));
 END IF;
END $setup$;";
            await using var connection = new NpgsqlConnection(administrator);
            await connection.OpenAsync(action == 3 ? CancellationToken.None : cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("user", user);
            command.Parameters.AddWithValue("action", action);
            command.Parameters.AddWithValue("sequence", sequence ?? string.Empty);
            command.Parameters.AddWithValue("advance", advance);
            await command.ExecuteNonQueryAsync(action == 3 ? CancellationToken.None : cancellationToken);
        }

        internal async Task<long?> ReadRawMarkAsync(string name)
        {
            await using var connection = new NpgsqlConnection(administrator);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT last_value FROM pg_sequences WHERE schemaname = @schema AND sequencename = @name", connection);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("name", name);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? null : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        public async ValueTask DisposeAsync() => await ConfigureAsync(3);
    }
}
