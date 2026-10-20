// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Excalibur.Data.SqlServer.Scheduling;
using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Scheduling;

[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "STORE")]
public sealed class SqlServerScheduleProcessRecoveryShould(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly string _database = "schedule_process_" + Guid.NewGuid().ToString("N");
    private string _connectionString = null!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Identifier is a generated GUID; schema is the embedded shipped script.")]
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        await using (var master = new SqlConnection(fixture.ConnectionString))
        {
            await master.OpenAsync(deadline.Token);
            await using var create = new SqlCommand($"CREATE DATABASE [{_database}]", master) { CommandTimeout = 20 };
            await create.ExecuteNonQueryAsync(deadline.Token);
        }
        _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = _database }.ConnectionString;
        var assembly = GetType().Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("SqlServer.004_CreateScheduleSchema.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Missing shipped schema.");
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(deadline.Token);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(deadline.Token);
        await using var command = new SqlCommand(script, connection) { CommandTimeout = 20 };
        await command.ExecuteNonQueryAsync(deadline.Token);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Identifier is a generated GUID, never external input.")]
    public async ValueTask DisposeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(deadline.Token);
        await using var command = new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", connection) { CommandTimeout = 20 };
        await command.ExecuteNonQueryAsync(deadline.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreserveOccurrenceAndFenceDeadWorkerAcrossIndependentProcesses(bool crash)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        var store = NewStore();
        var message = new ScheduledMessage
        {
            Id = Guid.NewGuid(), MessageName = "RecoveryDocument", MessageBody = "opaque\nexact payload 用户",
            CronExpression = null!, TimeZoneId = "UTC", Interval = TimeSpan.FromMinutes(2),
            NextExecutionUtc = DateTimeOffset.UtcNow.AddMinutes(-1), LastExecutionUtc = null, Enabled = true,
            MissedExecutionBehavior = MissedExecutionBehavior.SkipMissed,
            CorrelationId = "correlation-process", TraceParent = "trace-process", TenantId = "Tenant-A", UserId = "user-process",
        };
        await store.StoreAsync(message, token);
        var snapshot = (await store.GetExecutionSnapshotsAsync(token)).Single();
        await using var first = StartProbe(message.Id, crash ? 5 : 20);
        using var acquiredA = await first.ReadAsync("acquired", token);
        var oldLease = ReadLease(acquiredA.RootElement);
        acquiredA.RootElement.GetProperty("ProcessId").GetInt32().ShouldBe(first.Id);
        first.Id.ShouldNotBe(Environment.ProcessId);
        AssertMessage(acquiredA.RootElement.GetProperty("Message"), message);
        (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc.Value, token)).ShouldBeNull();
        var originalExpiry = await ReadLiveExpiryAsync(message.Id, oldLease.OwnerToken, token);

        if (crash)
        {
            // This oracle samples the database clock, proving the child died while its grant was live.
            (await HasLiveGrantAsync(message.Id, oldLease.OwnerToken, token)).ShouldBeTrue();
            await first.KillAsync(token);
            (await HasLiveGrantAsync(message.Id, oldLease.OwnerToken, token)).ShouldBeTrue(
                "The database grant must still be live after the owned child has exited; otherwise this did not test crash recovery.");
            while (await HasLiveGrantAsync(message.Id, oldLease.OwnerToken, token))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            }
        }
        else
        {
            await first.SendAsync("release", token);
            using var releasedA = await first.ReadAsync("released", token);
            releasedA.RootElement.GetProperty("Success").GetBoolean().ShouldBeTrue();
            await first.WaitForSuccessAsync(token);
        }

        await using var second = StartProbe(message.Id, crash ? 5 : 20);
        using var acquiredB = await second.ReadAsync("acquired", token);
        var replacement = ReadLease(acquiredB.RootElement);
        if (!crash)
        {
            (await DatabaseClockPrecedesAsync(originalExpiry, token)).ShouldBeTrue(
                "The replacement must acquire before A's original persisted expiry, proving release rather than expiry enabled admission.");
        }
        second.Id.ShouldNotBe(first.Id);
        second.Id.ShouldNotBe(Environment.ProcessId);
        replacement.StoreId.ShouldBe(oldLease.StoreId);
        replacement.Generation.ShouldBe(oldLease.Generation);
        replacement.Generation.ShouldBe(snapshot.Generation);
        replacement.OccurrenceUtc.ShouldBe(oldLease.OccurrenceUtc);
        replacement.OwnerToken.ShouldNotBe(oldLease.OwnerToken);
        AssertMessage(acquiredB.RootElement.GetProperty("Message"), message);
        (await store.TryRenewExecutionAsync(oldLease, token)).ShouldBeFalse();
        (await store.TryUpdateProgressAsync(oldLease, new ScheduleExecutionResult(oldLease.OccurrenceUtc, null, true), token)).ShouldBeFalse();
        (await store.ReleaseExecutionAsync(oldLease, token)).ShouldBeFalse();
        await second.SendAsync("commit", token);
        using var committed = await second.ReadAsync("committed", token);
        committed.RootElement.GetProperty("Success").GetBoolean().ShouldBeTrue();
        await second.SendAsync("release", token);
        using var releasedB = await second.ReadAsync("released", token);
        releasedB.RootElement.GetProperty("Success").GetBoolean().ShouldBeTrue();
        await second.WaitForSuccessAsync(token);
        var retired = (await NewStore().GetAllAsync(token)).Single();
        retired.Enabled.ShouldBeFalse();
        retired.NextExecutionUtc.ShouldBeNull();
        retired.LastExecutionUtc.ShouldBe(oldLease.OccurrenceUtc);
        retired.MessageBody.ShouldBe(message.MessageBody);
        retired.TenantId.ShouldBe(message.TenantId);
    }

    private SqlServerScheduleStore NewStore() => new(Microsoft.Extensions.Options.Options.Create(new SqlServerScheduleStoreOptions
    {
        ConnectionString = _connectionString, LeaseDuration = TimeSpan.FromSeconds(5),
        CommandTimeoutSeconds = 10, LockTimeoutMilliseconds = 1000,
    }));

    private ProbeProcess StartProbe(Guid id, int leaseSeconds) => new(_connectionString, id, leaseSeconds);

    private async Task<DateTimeOffset> ReadLiveExpiryAsync(Guid id, Guid owner, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT LeaseExpiresUtc FROM dispatch.ScheduledMessage WHERE Id=@Id AND OwnerToken=@Owner AND LeaseExpiresUtc>SYSUTCDATETIME()", connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Owner", SqlDbType.UniqueIdentifier).Value = owner;
        var value = await command.ExecuteScalarAsync(token);
        value.ShouldBeOfType<DateTimeOffset>("A must still own a physically live database grant before release or crash.");
        return (DateTimeOffset)value!;
    }

    private async Task<bool> DatabaseClockPrecedesAsync(DateTimeOffset expiry, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT CASE WHEN SYSUTCDATETIME()<@Expiry THEN 1 ELSE 0 END", connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Expiry", SqlDbType.DateTimeOffset).Value = expiry;
        return (int)(await command.ExecuteScalarAsync(token))! == 1;
    }

    private async Task<bool> HasLiveGrantAsync(Guid id, Guid owner, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dispatch.ScheduledMessage WHERE Id=@Id AND OwnerToken=@Owner AND LeaseExpiresUtc>SYSUTCDATETIME()", connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Owner", SqlDbType.UniqueIdentifier).Value = owner;
        return (int)(await command.ExecuteScalarAsync(token))! == 1;
    }

    private static ScheduleExecutionLease ReadLease(JsonElement reply)
    {
        var lease = reply.GetProperty("Lease");
        return new(lease.GetProperty("StoreId").GetGuid(), lease.GetProperty("ScheduleId").GetGuid(),
            lease.GetProperty("Generation").GetGuid(), lease.GetProperty("OccurrenceUtc").GetDateTimeOffset(), lease.GetProperty("OwnerToken").GetGuid());
    }

    private static void AssertMessage(JsonElement actual, ScheduledMessage expected)
    {
        actual.GetProperty("Id").GetGuid().ShouldBe(expected.Id);
        actual.GetProperty("MessageName").GetString().ShouldBe(expected.MessageName);
        actual.GetProperty("MessageBody").GetString().ShouldBe(expected.MessageBody);
        actual.GetProperty("CronExpression").ValueKind.ShouldBe(JsonValueKind.Null);
        actual.GetProperty("TimeZoneId").GetString().ShouldBe(expected.TimeZoneId);
        actual.GetProperty("Interval").GetString().ShouldBe(expected.Interval!.Value.ToString());
        actual.GetProperty("NextExecutionUtc").GetDateTimeOffset().EqualsExact(expected.NextExecutionUtc!.Value).ShouldBeTrue();
        actual.GetProperty("LastExecutionUtc").ValueKind.ShouldBe(JsonValueKind.Null);
        actual.GetProperty("Enabled").GetBoolean().ShouldBe(expected.Enabled);
        actual.GetProperty("MissedExecutionBehavior").GetInt32().ShouldBe((int)expected.MissedExecutionBehavior!.Value);
        actual.GetProperty("CorrelationId").GetString().ShouldBe(expected.CorrelationId);
        actual.GetProperty("TraceParent").GetString().ShouldBe(expected.TraceParent);
        actual.GetProperty("TenantId").GetString().ShouldBe(expected.TenantId);
        actual.GetProperty("UserId").GetString().ShouldBe(expected.UserId);
    }

    private sealed class ProbeProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _error;
        public int Id => _process.Id;

        public ProbeProcess(string connection, Guid id, int leaseSeconds)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "schedule-process-probe", "SqlServerScheduleProcessProbe.dll");
            File.Exists(path).ShouldBeTrue("The build must copy the independent process probe.");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add(path);
            start.Environment["EXCALIBUR_SCHEDULE_TEST_CONNECTION"] = connection;
            start.Environment["EXCALIBUR_SCHEDULE_TEST_ID"] = id.ToString();
            start.Environment["EXCALIBUR_SCHEDULE_TEST_LEASE_SECONDS"] = leaseSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start owned probe.");
            _error = _process.StandardError.ReadToEndAsync();
        }

        public async Task<JsonDocument> ReadAsync(string stage, CancellationToken token)
        {
            var line = await _process.StandardOutput.ReadLineAsync(token)
                ?? throw new InvalidOperationException("Probe exited before replying: " + await _error.WaitAsync(token));
            var document = JsonDocument.Parse(line);
            try
            {
                document.RootElement.GetProperty("Stage").GetString().ShouldBe(stage);
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }

        public async Task SendAsync(string command, CancellationToken token)
        {
            await _process.StandardInput.WriteLineAsync(command.AsMemory(), token);
            await _process.StandardInput.FlushAsync(token);
        }

        public async Task WaitForSuccessAsync(CancellationToken token)
        {
            await _process.WaitForExitAsync(token);
            _process.ExitCode.ShouldBe(0, await _error.WaitAsync(token));
        }

        public async Task KillAsync(CancellationToken token)
        {
            if (!_process.HasExited) { _process.Kill(entireProcessTree: true); }
            await _process.WaitForExitAsync(token);
        }

        public async ValueTask DisposeAsync()
        {
            var pid = Id;
            var cleanup = FinishCleanupAsync();
            try { await cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception error)
            {
                // The task retains the process and its streams until exit. Observe a late fault,
                // but fail this arm instead of claiming the owned PID was cleaned up.
                _ = cleanup.ContinueWith(static completed => { _ = completed.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new InvalidOperationException($"Owned schedule probe PID {pid} cleanup failed or exceeded 10 seconds; process exit is unverified.", error);
            }
        }

        private async Task FinishCleanupAsync()
        {
            try
            {
                if (!_process.HasExited) { _process.Kill(entireProcessTree: true); }
                await _process.WaitForExitAsync();
                await _error;
            }
            finally
            {
                // Do not close streams beneath pending exit/stderr work after a timed-out wait.
                if (_process.HasExited && _error.IsCompleted) { _process.Dispose(); }
            }
        }
    }
}
