// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Examples.ECommerceSample;
using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    [Fact]
    public async Task RollBackTheOriginalTransactionAfterKillingTheActualSampleInsideItsInsertTrigger()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        var run = Guid.NewGuid();
        var application = "d06-" + run.ToString("N");
        var resource = application[..24];
        var nonce = run.ToByteArray();
        var childConnection = new SqlConnectionStringBuilder(_connectionString) { ApplicationName = application }.ConnectionString;
        await using var gate = new SqlConnection(_connectionString);
        await gate.OpenAsync(token);
        await AcquireGateAsync(gate, resource, token);
        await InstallInsertGateAsync(resource, nonce, token);
        CommerceChild? child = null;
        ObservedTransaction? observed = null;
        Exception? primary = null;
        Exception? cleanupError = null;
        var gateHeld = true;
        try
        {
            child = new CommerceChild(childConnection, "--stage-only");
            observed = await ObserveWaitingTransactionAsync(application, child.Id, nonce, gate, resource, token);
            // The physical INSERT is executing its trigger; StageMessageAsync has NOT returned.
            await child.KillAsync(token);
            await WaitForOriginalSqlExitAsync(application, child.Id, observed, token);
            (await CountsAsync(SqlCommerceProfile.DemoOrder(), token)).ShouldBe((0L, 0L, 0L));
            await ExecuteAsync("DROP TRIGGER dbo.D06PauseOutbox;", token);
            await ReleaseGateAsync(gate, resource, token);
            gateHeld = false;
            var retry = await RunChildAsync("--stage-only", token);
            retry.ShouldContain("SQL order and intent committed; same-input duplicate and conflict checks passed.");
            await AssertCommittedAsync(SqlCommerceProfile.DemoOrder(), token);
            var recovered = await RunChildAsync("--drain-only", token);
            AssertOneSenderReceipt(recovered);
            await AssertCommittedAsync(SqlCommerceProfile.DemoOrder(), token);
        }
        catch (Exception error)
        {
            primary = error;
            if (child != null)
            {
                await child.WriteSafeStatusAsync();
                await WritePhaseFactsAsync(application, child.Id, nonce, gate, resource);
            }

            throw;
        }
        finally
        {
            var failure = await CleanupGatedChildAsync(child, observed, application, gate, resource, gateHeld);
            if (primary != null && failure != null)
            {
                primary.Data["CommerceProcessCleanupFailure"] = failure;
            }
            else
            {
                cleanupError = failure;
            }
        }

        if (cleanupError != null)
        {
            throw cleanupError;
        }
    }

    [Fact]
    public async Task CommitInTheActualStageProcessAndDrainThatIntentInAFreshActualProcess()
    {
        var order = SqlCommerceProfile.DemoOrder();
        var stageOutput = await RunChildAsync("--stage-only", Ct);
        stageOutput.ShouldContain("SQL order and intent committed; same-input duplicate and conflict checks passed.");

        await AssertCommittedAsync(order, Ct);
        var payload = await IntentPayloadAsync(order, Ct);
        var evidence = await Subject().ReadAsync(order.OrderId, Ct);
        var output = await RunChildAsync("--drain-only", Ct);
        output.ShouldContain("Retained SQL order and committed intent evidence found; no submission was reissued.");
        // This exclusively owned database has exactly the one intent whose actual payload was read above.
        // The existing sender's Information receipt binds the real send to that payload's order/recipient.
        AssertOneSenderReceipt(output);
        output.ShouldContain("SQL outbox has no pending work.");

        (await Subject().ReadAsync(order.OrderId, Ct)).ShouldBe(evidence);
        (await IntentPayloadAsync(order, Ct)).ShouldBe(payload);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var sent = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.OutboxMessages WHERE Id=@id AND SentAt IS NOT NULL;", connection);
        sent.Parameters.AddWithValue("@id", new OrderSubmissionPlan(order).IntentId);
        (await sent.ExecuteScalarAsync(Ct)).ShouldBe(1L);
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Duplicate);
        await AssertCommittedAsync(order, Ct);
    }

    private static void AssertOneSenderReceipt(string output) => SenderReceiptCount(output).ShouldBe(1);

    private static int SenderReceiptCount(string output) =>
        output.Split(Environment.NewLine).Count(line => line.Contains(
            "Sent OrderConfirmation email to customer@example.test: Order sql-commerce-demo confirmed", StringComparison.Ordinal));

    private static async Task AcquireGateAsync(SqlConnection gate, string resource, CancellationToken token)
    {
        await using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@name,@LockMode='Exclusive',@LockOwner='Session',@DbPrincipal='public',@LockTimeout=1000; SELECT @r;", gate);
        command.Parameters.AddWithValue("@name", resource);
        Convert.ToInt32(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0);
    }

    private static async Task ReleaseGateAsync(SqlConnection gate, string resource, CancellationToken token)
    {
        await using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_releaseapplock @Resource=@name,@LockOwner='Session',@DbPrincipal='public'; SELECT @r;", gate);
        command.Parameters.AddWithValue("@name", resource);
        Convert.ToInt32(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0);
    }

    private Task InstallInsertGateAsync(string resource, byte[] nonce, CancellationToken token) => ExecuteAsync($"""
        CREATE TRIGGER dbo.D06PauseOutbox ON dbo.OutboxMessages AFTER INSERT AS
        BEGIN
            SET NOCOUNT ON;
            SET CONTEXT_INFO 0x{Convert.ToHexString(nonce)};
            DECLARE @r int;
            EXEC @r=sys.sp_getapplock @Resource=N'{resource}',@LockMode='Exclusive',@LockOwner='Transaction',@DbPrincipal='public',@LockTimeout=20000;
            IF @r < 0 THROW 51001, 'Owned phase gate did not grant; never continue as successful staging.', 1;
        END
        """, token);

    private async Task<ObservedTransaction> ObserveWaitingTransactionAsync(
        string application, int pid, byte[] nonce, SqlConnection gate, string resource, CancellationToken token)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(token);
        phase.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var parent = new SqlCommand("SELECT APPLOCK_MODE('public',@name,'Session');", gate);
            parent.Parameters.AddWithValue("@name", resource);
            (await parent.ExecuteScalarAsync(phase.Token)).ShouldBe("Exclusive");
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(phase.Token);
            await using var command = new SqlCommand("SELECT s.session_id,c.connection_id,t.transaction_id FROM sys.dm_exec_sessions s JOIN sys.dm_exec_connections c ON c.session_id=s.session_id JOIN sys.dm_tran_session_transactions t ON t.session_id=s.session_id JOIN sys.dm_exec_requests r ON r.session_id=s.session_id JOIN sys.dm_tran_locks l ON l.request_session_id=s.session_id AND l.request_owner_id=t.transaction_id JOIN sys.dm_tran_locks g ON g.resource_type=l.resource_type AND g.resource_database_id=l.resource_database_id AND g.resource_description=l.resource_description AND (g.resource_lock_partition=l.resource_lock_partition OR (g.resource_lock_partition IS NULL AND l.resource_lock_partition IS NULL)) AND g.request_session_id=@parent AND g.request_owner_type='SESSION' AND g.request_status='GRANT' AND g.request_mode='X' WHERE s.program_name=@app AND s.host_process_id=@pid AND r.context_info=@nonce AND l.resource_database_id=DB_ID() AND l.resource_type='APPLICATION' AND l.request_owner_type='TRANSACTION' AND l.request_status='WAIT' AND l.request_mode='X' AND r.blocking_session_id=@parent AND CHARINDEX(@resource,l.resource_description)>0 AND r.wait_type LIKE 'LCK_M_%';", connection);
            command.Parameters.AddWithValue("@app", application);
            command.Parameters.AddWithValue("@pid", pid);
            command.Parameters.AddWithValue("@nonce", nonce);
            command.Parameters.AddWithValue("@parent", gate.ServerProcessId);
            command.Parameters.AddWithValue("@resource", resource);
            await using var reader = await command.ExecuteReaderAsync(phase.Token);
            if (await reader.ReadAsync(phase.Token))
            {
                var observed = new ObservedTransaction(reader.GetInt16(0), reader.GetGuid(1), reader.GetInt64(2));
                TestContext.Current.TestOutputHelper?.WriteLine($"owned-insert-cut pid={pid} app={application} nonce={Convert.ToHexString(nonce)} session={observed.Session} connection={observed.Connection} transaction={observed.Transaction}");
                (await reader.ReadAsync(phase.Token)).ShouldBeFalse("Exactly one owned transactional INSERT must be waiting.");
                return observed;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), phase.Token); // delay-ok: Poll the exact owned child transaction/request nonce and parent application-lock wait under the original phase deadline.
        }
    }

    private async Task WaitForOriginalSqlExitAsync(string application, int pid, ObservedTransaction? observed, CancellationToken token)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
        wait.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(wait.Token);
            await using var command = new SqlCommand("SELECT (SELECT COUNT_BIG(*) FROM sys.dm_exec_sessions WHERE program_name=@app AND host_process_id=@pid) + (SELECT COUNT_BIG(*) FROM sys.dm_exec_connections WHERE connection_id=@connection) + (SELECT COUNT_BIG(*) FROM sys.dm_tran_active_transactions WHERE transaction_id=@transaction);", connection);
            command.Parameters.AddWithValue("@app", application);
            command.Parameters.AddWithValue("@pid", pid);
            command.Parameters.AddWithValue("@transaction", observed?.Transaction ?? -1L);
            command.Parameters.AddWithValue("@connection", observed?.Connection ?? Guid.Empty);
            if ((long)(await command.ExecuteScalarAsync(wait.Token))! == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), wait.Token); // delay-ok: Poll exact owned session/connection/transaction disappearance before the independent locking rollback read.
        }
    }

    private async Task<Exception?> CleanupGatedChildAsync(
        CommerceChild? child, ObservedTransaction? observed, string application, SqlConnection gate, string resource, bool gateHeld)
    {
        try
        {
            var pid = child?.Id;
            if (child != null)
            {
                await child.DisposeAsync();
            }

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (pid != null)
            {
                await WaitForOriginalSqlExitAsync(application, pid.Value, observed, deadline.Token);
            }

            if (gateHeld)
            {
                await ReleaseGateAsync(gate, resource, deadline.Token);
            }

            return null;
        }
        catch (Exception failure)
        {
            // Fail closed in this generated database rather than releasing a live writer into a commit.
            // ROLLBACK IMMEDIATE aborts every session in the owned database, including this parent gate.
            try
            {
                using var abort = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await DatabaseCommandAsync($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", abort.Token);
            }
            catch (Exception abortFailure)
            {
                return new AggregateException(failure, abortFailure);
            }

            return failure;
        }
    }

    private sealed record ObservedTransaction(short Session, Guid Connection, long Transaction);
}
