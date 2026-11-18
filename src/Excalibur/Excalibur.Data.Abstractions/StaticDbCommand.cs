// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Dapper;

namespace Excalibur.Data;

/// <summary>Executes framework-owned, statically bound requests without runtime row or parameter emit.</summary>
internal static partial class StaticDbCommand
{
	internal static T Input<T>(DynamicParameters parameters, string name)
	{
		try
		{
			return parameters.Get<T>(name);
		}
		catch (Exception exception) when (exception is KeyNotFoundException or InvalidCastException or ApplicationException or NullReferenceException)
		{
			throw new ArgumentException($"Request input '{name}' must have its constructor-defined CLR representation.", name, exception);
		}
	}

	internal static async Task<IEnumerable<T>> QueryAsync<T>(
		IDbConnection connection,
		CommandDefinition definition,
		Action<DbCommand> bind,
		Func<DbDataReader, T> materialize)
	{
		var wasClosed = connection.State == ConnectionState.Closed;
		Exception? primary = null;
		IEnumerable<T> result = [];
		try
		{
			using var command = Create(connection, definition, bind);
			DbDataReader? reader = null;
			try
			{
				if (wasClosed)
				{
					await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false);
				}

				// Oracle does not support SequentialAccess. These bounded requests buffer their first result.
				reader = await command.ExecuteReaderAsync(CommandBehavior.Default, definition.CancellationToken).ConfigureAwait(false);
				var rows = new List<T>();
				while (await reader.ReadAsync(definition.CancellationToken).ConfigureAwait(false))
				{
					rows.Add(materialize(reader));
				}

				while (await reader.NextResultAsync(definition.CancellationToken).ConfigureAwait(false))
				{
					// Match the buffered request contract: do not return with a pending additional result.
				}

				result = rows;
			}
			catch (Exception error)
			{
				primary = error;
			}
			finally
			{
				primary = await CleanupReaderAndConnectionAsync(reader, connection, wasClosed, primary).ConfigureAwait(false);
			}
		}
		catch (Exception error)
		{
			primary = CaptureFailure(primary, error, "CommandCreateOrDispose");
		}

		ThrowPrimary(primary);
		return result;
	}

	internal static async Task<int> ExecuteAsync(IDbConnection connection, CommandDefinition definition, Action<DbCommand> bind)
	{
		var wasClosed = connection.State == ConnectionState.Closed;
		Exception? primary = null;
		var result = 0;
		try
		{
			using var command = Create(connection, definition, bind);
			try
			{
				if (wasClosed)
				{
					await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false);
				}

				result = await command.ExecuteNonQueryAsync(definition.CancellationToken).ConfigureAwait(false);
			}
			catch (Exception error)
			{
				primary = error;
			}
			finally
			{
				primary = CloseOwnedConnection(connection, wasClosed, primary);
			}
		}
		catch (Exception error)
		{
			primary = CaptureFailure(primary, error, "CommandCreateOrDispose");
		}

		ThrowPrimary(primary);
		return result;
	}

	internal static async Task<object?> ScalarAsync(IDbConnection connection, CommandDefinition definition, Action<DbCommand> bind)
	{
		var wasClosed = connection.State == ConnectionState.Closed;
		Exception? primary = null;
		object? result = null;
		try
		{
			using var command = Create(connection, definition, bind);
			try
			{
				if (wasClosed)
				{
					await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false);
				}

				result = await command.ExecuteScalarAsync(definition.CancellationToken).ConfigureAwait(false);
			}
			catch (Exception error)
			{
				primary = error;
			}
			finally
			{
				primary = CloseOwnedConnection(connection, wasClosed, primary);
			}
		}
		catch (Exception error)
		{
			primary = CaptureFailure(primary, error, "CommandCreateOrDispose");
		}

		ThrowPrimary(primary);
		return result;
	}

	internal static void AddParameter(DbCommand command, string name, object? value, DbType type)
	{
		if (type == DbType.String && value is null)
		{
			throw new ArgumentException($"Required request input '{name}' cannot be null.", name);
		}

		var parameter = command.CreateParameter();
		parameter.ParameterName = name.Length > 0 && name[0] is '@' or ':' or '?' ? name[1..] : name;
		parameter.Direction = ParameterDirection.Input;
		parameter.Value = value ?? DBNull.Value;
		parameter.DbType = type;
		if (value is string { Length: <= 4000 })
		{
			// Preserve Dapper's scalar-string size, including the provider plan-cache shape.
			parameter.Size = 4000;
		}

		_ = command.Parameters.Add(parameter);
	}

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Command text is trusted framework request metadata; runtime values are bound separately. Identifier validation belongs to the request or provider configuration.")]
	private static DbCommand Create(IDbConnection connection, CommandDefinition definition, Action<DbCommand> bind)
	{
		var raw = connection.CreateCommand();
		try
		{
			if (raw is not DbCommand command)
			{
				throw new InvalidOperationException("Async operations require a DbCommand from the connection.");
			}

			if (definition.Transaction is not null)
			{
				raw.Transaction = definition.Transaction;
			}

			command.CommandText = definition.CommandText;
			if ((definition.CommandTimeout ?? SqlMapper.Settings.CommandTimeout) is { } timeout)
			{
				command.CommandTimeout = timeout;
			}

			command.CommandType = definition.CommandType ?? CommandType.Text;
			bind(command);
			return command;
		}
		catch (Exception primary)
		{
			try
			{
				raw.Dispose();
			}
			catch (Exception cleanup)
			{
				_ = CaptureFailure(primary, cleanup, "CommandCreateDispose");
			}

			throw;
		}
	}

	private static Task OpenAsync(IDbConnection connection, CancellationToken cancellationToken) =>
		connection is DbConnection dbConnection
			? dbConnection.OpenAsync(cancellationToken)
			: throw new InvalidOperationException("Async opening requires a DbConnection; an IDbConnection wrapper must already be open.");
}
