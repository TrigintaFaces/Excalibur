// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    private async Task WritePhaseFactsAsync(string application, int pid, byte[] nonce, SqlConnection gate, string resource)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)); // deadline-ok: This short cap bounds failure-only diagnostic SQL collection, not SUT progress or the correctness oracle.
            await using var parent = new SqlCommand("SELECT APPLOCK_MODE('public',@name,'Session');", gate) { CommandTimeout = 2 };
            parent.Parameters.AddWithValue("@name", resource);
            var mode = await parent.ExecuteScalarAsync(timeout.Token);
            TestContext.Current.TestOutputHelper?.WriteLine($"phase-parent spid={gate.ServerProcessId} exclusive={Equals(mode, "Exclusive")}");
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(timeout.Token);
            const string sql = "SELECT TOP(32) s.session_id,CASE WHEN s.program_name=@app THEN 1 ELSE 0 END,CASE WHEN s.host_process_id=@pid THEN 1 ELSE 0 END,CASE WHEN s.context_info=@nonce THEN 1 ELSE 0 END,COALESCE(DATALENGTH(s.context_info),0),COALESCE(t.transaction_id,-1),COALESCE(r.blocking_session_id,-1),COALESCE(r.wait_type,N'none'),COALESCE(l.resource_type,N'none'),COALESCE(l.request_owner_type,N'none'),COALESCE(l.request_status,N'none'),COALESCE(l.request_mode,N'none'),COALESCE(l.request_owner_id,-1),CASE WHEN l.resource_database_id=DB_ID() THEN 1 ELSE 0 END,CASE WHEN CHARINDEX(@resource,l.resource_description)>0 THEN 1 ELSE 0 END,CASE WHEN EXISTS(SELECT 1 FROM sys.dm_tran_locks g WHERE g.request_session_id=@parent AND g.resource_type=l.resource_type AND g.resource_database_id=l.resource_database_id AND g.resource_description=l.resource_description AND g.resource_lock_partition=l.resource_lock_partition AND g.request_owner_type='SESSION' AND g.request_status='GRANT' AND g.request_mode='X') THEN 1 ELSE 0 END,CASE WHEN r.context_info=@nonce THEN 1 ELSE 0 END,COALESCE(DATALENGTH(r.context_info),0),CASE WHEN l.resource_lock_partition IS NULL THEN 1 ELSE 0 END,COALESCE(l.resource_lock_partition,-1),CASE WHEN EXISTS(SELECT 1 FROM sys.dm_tran_locks g WHERE g.request_session_id=@parent AND g.resource_type=l.resource_type AND g.resource_database_id=l.resource_database_id AND g.resource_description=l.resource_description AND g.request_owner_type='SESSION' AND g.request_status='GRANT' AND g.request_mode='X') THEN 1 ELSE 0 END,CASE WHEN EXISTS(SELECT 1 FROM sys.dm_tran_locks g WHERE g.request_session_id=@parent AND g.resource_type=l.resource_type AND g.resource_database_id=l.resource_database_id AND g.resource_description=l.resource_description AND (g.resource_lock_partition=l.resource_lock_partition OR (g.resource_lock_partition IS NULL AND l.resource_lock_partition IS NULL)) AND g.request_owner_type='SESSION' AND g.request_status='GRANT' AND g.request_mode='X') THEN 1 ELSE 0 END FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id LEFT JOIN sys.dm_tran_session_transactions t ON t.session_id=s.session_id LEFT JOIN sys.dm_tran_locks l ON l.request_session_id=s.session_id AND l.resource_type='APPLICATION' AND l.resource_database_id=DB_ID() WHERE s.session_id=@parent OR (s.database_id=DB_ID() AND (s.program_name=@app OR s.host_process_id=@pid)) ORDER BY s.session_id;";
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 2 };
            command.Parameters.AddWithValue("@app", application);
            command.Parameters.AddWithValue("@pid", pid);
            command.Parameters.AddWithValue("@nonce", nonce);
            command.Parameters.AddWithValue("@parent", gate.ServerProcessId);
            command.Parameters.AddWithValue("@resource", resource);
            await using var reader = await command.ExecuteReaderAsync(timeout.Token);
            var count = 0;
            while (await reader.ReadAsync(timeout.Token))
            {
                count++;
                TestContext.Current.TestOutputHelper?.WriteLine($"phase-facts session={reader.GetInt16(0)} appMatch={reader.GetInt32(1)} pidMatch={reader.GetInt32(2)} nonceMatch={reader.GetInt32(3)} contextBytes={reader.GetInt32(4)} transaction={reader.GetInt64(5)} blocker={reader.GetInt32(6)} wait={reader.GetString(7)} lock={reader.GetString(8)} owner={reader.GetString(9)} status={reader.GetString(10)} mode={reader.GetString(11)} ownerId={reader.GetInt64(12)} sameDb={reader.GetInt32(13)} resourceMatch={reader.GetInt32(14)} parentGrantMatch={reader.GetInt32(15)} requestNonceMatch={reader.GetInt32(16)} requestContextBytes={reader.GetInt32(17)} partitionNull={reader.GetInt32(18)} partition={reader.GetInt32(19)} parentDescriptionMatch={reader.GetInt32(20)} parentNullSafePartitionMatch={reader.GetInt32(21)}");
            }

            TestContext.Current.TestOutputHelper?.WriteLine($"phase-facts rows={count}");
        }
        catch (Exception diagnostic)
        {
            TestContext.Current.TestOutputHelper?.WriteLine($"phase-facts diagnosticException={diagnostic.GetType().FullName}");
        }
    }
}
