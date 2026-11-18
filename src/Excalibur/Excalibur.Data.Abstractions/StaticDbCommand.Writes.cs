// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;

using Dapper;

namespace Excalibur.Data;

internal static partial class StaticDbCommand
{
	internal static T RequiredInput<T>(DynamicParameters parameters, string name) where T : class =>
		Input<T>(parameters, name) ?? throw new ArgumentException($"Required request input '{name}' cannot be null.", name);

	internal static Task<T> SingleAsync<T>(IDbConnection connection, CommandDefinition definition,
		Action<DbCommand> bind, Func<DbDataReader, T> materialize) =>
		SingleCoreAsync(connection, definition, bind, materialize, allowDefault: false);

	internal static Task<T> SingleOrDefaultAsync<T>(IDbConnection connection, CommandDefinition definition,
		Action<DbCommand> bind, Func<DbDataReader, T> materialize) =>
		SingleCoreAsync(connection, definition, bind, materialize, allowDefault: true);

	internal static async Task<T> ExecuteOutputsAsync<T>(IDbConnection connection, CommandDefinition definition,
		Action<DbCommand> bind, Func<DbCommand, T> readOutputs)
	{
		var wasClosed = connection.State == ConnectionState.Closed;
		Exception? primary = null;
		T result = default!;
		try
		{
			using var command = Create(connection, definition, bind);
			try
			{
				if (wasClosed)
				{
					await OpenAsync(connection, definition.CancellationToken).ConfigureAwait(false);
				}

				_ = await command.ExecuteNonQueryAsync(definition.CancellationToken).ConfigureAwait(false);
				// OUT values belong to this completed command and must be read before its disposal.
				result = readOutputs(command);
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

	internal static void AddNullableStringParameter(DbCommand command, string name, string? value)
	{
		if (value is not null)
		{
			AddParameter(command, name, value, DbType.String);
			return;
		}

		var parameter = command.CreateParameter();
		parameter.ParameterName = name.Length > 0 && name[0] is '@' or ':' or '?' ? name[1..] : name;
		parameter.Direction = ParameterDirection.Input;
		parameter.Value = DBNull.Value;
		parameter.DbType = DbType.String;
		_ = command.Parameters.Add(parameter);
	}

	internal static void AddOutputParameter(DbCommand command, string name, DbType type)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.Direction = ParameterDirection.Output;
		parameter.Value = DBNull.Value;
		parameter.DbType = type;
		_ = command.Parameters.Add(parameter);
	}

	internal static T Output<T>(DbCommand command, DynamicParameters observableParameters, string name, DbType type)
	{
		var value = command.Parameters[name].Value;
		if (value is not T result)
		{
			throw new InvalidDataException($"Database output '{name}' did not contain its declared CLR representation.");
		}

		// Preserve the existing request's public OUT-value observability when its bag is exposed.
		observableParameters.Add(name, result, type, ParameterDirection.Output);
		return result;
	}

	private static async Task<T> SingleCoreAsync<T>(IDbConnection connection, CommandDefinition definition,
		Action<DbCommand> bind, Func<DbDataReader, T> materialize, bool allowDefault)
	{
		var wasClosed = connection.State == ConnectionState.Closed;
		Exception? primary = null;
		T result = default!;
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

				reader = await command.ExecuteReaderAsync(CommandBehavior.Default, definition.CancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(definition.CancellationToken).ConfigureAwait(false) && reader.FieldCount != 0)
				{
					result = materialize(reader);
					if (await reader.ReadAsync(definition.CancellationToken).ConfigureAwait(false))
					{
						throw new InvalidOperationException("Sequence contains more than one element");
					}
				}
				else if (!allowDefault)
				{
					throw new InvalidOperationException("Sequence contains no elements");
				}

				while (await reader.NextResultAsync(definition.CancellationToken).ConfigureAwait(false))
				{
				}
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
}
