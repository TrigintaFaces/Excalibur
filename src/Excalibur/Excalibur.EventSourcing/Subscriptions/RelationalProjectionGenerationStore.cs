// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>Provider-neutral routing statements; schemas enforce the lifetime reference with a foreign key.</summary>
internal sealed class RelationalProjectionGenerationStore(
	Func<CancellationToken, Task<DbConnection>> open,
	Func<DbConnection, string, (string Name, object? Value)[], DbCommand> command,
	Func<DbException, bool> isConflict,
	string membersTable, string readersTable, bool snakeCase = false, bool oracle = false) : IProjectionGenerationStore
{
	private string Column(string name) => snakeCase ? name switch
	{
		"SubscriptionName" => "subscription_name", "ProjectionKey" => "projection_key",
		"DefinitionHash" => "definition_hash", "BindingId" => "binding_id", "ReaderName" => "reader_name",
		"DestinationScope" => "destination_scope", "Destination" => "destination", "Position" => "position", _ => name,
	} : name;
	private string Parameter(string name) => (oracle ? ":" : "@") + name;
	private string Equal(string column, string parameter) => column + (oracle ? " COLLATE BINARY" : string.Empty) + " = " + Parameter(parameter);

	public async Task<ProjectionGeneration?> ReadGenerationAsync(string subscriptionName, string readerName, CancellationToken cancellationToken)
	{
		Validate(subscriptionName, readerName);
		await using var connection = await open(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
		await using var query = command(connection,
			$"SELECT r.Revision, m.{Column("ProjectionKey")}, m.{Column("DefinitionHash")}, m.{Column("Destination")}, m.{Column("BindingId")}, m.{Column("Position")}, m.{Column("DestinationScope")} " +
			$"FROM {readersTable} r JOIN {membersTable} m ON r.{Column("BindingId")} = m.{Column("BindingId")} " +
			$"WHERE {Equal("r." + Column("SubscriptionName"), "Name")} AND {Equal("r." + Column("ReaderName"), "Reader")}",
			[("Name", subscriptionName), ("Reader", readerName)]);
		query.Transaction = transaction;
		ProjectionGeneration? result = null;
		await using (var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
		{
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				result = new(Guid.Parse(reader.GetString(0)), new SubscriptionMember(reader.GetString(1), reader.GetString(2),
					await reader.IsDBNullAsync(3, cancellationToken).ConfigureAwait(false) ? string.Empty : reader.GetString(3))
				{
					BindingId = Guid.Parse(reader.GetString(4)),
                    DestinationScope = await reader.IsDBNullAsync(6, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(6),
					Position = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
				});
			}
		}
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		return result;
	}

	public async Task<bool> TrySetGenerationAsync(string subscriptionName, string readerName, Guid? expectedRevision,
		string projectionKey, Guid bindingId, long minimumPosition, CancellationToken cancellationToken)
	{
		Validate(subscriptionName, readerName);
		SubscriptionDefinitionValidation.ValidateBinding(subscriptionName, projectionKey, bindingId);
		ArgumentOutOfRangeException.ThrowIfNegative(minimumPosition);
		if (expectedRevision == Guid.Empty) { throw new ArgumentException("A revision cannot be empty.", nameof(expectedRevision)); }
		await using var connection = await open(cancellationToken).ConfigureAwait(false);
		var target = $"SELECT {Column("BindingId")} FROM {membersTable} WHERE {Equal(Column("SubscriptionName"), "Name")} " +
			$"AND {Equal(Column("ProjectionKey"), "Key")} AND {Equal(Column("BindingId"), "Binding")} AND {Column("Position")} >= {Parameter("Minimum")}";
		var sql = expectedRevision is null
			? $"INSERT INTO {readersTable} ({Column("SubscriptionName")}, {Column("ReaderName")}, Revision, {Column("BindingId")}) " +
				$"SELECT {Parameter("Name")}, {Parameter("Reader")}, {Parameter("Revision")}, {Column("BindingId")} FROM ({target}) candidate"
			: $"UPDATE {readersTable} SET Revision = {Parameter("Revision")}, {Column("BindingId")} = {Parameter("Binding")} " +
				$"WHERE {Equal(Column("SubscriptionName"), "Name")} AND {Equal(Column("ReaderName"), "Reader")} " +
				$"AND {Equal("Revision", "Expected")} AND EXISTS ({target})";
		var parameters = new List<(string Name, object? Value)>
		{
			("Name", subscriptionName), ("Reader", readerName), ("Revision", Guid.NewGuid().ToString("D")),
			("Key", projectionKey), ("Binding", bindingId.ToString("D")), ("Minimum", minimumPosition),
		};
		if (expectedRevision is { } revision) { parameters.Add(("Expected", revision.ToString("D"))); }
		await using var query = command(connection, sql, [.. parameters]);
		try { return await query.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1; }
		catch (DbException error) when (isConflict(error)) { return false; }
	}

	public async Task<bool> TryRemoveGenerationAsync(string subscriptionName, string readerName, Guid expectedRevision, CancellationToken cancellationToken)
	{
		Validate(subscriptionName, readerName);
		if (expectedRevision == Guid.Empty) { throw new ArgumentException("A revision cannot be empty.", nameof(expectedRevision)); }
		await using var connection = await open(cancellationToken).ConfigureAwait(false);
		await using var query = command(connection,
			$"DELETE FROM {readersTable} WHERE {Equal(Column("SubscriptionName"), "Name")} AND {Equal(Column("ReaderName"), "Reader")} AND {Equal("Revision", "Expected")}",
			[("Name", subscriptionName), ("Reader", readerName), ("Expected", expectedRevision.ToString("D"))]);
		try { return await query.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1; }
		catch (DbException error) when (isConflict(error)) { return false; }
	}

	internal static void Validate(string subscriptionName, string readerName)
	{
		SubscriptionDefinitionValidation.ValidateName(subscriptionName, nameof(subscriptionName), 255);
		SubscriptionDefinitionValidation.ValidateName(readerName, nameof(readerName), 255);
		if (subscriptionName.Length > 255 || readerName.Length > 255) { throw new ArgumentException("Subscription and reader names must not exceed 255 characters."); }
	}
}
