// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Excalibur.Data.SqlServer.Scheduling;
using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Scheduling;

[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "STORE")]
public sealed class SqlServerHostedScheduleProcessRecoveryShould(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly string _database = "schedule_hosted_" + Guid.NewGuid().ToString("N");
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
        var profileResource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("SqlServer.006_AddScheduleCronRecurrenceProfile.sql", StringComparison.Ordinal));
        await using var profileStream = assembly.GetManifestResourceStream(profileResource) ?? throw new InvalidOperationException("Missing shipped recurrence profile schema.");
        using var profileText = new StreamReader(profileStream);
        var failureResource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("SqlServer.005_CreateScheduleFailureSchema.sql", StringComparison.Ordinal));
        await using var failureStream = assembly.GetManifestResourceStream(failureResource) ?? throw new InvalidOperationException("Missing shipped failure schema.");
        using var failureText = new StreamReader(failureStream);
        script += "\n" + await failureText.ReadToEndAsync(deadline.Token);
        script += "\n" + await profileText.ReadToEndAsync(deadline.Token);
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

    [Fact]
    public async Task RestartActualHostedWorkerAndCompletePersistedDocumentAfterOwnerTermination()
        => await RunHostedRecoveryAsync(null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveCapturedLegacyOccurrenceThroughActualHostedSuccessOrFailure(bool failCurrentOccurrence)
        => await RunHostedRecoveryAsync(failCurrentOccurrence);

    private async Task RunHostedRecoveryAsync(bool? failLegacyOccurrence)
    {
        // Run only under the existing reviewed Windows Job launcher. Children inherit its non-breakaway Job.
        OperatingSystem.IsWindows().ShouldBeTrue("This arm requires the existing owned Windows Job launcher.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        var token = deadline.Token;
        var run = Guid.NewGuid();
        var children = new List<HostedProcess>();
        Exception? primaryException = null;
        var cleanupFailures = new List<Exception>();
        try
        {
            var first = new HostedProcess(_connectionString, run, "hosted-produce", null);
            children.Add(first);
            first.Handle.ShouldNotBe(nint.Zero);
            WriteEvidence($"child-start mode=hosted-produce run={run} pid={first.Id} createdUtc={first.CreatedUtc:O} handleRecorded=true");
            using var produced = await first.ReadAsync("produced", token);
            var id = produced.RootElement.GetProperty("ScheduleId").GetGuid();
            id.ShouldNotBe(Guid.Empty);
            using var readyA = await first.ReadAsync("worker-ready", token);
            using var enteredA = await first.ReadAsync("handler-entered", token);
            AssertEntered(enteredA.RootElement, run, id, first.Id);
            var scopeA = enteredA.RootElement.GetProperty("ScopedInstanceId").GetGuid();
            var liveA = await ObserveAsync(id, token);
            AssertUncommitted(liveA);
            liveA.Owner.ShouldNotBeNull();
            liveA.Expiry.ShouldNotBeNull();
            (liveA.Expiry > liveA.ServerUtc).ShouldBeTrue("Observe an actually live A lease before the intentional crash.");
            liveA.Occurrence.ShouldNotBeNull();
            var ownerA = liveA.Owner!.Value;
            first.TimedOut.ShouldBeFalse();
            first.HasExited.ShouldBeFalse("Handler timeout/graceful shutdown cannot substitute for the intended crash.");
            if (failLegacyOccurrence is not null)
            {
                await SetCapturedLegacyConfigurationAsync(id, liveA.Owner!.Value, liveA.Generation, token);
                await AssertCapturedLegacyConfigurationAsync(id, liveA.MessageBody, expectedBudget: 5, token);
            }
            await first.KillAsync(token);
            first.HasExited.ShouldBeTrue();
            first.TimedOut.ShouldBeFalse("Independent child lifetime expiry is failure, not the intended crash.");
            first.ExitCode.ShouldNotBe(0, "A must be terminated, not accepted as graceful success.");
            WriteEvidence($"A-exited pid={first.Id} createdUtc={first.CreatedUtc:O} exit={first.ExitCode}");
            var afterExit = await ObserveAsync(id, token);
            AssertUncommitted(afterExit);
            afterExit.Owner.ShouldBe(ownerA, "A's recorded grant must survive its observed process exit.");
            afterExit.Generation.ShouldBe(liveA.Generation);
            afterExit.Occurrence.ShouldBe(liveA.Occurrence);
            // This is an observed lower bound, not final expiry: an issued A renewal RPC may still commit.
            WriteEvidence($"A-expiry-observed-lower-bound server={afterExit.ServerUtc:O} expiry={afterExit.Expiry:O}");
            var mode = failLegacyOccurrence == true ? "hosted-resume-failure" : "hosted-resume";
            var second = new HostedProcess(_connectionString, run, mode, id);
            children.Add(second);
            second.Handle.ShouldNotBe(nint.Zero);
            WriteEvidence($"child-start mode={mode} run={run} pid={second.Id} createdUtc={second.CreatedUtc:O} handleRecorded=true");
            second.Id.ShouldNotBe(first.Id);
            using var existing = await second.ReadAsync("existing", token);
            existing.RootElement.GetProperty("ScheduleId").GetGuid().ShouldBe(id);
            using var readyB = await second.ReadAsync("worker-ready", token);
            var entering = second.ReadAsync("handler-entered", token);
            DateTimeOffset? bEnteredUtc = null;
            try
            {
                while (!entering.IsCompleted)
                {
                    var sample = await ObserveAsync(id, token);
                    sample.Generation.ShouldBe(liveA.Generation);
                    sample.MessageBody.ShouldBe(liveA.MessageBody);
                    if (sample.Owner == ownerA)
                    {
                        AssertUncommitted(sample);
                        WriteEvidence($"A-expiry-observed-lower-bound server={sample.ServerUtc:O} expiry={sample.Expiry:O}");
                    }
                    await Task.WhenAny(entering, Task.Delay(TimeSpan.FromMilliseconds(100), token));
                    token.ThrowIfCancellationRequested();
                }
                using var enteredB = await entering;
                AssertEntered(enteredB.RootElement, run, id, second.Id);
                bEnteredUtc = enteredB.RootElement.GetProperty("ObservedUtc").GetDateTimeOffset();
                enteredB.RootElement.GetProperty("ScopedInstanceId").GetGuid().ShouldNotBe(scopeA);
            }
            finally
            {
                if (!entering.IsCompleted)
                {
                    // The owned process is still disposed/awaited below; observe a later protocol fault too.
                    _ = entering.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
            var liveB = await ObserveAsync(id, token);
            AssertUncommitted(liveB);
            liveB.Owner.ShouldNotBeNull();
            liveB.Owner.ShouldNotBe(ownerA);
            liveB.Generation.ShouldBe(liveA.Generation);
            liveB.Occurrence.ShouldBe(liveA.Occurrence);
            liveB.MessageBody.ShouldBe(liveA.MessageBody);
            (liveB.Expiry > liveB.ServerUtc).ShouldBeTrue("Observe B's live grant while its actual handler remains blocked.");
            await second.SendAsync("allow-complete", token);
            if (failLegacyOccurrence == true)
            {
                using var quarantined = await second.ReadAsync("quarantined", token);
                quarantined.RootElement.GetProperty("Invocations").GetInt32().ShouldBe(4);
                quarantined.RootElement.GetProperty("HandlerFaults").GetInt32().ShouldBe(4);
                quarantined.RootElement.GetProperty("FutureConfigurationBlocks").GetInt32().ShouldBe(0);
                using var failedStopped = await second.ReadAsync("stopped", token);
                failedStopped.RootElement.GetProperty("Invocations").GetInt32().ShouldBe(4);
                failedStopped.RootElement.GetProperty("FutureConfigurationBlocks").GetInt32().ShouldBe(0);
                await second.WaitForSuccessAsync(token);
                var failedFinal = await ObserveAsync(id, token);
                failedFinal.Enabled.ShouldBeFalse();
                failedFinal.LastExecution.ShouldBeNull();
                failedFinal.Generation.ShouldBe(liveA.Generation);
                failedFinal.Occurrence.ShouldBeNull();
                failedFinal.MessageBody.ShouldBe(liveA.MessageBody);
                await AssertCapturedLegacyConfigurationAsync(id, liveA.MessageBody, expectedBudget: 5, token);
                var failureStore = NewPublicFailureStore();
                var retained = (await failureStore.GetOccurrenceFailureAsync(id, liveA.Generation,
                    liveA.Occurrence!.Value, "hosted-" + run.ToString("N"), token)).ShouldNotBeNull();
                retained.Disposition.ShouldBe(ScheduleFailureDisposition.Quarantined);
                retained.AdmissionAttempts.ShouldBe(6);
                retained.MaxAdmissionAttempts.ShouldBe(5);
                retained.Reason.ShouldBeOneOf(ScheduleFailureReason.HandlerFailed, ScheduleFailureReason.InvalidResult);
                retained.Message.TimeZoneId.ShouldBe("missing-installed-zone-for-hosted-regression");
                retained.Message.CronRecurrenceProfileId.ShouldBe("legacy-unknown");
                retained.Message.CronRecurrenceProfileVersion.ShouldBe(17);
                retained.Message.MessageBody.ShouldBe(liveA.MessageBody);
            }
            else
            {
                if (failLegacyOccurrence == false)
                {
                    using var diagnostic = await second.ReadAsync("future-configuration-blocked", token);
                    diagnostic.RootElement.GetProperty("ScheduleId").GetGuid().ShouldBe(id);
                    diagnostic.RootElement.GetProperty("FutureConfigurationBlocks").GetInt32().ShouldBe(1);
                }
                using var settled = await second.ReadAsync("settled", token);
                var settledExecution = settled.RootElement.GetProperty("LastExecutionUtc").GetDateTimeOffset();
                var settledObservedUtc = settled.RootElement.GetProperty("ObservedUtc").GetDateTimeOffset();
                bEnteredUtc.ShouldNotBeNull();
                // All three values use B's uncustomized TimeProvider.System clock. No SQL/A clock comparison.
                // Ordinary completion is after this actual handler entry and before B observes retirement.
                settledExecution.ShouldBeInRange(bEnteredUtc.GetValueOrDefault(), settledObservedUtc);
                using var stopped = await second.ReadAsync("stopped", token);
                stopped.RootElement.GetProperty("HandlerFault").GetBoolean().ShouldBeFalse();
                stopped.RootElement.GetProperty("HandlerFaults").GetInt32().ShouldBe(0);
                stopped.RootElement.GetProperty("Invocations").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
                if (failLegacyOccurrence == false)
                {
                    stopped.RootElement.GetProperty("Invocations").GetInt32().ShouldBe(1);
                    stopped.RootElement.GetProperty("FutureConfigurationBlocks").GetInt32().ShouldBe(1);
                }
                await second.WaitForSuccessAsync(token);
                var final = await ObserveAsync(id, token);
                final.Enabled.ShouldBeFalse();
                final.LastExecution.ShouldNotBeNull();
                final.LastExecution.ShouldBe(settledExecution);
                final.Generation.ShouldBe(liveA.Generation);
                final.MessageBody.ShouldBe(liveA.MessageBody);
                final.MessageName.ShouldBe(liveA.MessageName);
                var store = new SqlServerScheduleStore(Microsoft.Extensions.Options.Options.Create(new SqlServerScheduleStoreOptions
                {
                    ConnectionString = _connectionString, LeaseDuration = TimeSpan.FromSeconds(10),
                    CommandTimeoutSeconds = 10, LockTimeoutMilliseconds = 1000,
                }));
                var retired = (await store.GetAllAsync(token)).Single();
                retired.Id.ShouldBe(id);
                retired.Enabled.ShouldBeFalse();
                retired.LastExecutionUtc.ShouldNotBeNull();
                retired.LastExecutionUtc.ShouldBe(final.LastExecution);
                retired.MessageBody.ShouldBe(liveA.MessageBody);
                retired.TenantId.ShouldBe("hosted-" + run.ToString("N"));
                retired.CorrelationId.ShouldBe(run.ToString());
                if (failLegacyOccurrence == false)
                {
                    retired.TimeZoneId.ShouldBe("missing-installed-zone-for-hosted-regression");
                    retired.CronRecurrenceProfileId.ShouldBe("legacy-unknown");
                    retired.CronRecurrenceProfileVersion.ShouldBe(17);
                    retired.NextExecutionUtc.ShouldBe(liveA.Occurrence);
                    await AssertCapturedLegacyConfigurationAsync(id, liveA.MessageBody, expectedBudget: null, token);
                    (await NewPublicFailureStore().GetOccurrenceFailureAsync(id, liveA.Generation,
                        liveA.Occurrence!.Value, "hosted-" + run.ToString("N"), token)).ShouldBeNull();
                }
                // No exactly-once assertion: this is actual hosted recovery/settlement under at-least-once delivery.
            }
        }
        catch (Exception error)
        {
            primaryException = error;
        }
        finally
        {
            // Attempt cancellation and EVERY child disposal even if either starts/fails synchronously.
            // All asynchronous cleanup, including cancellation callbacks, shares the existing 15-second bound.
            var cleanupTasks = new List<Task>();
            try { cleanupTasks.Add(deadline.CancelAsync()); }
            catch (Exception error) { cleanupFailures.Add(error); }
            foreach (var child in children)
            {
                try { cleanupTasks.Add(child.DisposeAsync().AsTask()); }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
            var cleanup = Task.WhenAll(cleanupTasks);
            try { await cleanup.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception error)
            {
                if (cleanup.Exception is { } failures) { cleanupFailures.AddRange(failures.InnerExceptions); }
                else { cleanupFailures.Add(error); }
                // A timeout does not close streams beneath pending work. Observe any eventual fault.
                _ = cleanup.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        if (primaryException is not null)
        {
            if (cleanupFailures.Count > 0)
            {
                var failures = new List<Exception> { primaryException };
                failures.AddRange(cleanupFailures);
                throw new AggregateException("Hosted restart assertion and owned cleanup both failed.", failures);
            }
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        }
        if (cleanupFailures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        }
        if (cleanupFailures.Count > 1)
        {
            throw new AggregateException("Hosted restart owned cleanup failed.", cleanupFailures);
        }
    }

    private IScheduleExecutionFailureStore NewPublicFailureStore() => new SqlServerScheduleStore(
        global::Microsoft.Extensions.Options.Options.Create(new SqlServerScheduleStoreOptions
        {
            ConnectionString = _connectionString, LeaseDuration = TimeSpan.FromSeconds(10),
            CommandTimeoutSeconds = 10, LockTimeoutMilliseconds = 1000,
        }));

    private async Task SetCapturedLegacyConfigurationAsync(Guid id, Guid owner, Guid generation, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("UPDATE dispatch.ScheduledMessage SET CronExpression=N'* * * * *',TimeZoneId=N'missing-installed-zone-for-hosted-regression',CronRecurrenceProfileId=N'legacy-unknown',CronRecurrenceProfileVersion=17 WHERE Id=@Id AND Generation=@Generation AND OwnerToken=@Owner AND CapturedMaxAdmissionAttempts=5 AND AdmissionAttempts=1 AND ProgressCommitted=0 AND LeaseExpiresUtc>SYSUTCDATETIME()", connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Generation", SqlDbType.UniqueIdentifier).Value = generation;
        command.Parameters.Add("@Owner", SqlDbType.UniqueIdentifier).Value = owner;
        (await command.ExecuteNonQueryAsync(token)).ShouldBe(1, "Seed legacy configuration only under the actual first captured live A grant.");
    }

    private async Task AssertCapturedLegacyConfigurationAsync(Guid id, string originalBody, int? expectedBudget, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand("SELECT MessageBody,CronExpression,TimeZoneId,CronRecurrenceProfileId,CronRecurrenceProfileVersion,CapturedMaxAdmissionAttempts FROM dispatch.ScheduledMessage WHERE Id=@Id", connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();
        reader.GetString(0).ShouldBe(originalBody);
        reader.GetString(1).ShouldBe("* * * * *");
        reader.GetString(2).ShouldBe("missing-installed-zone-for-hosted-regression");
        reader.GetString(3).ShouldBe("legacy-unknown");
        reader.GetInt32(4).ShouldBe(17);
        (reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)).ShouldBe(expectedBudget);
    }

    private static void AssertEntered(JsonElement row, Guid run, Guid id, int pid)
    {
        row.GetProperty("RunId").GetGuid().ShouldBe(run);
        row.GetProperty("ScheduleId").GetGuid().ShouldBe(id);
        row.GetProperty("ProcessId").GetInt32().ShouldBe(pid);
        pid.ShouldNotBe(Environment.ProcessId);
        row.GetProperty("Payload").GetString().ShouldBe("hosted-recovery\n用户");
        row.GetProperty("Tenant").GetString().ShouldBe("hosted-" + run.ToString("N"));
        row.GetProperty("AmbientTenant").GetString().ShouldBe("hosted-" + run.ToString("N"));
        row.GetProperty("Correlation").GetString().ShouldBe(run.ToString());
        row.GetProperty("ScopedInstanceId").GetGuid().ShouldNotBe(Guid.Empty);
        row.GetProperty("HandlerFault").GetBoolean().ShouldBeFalse();
        row.GetProperty("HandlerFaults").GetInt32().ShouldBe(0);
    }

    private static void AssertUncommitted(SqlObservation row)
    {
        row.Enabled.ShouldBeTrue();
        row.LastExecution.ShouldBeNull();
        row.ProgressCommitted.ShouldBeFalse();
    }

    private async Task<SqlObservation> ObserveAsync(Guid id, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        const string sql = "SELECT Generation, OwnerToken, AdmittedOccurrence, LeaseExpiresUtc, LastExecutionUtc, Enabled, ProgressCommitted, CAST(SYSUTCDATETIME() AS datetimeoffset(7)), MessageName, MessageBody FROM dispatch.ScheduledMessage WHERE Id=@Id";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();
        var value = new SqlObservation(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4), reader.GetBoolean(5), reader.GetBoolean(6),
            reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8), reader.GetString(9));
        (await reader.ReadAsync(token)).ShouldBeFalse();
        WriteEvidence($"SQL id={id} generation={value.Generation} owner={value.Owner} occurrence={value.Occurrence:O} expiry={value.Expiry:O} server={value.ServerUtc:O} enabled={value.Enabled} progress={value.ProgressCommitted} last={value.LastExecution:O}");
        return value;
    }

    private static void WriteEvidence(string message) => TestContext.Current.TestOutputHelper?.WriteLine(message);

    private sealed record SqlObservation(Guid Generation, Guid? Owner, DateTimeOffset? Occurrence,
        DateTimeOffset? Expiry, DateTimeOffset? LastExecution, bool Enabled, bool ProgressCommitted,
        DateTimeOffset ServerUtc, string MessageName, string MessageBody);

    private sealed class HostedProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _error;
        private readonly Task _lifetime;
        private int _timedOut;
        private readonly Guid _run;
        private readonly string _mode;
        public int Id => _process.Id;
        public bool HasExited => _process.HasExited;
        public bool TimedOut => Volatile.Read(ref _timedOut) != 0;
        public int ExitCode => _process.ExitCode;
        public nint Handle => _process.Handle;
        public DateTime CreatedUtc => _process.StartTime.ToUniversalTime();

        public HostedProcess(string connection, Guid run, string mode, Guid? schedule)
        {
            _run = run;
            _mode = mode;
            var path = Path.Combine(AppContext.BaseDirectory, "schedule-process-probe", "SqlServerScheduleProcessProbe.dll");
            File.Exists(path).ShouldBeTrue("Reuse the existing copied independent process harness.");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add(path);
            start.Environment["EXCALIBUR_SCHEDULE_TEST_CONNECTION"] = connection;
            start.Environment["EXCALIBUR_SCHEDULE_TEST_MODE"] = mode;
            start.Environment["EXCALIBUR_SCHEDULE_TEST_RUN"] = run.ToString();
            if (schedule is { } id) { start.Environment["EXCALIBUR_SCHEDULE_TEST_ID"] = id.ToString(); }
            _process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start owned hosted probe.");
            _error = _process.StandardError.ReadToEndAsync();
            _lifetime = EnforceLifetimeAsync();
        }

        public async Task<JsonDocument> ReadAsync(string stage, CancellationToken token)
        {
            var line = await _process.StandardOutput.ReadLineAsync(token)
                ?? throw new InvalidOperationException("Hosted probe exited before protocol reply: " + await _error.WaitAsync(token));
            var document = JsonDocument.Parse(line);
            try
            {
                var row = document.RootElement;
                row.GetProperty("Stage").GetString().ShouldBe(stage);
                row.GetProperty("Mode").GetString().ShouldBe(_mode);
                row.GetProperty("RunId").GetGuid().ShouldBe(_run);
                row.GetProperty("ProcessId").GetInt32().ShouldBe(Id);
                WriteEvidence($"child-protocol stage={stage} mode={_mode} run={_run} pid={Id}");
                return document;
            }
            catch { document.Dispose(); throw; }
        }

        public async Task SendAsync(string command, CancellationToken token)
        {
            await _process.StandardInput.WriteLineAsync(command.AsMemory(), token);
            await _process.StandardInput.FlushAsync(token);
        }

        public async Task KillAsync(CancellationToken token)
        {
            _process.HasExited.ShouldBeFalse("A exited before the intended crash-stop action.");
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(token);
            (await _error.WaitAsync(token)).ShouldBeEmpty("A reported a graceful fault/timeout before its intended termination.");
        }

        public async Task WaitForSuccessAsync(CancellationToken token)
        {
            await _process.WaitForExitAsync(token);
            TimedOut.ShouldBeFalse("Independent hosted child 90-second lifetime expired.");
            _process.ExitCode.ShouldBe(0, await _error.WaitAsync(token));
        }

        public async ValueTask DisposeAsync()
        {
            var pid = Id;
            var cleanup = FinishCleanupAsync();
            try { await cleanup.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception error)
            {
                _ = cleanup.ContinueWith(static completed => { _ = completed.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new InvalidOperationException($"Owned hosted probe PID {pid} cleanup failed or exceeded 15 seconds; exit is unverified.", error);
            }
        }

        private async Task EnforceLifetimeAsync()
        {
            try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
            catch (TimeoutException)
            {
                Interlocked.Exchange(ref _timedOut, 1);
                WriteEvidence($"child-lifetime-timeout pid={Id} seconds=90");
                if (!_process.HasExited) { _process.Kill(entireProcessTree: true); }
                await _process.WaitForExitAsync();
            }
        }

        private async Task FinishCleanupAsync()
        {
            try
            {
                if (!_process.HasExited) { _process.Kill(entireProcessTree: true); }
                await _process.WaitForExitAsync();
                await _error;
                await _lifetime;
                WriteEvidence($"child-observed-exit pid={Id} exit={_process.ExitCode}");
            }
            finally
            {
                if (_process.HasExited && _error.IsCompleted && _lifetime.IsCompleted) { _process.Dispose(); }
            }
        }
    }
}
