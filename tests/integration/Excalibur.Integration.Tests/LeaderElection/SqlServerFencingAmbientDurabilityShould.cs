// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Transactions;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.Integration.Tests.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerFencingAmbientDurabilityShould(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task PreserveAFirstMintAcrossBusinessRollbackAndAdvanceRatherThanReusingItsToken()
    {
        var resource = "sql-fence-ambient-" + Guid.NewGuid().ToString("N");
        var sequence = "fencing_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resource)));
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = true };
        var services = new ServiceCollection();
        _ = services.AddSqlServerFencingTokenProvider(connection.ConnectionString);
        await using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IFencingTokenIssuer>();
        var failures = new List<Exception>();
        var marker = "business_marker_" + Guid.NewGuid().ToString("N");
        try
        {
            // Create the business marker table autonomously; its inserted row must roll back.
            await ExecuteMarkerAsync($"CREATE TABLE dbo.[{marker}] (Id int NOT NULL);", enlist: false);
            (await provider.GetTokenAsync(resource, TestContext.Current.CancellationToken)).ShouldBeNull();
            long first;
            using (var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                var callerTransaction = Transaction.Current.ShouldNotBeNull();
                // Close the enlisted connection before minting to avoid accidental promotion.
                await ExecuteMarkerAsync($"INSERT INTO dbo.[{marker}] (Id) VALUES (1);", enlist: true);
                first = await provider.IssueTokenAsync(resource, TestContext.Current.CancellationToken);
                first.ShouldBeGreaterThan(0);
                Transaction.Current.ShouldBeSameAs(callerTransaction);
                // No Complete: a first-use sequence created in this transaction would disappear.
            }
            (await ExecuteMarkerAsync($"SELECT COUNT(*) FROM dbo.[{marker}];", enlist: false, scalar: true)).ShouldBe(0);
            var freshServices = new ServiceCollection();
            _ = freshServices.AddSqlServerFencingTokenProvider(connection.ConnectionString);
            await using var freshServiceProvider = freshServices.BuildServiceProvider();
            var freshProvider = freshServiceProvider.GetRequiredService<IFencingTokenIssuer>();
            var persisted = await freshProvider.GetTokenAsync(resource, TestContext.Current.CancellationToken);
            var next = await freshProvider.IssueTokenAsync(resource, TestContext.Current.CancellationToken);
            persisted.ShouldBe(first);
            next.ShouldBeGreaterThan(first);
            (await freshProvider.GetTokenAsync(resource, TestContext.Current.CancellationToken)).ShouldBe(next);
            (await freshProvider.ValidateTokenAsync(resource, next, TestContext.Current.CancellationToken)).ShouldBeTrue();
            (await provider.ValidateTokenAsync(resource, first, TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var client = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = false }.ConnectionString);
                await client.OpenAsync(cleanup.Token);
                await using var command = client.CreateCommand();
                command.CommandText = """
                    DECLARE @sql nvarchar(max) = N'DROP SEQUENCE IF EXISTS ' + QUOTENAME(N'dbo') + N'.' + QUOTENAME(@Name);
                    EXEC sys.sp_executesql @sql;
                    SET @sql = N'DROP TABLE IF EXISTS ' + QUOTENAME(N'dbo') + N'.' + QUOTENAME(@Marker);
                    EXEC sys.sp_executesql @sql;
                    """;
                command.CommandTimeout = 10;
                _ = command.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = sequence;
                _ = command.Parameters.Add("@Marker", SqlDbType.NVarChar, 128).Value = marker;
                _ = await command.ExecuteNonQueryAsync(cleanup.Token);
            }
            catch (Exception error) { failures.Add(error); }
        }
        async Task<int> ExecuteMarkerAsync(string sql, bool enlist, bool scalar = false)
        {
            await using var client = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString) { Enlist = enlist }.ConnectionString);
            await client.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = client.CreateCommand();
#pragma warning disable CA2100 // Only fixed SQL templates with this test's locally generated N-format GUID identifier reach this local helper.
            command.CommandText = sql;
#pragma warning restore CA2100
            command.CommandTimeout = 10;
            return scalar
                ? Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture)
                : await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException(failures); }
    }
}
