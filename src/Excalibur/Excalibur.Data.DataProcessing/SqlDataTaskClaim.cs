// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Excalibur.Data.DataProcessing;

/// <summary>Owns one SQL Server task on one session until all processing has joined.</summary>
internal sealed class SqlDataTaskClaim : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly DataProcessingOptions _options;
    private readonly Guid _taskId;
    private readonly string _resource;
    private bool _acquisitionDispatched;

    private SqlDataTaskClaim(DbConnection connection, Guid taskId, DataProcessingOptions options)
    {
        _connection = connection;
        _taskId = taskId;
        _options = options;
        // Database-scoped, independent of schema/table casing and database collation.
        _resource = "Excalibur.DataTask." + taskId.ToString("N");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Ownership transfers to the caller on success; the asynchronous finally disposes every unsuccessful claim.")]
    internal static async Task<SqlDataTaskClaim?> TryAcquireAsync(
        Func<IDbConnection> factory, Guid taskId, DataProcessingOptions options, CancellationToken cancellationToken)
    {
        var connection = factory();
        if (connection is not DbConnection database)
        {
            connection?.Dispose();
            throw new InvalidOperationException("Data processing requires an owned DbConnection.");
        }

        SqlDataTaskClaim? claim = new(database, taskId, options);
        try
        {
            if (database.State != ConnectionState.Closed)
            {
                throw new InvalidOperationException("The orchestration connection factory must return a new, closed connection.");
            }
            // A cancelled/lost grant or release response leaves ownership uncertain.
            // A dedicated physical session ensures disposal cannot return that lock to a pool.
            var connectionString = new DbConnectionStringBuilder { ConnectionString = database.ConnectionString };
            connectionString["Pooling"] = false;
            database.ConnectionString = connectionString.ConnectionString;
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = claim.CreateCommand("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=@OwnershipResource,
                    @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0, @DbPrincipal='public';
                SELECT @result;
                """, guarded: false);
            claim._acquisitionDispatched = true;
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (result == -1)
            {
                return null;
            }
            if (result < 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException($"Acquiring data task ownership failed with SQL application-lock result {result}.");
            }
            var acquired = claim;
            claim = null;
            return acquired;
        }
        catch (Exception acquisitionFailure)
        {
            var failedClaim = claim;
            claim = null;
            try
            {
                if (failedClaim is not null)
                {
                    await failedClaim.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Task ownership acquisition and cleanup both failed.", acquisitionFailure, cleanupFailure);
            }
            throw;
        }
        finally
        {
            if (claim is not null)
            {
                await claim.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    internal async Task<DataTaskRequest?> ReadEligibleAsync(CancellationToken cancellationToken)
    {
        using var command = CreateCommand($"""
            SELECT DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts, CompletedCount, FetchCursor, ProcessedCursor
            FROM {_options.QualifiedTableName} WHERE DataTaskId=@DataTaskId AND Attempts < MaxAttempts;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTask(reader) : null;
    }

    internal async Task<int> CheckpointAsync(long count, string? cursor, CancellationToken cancellationToken)
    {
        using var command = CreateCommand($"""
            UPDATE {_options.QualifiedTableName}
            SET CompletedCount=@CompletedCount, ProcessedCursor=COALESCE(@ProcessedCursor, ProcessedCursor)
            WHERE DataTaskId=@DataTaskId;
            """);
        AddParameter(command, "@CompletedCount", DbType.Int64, count);
        AddParameter(command, "@ProcessedCursor", DbType.String, cursor);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task UpdateAttemptsAsync(int attempts, CancellationToken cancellationToken)
    {
        using var command = CreateCommand($"UPDATE {_options.QualifiedTableName} SET Attempts=@Attempts WHERE DataTaskId=@DataTaskId;");
        AddParameter(command, "@Attempts", DbType.Int32, attempts);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task DeleteAsync(CancellationToken cancellationToken)
    {
        using var command = CreateCommand($"DELETE {_options.QualifiedTableName} WHERE DataTaskId=@DataTaskId;");
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_acquisitionDispatched && _connection.State == ConnectionState.Open)
            {
                using var command = CreateCommand("IF APPLOCK_MODE(N'public', @OwnershipResource, N'Session') = N'Exclusive' EXEC sys.sp_releaseapplock @Resource=@OwnershipResource, @LockOwner='Session', @DbPrincipal='public';", guarded: false);
                _ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _acquisitionDispatched = false;
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static DataTaskRequest ReadTask(DbDataReader reader) => new()
    {
        DataTaskId = reader.GetGuid(0),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(1),
        RecordType = reader.GetString(2),
        Attempts = reader.GetInt32(3),
        MaxAttempts = reader.GetInt32(4),
        CompletedCount = reader.GetInt64(5),
        FetchCursor = reader.IsDBNull(6) ? null : reader.GetString(6),
        ProcessedCursor = reader.IsDBNull(7) ? null : reader.GetString(7),
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Only private fixed SQL templates and validated QualifiedTableName reach this method; all values are parameters.")]
    private DbCommand CreateCommand(string sql, bool guarded = true)
    {
        // Never reopen: a new session has no right to use the old session's claim.
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The data task ownership session is no longer open.");
        }
        var command = _connection.CreateCommand();
        command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
        command.CommandText = (guarded ? """
            IF COALESCE(APPLOCK_MODE(N'public', @OwnershipResource, N'Session'), N'NoLock') <> N'Exclusive'
                THROW 51001, 'Data task ownership was lost.', 1;

            """ : string.Empty) + sql;
        AddParameter(command, "@OwnershipResource", DbType.String, _resource);
        AddParameter(command, "@DataTaskId", DbType.Guid, _taskId);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        _ = command.Parameters.Add(parameter);
    }
}
