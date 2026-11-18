// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>A single owned physical SQL session; never reconnects or reacquires during verification.</summary>
internal interface ISqlServerElectionSession : IAsyncDisposable
{
    Task<int> AcquireAsync(string resource, int lockTimeoutMilliseconds, int commandTimeoutSeconds, CancellationToken cancellationToken);
    Task<SqlServerSessionProof> VerifyAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken);
    Task ReleaseAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken);
}

internal enum SqlServerSessionProof { Exclusive, Lost, Indeterminate }

/// <summary>Owns the supplied dedicated connection and the commands executed on that same session.</summary>
internal sealed class SqlServerElectionSession(SqlConnection connection) : ISqlServerElectionSession
{
    public async Task<int> AcquireAsync(string resource, int lockTimeoutMilliseconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "sys.sp_getapplock";
        command.CommandTimeout = commandTimeoutSeconds;
        AddResource(command, resource);
        _ = command.Parameters.Add("@LockMode", SqlDbType.VarChar, 32).Value = "Exclusive";
        _ = command.Parameters.Add("@LockOwner", SqlDbType.VarChar, 32).Value = "Session";
        _ = command.Parameters.Add("@DbPrincipal", SqlDbType.NVarChar, 128).Value = "public";
        _ = command.Parameters.Add("@LockTimeout", SqlDbType.Int).Value = lockTimeoutMilliseconds;
        var returned = command.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
        returned.Direction = ParameterDirection.ReturnValue;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return returned.Value is int result ? result : throw new InvalidOperationException("sp_getapplock returned no integer status.");
    }

    public async Task<SqlServerSessionProof> VerifyAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        if (connection.State is ConnectionState.Closed or ConnectionState.Broken) { return SqlServerSessionProof.Lost; }
        if (connection.State != ConnectionState.Open) { return SqlServerSessionProof.Indeterminate; }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT APPLOCK_MODE(N'public', @Resource, N'Session')";
        command.CommandTimeout = commandTimeoutSeconds;
        AddResource(command, resource);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is string mode ? ClassifyMode(mode) : SqlServerSessionProof.Indeterminate;
    }

    internal static SqlServerSessionProof ClassifyMode(string mode) => mode switch
    {
        "Exclusive" => SqlServerSessionProof.Exclusive,
        "NoLock" or "Shared" or "Update" or "IntentShared" or "IntentExclusive" or
            "SharedIntentExclusive" or "UpdateIntentExclusive" => SqlServerSessionProof.Lost,
        _ => SqlServerSessionProof.Indeterminate
    };

    public async Task ReleaseAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        if (connection.State is ConnectionState.Closed or ConnectionState.Broken) { return; }
        if (connection.State != ConnectionState.Open) { throw new InvalidOperationException("The owned SQL session has an unsettled command."); }
        await using var command = connection.CreateCommand();
        // Cleanup is idempotent for an unsuccessful/ambiguous acquire and an explicitly lost lock.
        // This checks the exact resource, principal and owner on the same retained session.
        command.CommandText = """
            IF APPLOCK_MODE(N'public', @Resource, N'Session') <> N'NoLock'
            BEGIN
                DECLARE @result int;
                EXEC @result = sys.sp_releaseapplock @Resource = @Resource, @LockOwner = N'Session', @DbPrincipal = N'public';
                SELECT @result;
            END
            ELSE SELECT 0;
            """;
        command.CommandTimeout = commandTimeoutSeconds;
        AddResource(command, resource);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is not int status || status < 0) { throw new InvalidOperationException("sp_releaseapplock did not confirm release."); }
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private static void AddResource(SqlCommand command, string resource) =>
        command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = resource;
}
