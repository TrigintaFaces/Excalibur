// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using Dapper;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Explicit parameters for provider-owned commands. The Dapper view is retained for public request
/// compatibility; only internal execution uses the recorded types. Public requests still execute
/// through Dapper, including consumer changes to parameter metadata and templates.
/// </summary>
internal sealed class SqlServerParameterSet : DynamicParameters
{
    private readonly Dictionary<string, DbType?> _types = new(StringComparer.Ordinal);

    internal void Add(string name, object? value = null, DbType? dbType = null)
    {
        var type = dbType ?? value switch
        {
            string or null => DbType.String,
            long => DbType.Int64,
            int => DbType.Int32,
            short => DbType.Int16,
            bool => DbType.Boolean,
            Guid => DbType.Guid,
            DateTimeOffset => DbType.DateTimeOffset,
            DateTime => null,
            byte[] => DbType.Binary,
            _ => throw new ArgumentException("Provider parameters require an explicit supported database type.", nameof(value)),
        };
        base.Add(name, value, dbType);
        _types[name.TrimStart('@')] = type;
    }

    internal static SqlServerParameterSet Create(params (string Name, object? Value)[] values)
    {
        var parameters = new SqlServerParameterSet();
        foreach (var (name, value) in values) { parameters.Add(name, value); }
        return parameters;
    }

    internal static Task<T?> QuerySingleOrDefaultAsync<T>(IDbConnection connection, CommandDefinition definition) =>
        ExecuteAsync(connection, definition, static async (command, token) =>
        {
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) { return default; }
            var value = reader.GetValue(0);
            var result = value is DBNull ? default(T) : value is T typed ? typed
                : throw new InvalidOperationException("The SQL row did not match its declared provider type.");
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The SQL query returned more than one row for its identity.");
            }
            return result;
        });

    internal DbCommand CreateCommand(IDbConnection connection, in CommandDefinition definition)
    {
        var command = (DbCommand)connection.CreateCommand();
        try
        {
#pragma warning disable CA2100 // SQL is constructed by provider requests from validated identifiers; values are bound below.
            command.CommandText = definition.CommandText;
#pragma warning restore CA2100
            command.CommandType = definition.CommandType ?? CommandType.Text;
            command.Transaction = (DbTransaction?)definition.Transaction;
            if ((definition.CommandTimeout ?? SqlMapper.Settings.CommandTimeout) is { } timeout) { command.CommandTimeout = timeout; }
            foreach (var name in ParameterNames)
            {
                var value = Get<object?>(name);
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                if (_types[name] is { } type) { parameter.DbType = type; }
                parameter.Value = value ?? DBNull.Value;
                // Keep parameter sizes stable across values, matching the existing string binding.
                if (parameter.DbType == DbType.String && value is string { Length: <= 4000 }) { parameter.Size = 4000; }
                command.Parameters.Add(parameter);
            }
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    internal static async Task<T> ExecuteAsync<T>(IDbConnection connection, CommandDefinition definition,
        Func<DbCommand, CancellationToken, Task<T>> execute)
    {
        var parameters = (SqlServerParameterSet?)definition.Parameters ?? new SqlServerParameterSet();
        await using var command = parameters.CreateCommand(connection, definition);
        var dbConnection = (DbConnection)connection;
        var openedHere = connection.State == ConnectionState.Closed;
        try
        {
            if (openedHere) { await dbConnection.OpenAsync(definition.CancellationToken).ConfigureAwait(false); }
            return await execute(command, definition.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (openedHere) { await dbConnection.CloseAsync().ConfigureAwait(false); }
        }
    }

    internal static Task<int> ExecuteNonQueryAsync(IDbConnection connection, CommandDefinition definition) =>
        ExecuteAsync(connection, definition, static (command, token) => command.ExecuteNonQueryAsync(token));

    internal static Task<T?> ExecuteScalarAsync<T>(IDbConnection connection, CommandDefinition definition) =>
        ExecuteAsync(connection, definition, static async (command, token) =>
        {
            var value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value is null or DBNull) { return default; }
            if (value is T typed) { return typed; }
            throw new InvalidOperationException("The SQL scalar result did not match its declared provider type.");
        });
}
