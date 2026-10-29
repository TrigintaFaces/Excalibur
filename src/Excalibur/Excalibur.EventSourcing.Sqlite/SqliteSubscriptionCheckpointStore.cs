// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Globalization;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Data.Sqlite;
using Excalibur.Data.Validation;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>Persists each member lifetime and its nullable cursor in one atomic row.</summary>
internal sealed class SqliteSubscriptionCheckpointStore : ISubscriptionCheckpointStore, IProjectionGenerationStore, IProjectionDestinationScope
{
    string IProjectionDestinationScope.Scope
    {
        get
        {
            using var connection = new SqliteConnection(_connectionString);
            return string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.Database)
                ? string.Empty
                : "sqlite/" + Uri.EscapeDataString(connection.DataSource) + "/" + Uri.EscapeDataString(connection.Database);
        }
    }

	private readonly string _connectionString;
	private readonly string _table;
	private readonly RelationalProjectionGenerationStore _generations;

	internal SqliteSubscriptionCheckpointStore(
		string connectionString,
		string table = "SubscriptionCheckpoints")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);

		// The table name is interpolated directly into DDL and statement text (SQL does not allow a
		// parameterized identifier), so it must be allowlist-validated before it can reach
		// SqliteTableInitializer or any query this store issues. Matches SqliteEventStore.
		SqlIdentifierValidator.ThrowIfInvalid(table, nameof(table));

		_connectionString = connectionString;
		_table = table;
		_generations = new(OpenAsync, Command, static error => error is SqliteException { SqliteErrorCode: 19 }, "[" + table + "]", "[" + table + "Readers]");
	}



	public Task<ProjectionGeneration?> ReadGenerationAsync(string subscriptionName, string readerName, CancellationToken cancellationToken) =>
		_generations.ReadGenerationAsync(subscriptionName, readerName, cancellationToken);

	public Task<bool> TrySetGenerationAsync(string subscriptionName, string readerName, Guid? expectedRevision,
		string projectionKey, Guid bindingId, long minimumPosition, CancellationToken cancellationToken) =>
		_generations.TrySetGenerationAsync(subscriptionName, readerName, expectedRevision, projectionKey, bindingId, minimumPosition, cancellationToken);

	public Task<bool> TryRemoveGenerationAsync(string subscriptionName, string readerName, Guid expectedRevision, CancellationToken cancellationToken) =>
		_generations.TryRemoveGenerationAsync(subscriptionName, readerName, expectedRevision, cancellationToken);

	public async Task<SubscriptionRoster> BindSubscriptionAsync(string subscriptionName,
		IReadOnlyList<SubscriptionMember> members, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.Validate(subscriptionName, members);
		{
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		foreach (var member in members)
		{
			await using var command = Command(connection,
				$"INSERT INTO [{_table}] (SubscriptionName, ProjectionKey, DefinitionHash, Destination, BindingId, DestinationScope) VALUES (@Name, @Key, @Hash, @Destination, @BindingId, @Scope) ON CONFLICT (SubscriptionName, ProjectionKey) DO NOTHING",
				("Name", subscriptionName), ("Key", member.ProjectionKey), ("Hash", member.DefinitionHash),
				("Destination", member.Destination), ("BindingId", Guid.NewGuid().ToString("D")), ("Scope", member.DestinationScope));
			try { _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
			catch (SqliteException error) when (error.SqliteErrorCode == 19) { /* The existing row wins; validate its definition below. */ }
		}
		}
		var roster = await ReadSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);
		var stored = roster.Members.ToDictionary(static member => member.ProjectionKey, StringComparer.Ordinal);
		foreach (var member in members)
		{
			if (!stored.TryGetValue(member.ProjectionKey, out var bound))
			{
				throw new InvalidOperationException("A member was retired while binding the subscription. Retry the binding.");
			}
			SubscriptionDefinitionValidation.RequireMemberMatch(subscriptionName, member, bound);
		}
		return roster;
	}

	public async Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateName(subscriptionName, nameof(subscriptionName), 255);
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = Command(connection,
			$"SELECT ProjectionKey, DefinitionHash, Destination, BindingId, Position, DestinationScope FROM [{_table}] WHERE SubscriptionName = @Name", ("Name", subscriptionName));
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		var members = new List<SubscriptionMember>();
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			members.Add(new SubscriptionMember(reader.GetString(0), reader.GetString(1),
				(await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false)) ? string.Empty : reader.GetString(2))
			{
				BindingId = Guid.Parse(reader.GetString(3)),
                DestinationScope = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(5),
				Position = (await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false)) ? null : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
			});
		}
		return new SubscriptionRoster(subscriptionName, members.AsReadOnly());
	}

	public async Task<bool> ReleaseSubscriptionMemberAsync(string subscriptionName, string projectionKey,
		Guid bindingId, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateBinding(subscriptionName, projectionKey, bindingId);
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = Command(connection,
			$"DELETE FROM [{_table}] WHERE SubscriptionName = @Name AND ProjectionKey = @Key AND BindingId = @BindingId",
			("Name", subscriptionName), ("Key", projectionKey), ("BindingId", bindingId.ToString("D")));
		try { return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1; }
		catch (DbException error) when (error is SqliteException { SqliteErrorCode: 19 }) { return false; }
	}

	public async Task<long?> GetCheckpointAsync(string subscriptionName, string projectionKey, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateName(subscriptionName, nameof(subscriptionName), 255);
		SubscriptionDefinitionValidation.ValidateName(projectionKey, nameof(projectionKey), 512);
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = Command(connection,
			$"SELECT Position FROM [{_table}] WHERE SubscriptionName = @Name AND ProjectionKey = @Key",
			("Name", subscriptionName), ("Key", projectionKey));
		var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
	}

	public async Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(string subscriptionName, string projectionKey,
		Guid bindingId, long? expectedPosition, long newPosition, CancellationToken cancellationToken)
	{
		SubscriptionDefinitionValidation.ValidateAdvance(subscriptionName, projectionKey, bindingId, expectedPosition, newPosition);
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		var expected = expectedPosition.HasValue ? "Position = @Expected" : "Position IS NULL";
		await using var command = Command(connection,
			$"UPDATE [{_table}] SET Position = @Position WHERE SubscriptionName = @Name AND ProjectionKey = @Key AND BindingId = @BindingId AND {expected}",
			("Position", newPosition), ("Name", subscriptionName), ("Key", projectionKey), ("BindingId", bindingId.ToString("D")));
		if (expectedPosition is { } prior) { AddParameter(command, "Expected", prior); }
		return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1
			? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded;
	}

	public async Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken)
	{
		await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = Command(connection,
			$"SELECT SubscriptionName, ProjectionKey, Position FROM [{_table}] WHERE Position IS NOT NULL");
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		var checkpoints = new List<SubscriptionCheckpoint>();
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			checkpoints.Add(new SubscriptionCheckpoint(reader.GetString(0), reader.GetString(1),
				Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture)));
		}
		return checkpoints;
	}

	private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
	{
		var connection = new SqliteConnection(new SqliteConnectionStringBuilder(_connectionString) { ForeignKeys = true }.ToString());
		try
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureCheckpointsTableAsync(connection, _table, cancellationToken).ConfigureAwait(false);
			return connection;
		}
		catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
	}

	private static DbCommand Command(DbConnection connection, string sql, params (string Name, object? Value)[] parameters)
	{
		var command = connection.CreateCommand();
#pragma warning disable CA2100 // Only provider-owned SQL and constructor-validated identifiers reach this helper.
		command.CommandText = sql;
#pragma warning restore CA2100
		foreach (var (name, value) in parameters) { AddParameter(command, name, value); }
		return command;
	}

	private static void AddParameter(DbCommand command, string name, object? value)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.DbType = value is long ? DbType.Int64 : DbType.String;
		parameter.Value = value ?? DBNull.Value;
		_ = command.Parameters.Add(parameter);
	}
}
