// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Globalization;
using System.Text;
using Dapper;
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServerCdc)]
[Trait("Category", "Integration")]
[Trait("Component", "CDC")]
[Trait("Pattern", "Recovery")]
public sealed class SqlServerCdcBackupRestoreIntegrationShould(SqlServerCdcContainerFixture fixture) : IntegrationTestBase
{
    [Fact]
    public async Task RecoverTheExistingProcessorWhenExternalStateIsAheadOfARestoredSource()
    {
        fixture.DockerAvailable.ShouldBeTrue("This regression requires SQL Server Agent and a real BACKUP/RESTORE.");
        var source = "CdcRestore" + Guid.NewGuid().ToString("N")[..12];
        var state = source + "State";
        var sourceConnection = await fixture.CreateCdcEnabledDatabaseAsync(source, TestCancellationToken);
        var stateConnection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = state }.ConnectionString;
        await ExecuteAsync(fixture.ConnectionString, $"CREATE DATABASE [{state}];");
        var batch = new StringBuilder();
        foreach (var line in await File.ReadAllLinesAsync(LocateSchema(), TestCancellationToken))
        {
            if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.Length > 0)
                {
                    await ExecuteAsync(stateConnection, batch.ToString());
                }
                batch.Clear();
            }
            else
            {
                batch.AppendLine(line);
            }
        }
        if (batch.Length > 0)
        {
            await ExecuteAsync(stateConnection, batch.ToString());
        }
        await ExecuteAsync(sourceConnection, "CREATE TABLE dbo.Items(Id int PRIMARY KEY, Value nvarchar(40));");
        await SqlServerCdcContainerFixture.EnableTableCaptureAsync(sourceConnection, "dbo", "Items", TestCancellationToken);
        await InsertAndWaitAsync(sourceConnection, 1);
        var backup = "/var/opt/mssql/" + source + ".bak";
        await ExecuteAsync(fixture.ConnectionString, $"BACKUP DATABASE [{source}] TO DISK=N'{backup}' WITH INIT;");
        var resets = new List<CdcPositionResetEventArgs>();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddCdcProcessor(cdc => cdc
            .UseSqlServer(sql => sql.ConnectionString(sourceConnection).DatabaseName(source)
                .DatabaseConnectionIdentifier(source).CaptureInstances("dbo_Items")
                .WithStateStore(store => store.ConnectionString(stateConnection)))
            .WithRecovery(recovery => recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
                .OnPositionReset((args, _) => { resets.Add(args); return Task.CompletedTask; })));
        using var host = builder.Build();
        var processor = host.Services.GetRequiredService<ISqlServerCdcProcessor>();
        var delivered = new List<int>();
        Task Handle(DataChangeEvent change, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            delivered.Add(Convert.ToInt32(change.Changes.Single(x => x.ColumnName == "Id").NewValue, CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        }
        await processor.ProcessBatchAsync(Handle, TestCancellationToken);
        await InsertAndWaitAsync(sourceConnection, 2);
        await processor.ProcessBatchAsync(Handle, TestCancellationToken);
        delivered.ShouldBe([1, 2]);
        // Only the generated database in the fixture-owned container is restored. The state database survives.
        await ExecuteAsync(fixture.ConnectionString,
            $"ALTER DATABASE [{source}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{source}] FROM DISK=N'{backup}' WITH REPLACE, KEEP_CDC; ALTER DATABASE [{source}] SET MULTI_USER;");
        delivered.Clear();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await processor.ProcessBatchAsync(Handle, TestCancellationToken);
                break;
            }
            catch (SqlException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), TestCancellationToken);
            }
        }
        resets.Count.ShouldBe(1);
        resets[0].ReasonCode.ShouldBe(StalePositionReasonCodes.BackupRestore);
        delivered.ShouldBe([1]);
    }

    private async Task InsertAndWaitAsync(string connection, int id)
    {
        await using var sql = new SqlConnection(connection);
        await sql.ExecuteAsync(new CommandDefinition("INSERT dbo.Items VALUES(@id,N'captured');", new { id }, cancellationToken: TestCancellationToken));
        (await SqlServerCdcContainerFixture.WaitForCapturedRowsAsync(connection, "dbo_Items", id, TimeSpan.FromSeconds(60), TestCancellationToken)).ShouldBeTrue();
    }
    private async Task ExecuteAsync(string connection, string statement)
    {
        await using var sql = new SqlConnection(connection);
        await sql.ExecuteAsync(new CommandDefinition(statement, commandTimeout: 90, cancellationToken: TestCancellationToken));
    }
    private static string LocateSchema()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src/Excalibur/Excalibur.Cdc.SqlServer/Scripts/001_CreateCdcStateSchema.sql");
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException("The shipped SQL Server CDC state schema must be available to this test.");
    }
}
