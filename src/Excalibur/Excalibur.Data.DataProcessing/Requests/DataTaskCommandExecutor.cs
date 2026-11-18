// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using Dapper;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>Executes fixed data-task commands without runtime parameter or row generation.</summary>
internal static class DataTaskCommandExecutor
{
    internal static async Task<int> ExecuteAsync(IDbConnection connection, CommandDefinition definition, Action<DbCommand> bind)
    {
        using var command = CreateCommand(connection, definition);
        bind(command);
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) { await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false); }
            return await command.ExecuteNonQueryAsync(definition.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed) { connection.Close(); }
        }
    }

    internal static async Task<IEnumerable<DataTaskRequest>> ReadAsync(IDbConnection connection, CommandDefinition definition)
    {
        using var command = CreateCommand(connection, definition);
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) { await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false); }
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, definition.CancellationToken).ConfigureAwait(false);
            var tasks = new List<DataTaskRequest>();
            while (await reader.ReadAsync(definition.CancellationToken).ConfigureAwait(false))
            {
                tasks.Add(SqlDataTaskClaim.ReadTask(reader));
            }
            return tasks;
        }
        finally
        {
            if (wasClosed) { connection.Close(); }
        }
    }

    internal static void Add(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Direction = ParameterDirection.Input;
        parameter.Value = value ?? DBNull.Value;
        // DynamicParameters leaves a null input's provider type/size defaults untouched.
        if (value is not null) { parameter.DbType = type; }
        if (value is string text && text.Length <= 4000) { parameter.Size = 4000; }
        command.Parameters.Add(parameter);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Internal callers supply fixed task SQL with validated QualifiedTableName identifiers; all record data is passed through bound parameters.")]
    private static DbCommand CreateCommand(IDbConnection connection, CommandDefinition definition)
    {
        var raw = connection.CreateCommand();
        if (raw is not DbCommand command)
        {
            raw.Dispose();
            throw new InvalidOperationException("Async data processing requires CreateCommand() to return a DbCommand.");
        }
        try
        {
            command.CommandText = definition.CommandText;
            command.CommandType = definition.CommandType ?? CommandType.Text;
            if ((definition.CommandTimeout ?? SqlMapper.Settings.CommandTimeout) is { } timeout) { command.CommandTimeout = timeout; }
            ((IDbCommand)command).Transaction = definition.Transaction;
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private static Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken) =>
        connection is DbConnection database
            ? database.OpenAsync(cancellationToken)
            : throw new InvalidOperationException("Async data processing requires a DbConnection when opening a closed connection.");
}
