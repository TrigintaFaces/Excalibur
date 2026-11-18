// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Reads the recorded fencing high-water mark for a scope, or <see langword="null"/> when the scope has
/// never been fenced.
/// </summary>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fence is keyed on the outbox table, not on a tenant: a fencing token orders competing processors of one table and has no tenant dimension to scope to")]
internal sealed class GetOutboxFenceHighWaterRequest : DataRequestBase<IDbConnection, long?>
{
	/// <remarks>
	/// This framework request binds its fixed primitive input and output schema without runtime emit.
	/// Replacing a known parameter value is observed at execution; custom parameter metadata, templates
	/// and output callbacks are not an extension contract. Custom IDataRequest implementations are unchanged.
	/// </remarks>
	public GetOutboxFenceHighWaterRequest(
		string fenceTableName,
		string scope,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		var sql = $"SELECT HighWaterToken FROM {fenceTableName} WHERE OutboxTable = @Scope";
		var parameters = new DynamicParameters();
		parameters.Add("@Scope", scope);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await StaticDbCommand.SingleOrDefaultAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "Scope", StaticDbCommand.Input<string>(Parameters, "Scope"), DbType.String);
			}, static reader => reader.IsDBNull(0) ? (long?)null : StaticDbValue.Int64(reader, 0)).ConfigureAwait(false);
	}
}

/// <summary>
/// Administratively sets the fencing high-water mark for a scope in one atomic statement.
/// </summary>
/// <remarks>
/// Without <c>force</c>, <c>WHEN MATCHED</c> carries an <c>AND</c> condition requiring the recorded value
/// to be no greater than the new one; a row that already exists with a higher value matches neither
/// <c>WHEN</c> branch (a match on <c>OutboxTable</c> exists, so <c>WHEN NOT MATCHED</c> does not fire
/// either) and the statement affects zero rows, which the caller reads as a refusal. A never-fenced scope
/// always inserts. With <c>force</c> the <c>AND</c> condition is omitted and the row is always written.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fence is keyed on the outbox table, not on a tenant: a fencing token orders competing processors of one table and has no tenant dimension to scope to")]
internal sealed class ResetOutboxFenceHighWaterRequest : DataRequestBase<IDbConnection, bool>
{
	/// <remarks>
	/// This framework request binds its fixed primitive input and output schema without runtime emit.
	/// Replacing a known parameter value is observed at execution; custom parameter metadata, templates
	/// and output callbacks are not an extension contract. Custom IDataRequest implementations are unchanged.
	/// </remarks>
	public ResetOutboxFenceHighWaterRequest(
		string fenceTableName,
		string scope,
		long newHighWater,
		bool force,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		var guard = force ? string.Empty : "AND f.HighWaterToken <= @NewHighWater";
		var sql = $"""
			MERGE {fenceTableName} WITH (UPDLOCK, HOLDLOCK) AS f
			USING (SELECT @Scope AS OutboxTable) AS s ON (f.OutboxTable = s.OutboxTable)
			WHEN MATCHED {guard} THEN
				UPDATE SET HighWaterToken = @NewHighWater
			WHEN NOT MATCHED THEN
				INSERT (OutboxTable, HighWaterToken) VALUES (@Scope, @NewHighWater);
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@Scope", scope);
		parameters.Add("@NewHighWater", newHighWater);

		SqlServerAffectedRows.Declare(parameters);
		Command = CreateCommand(sql + SqlServerAffectedRows.Capture, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection => await StaticDbCommand.ExecuteOutputsAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "Scope", StaticDbCommand.Input<string>(Parameters, "Scope"), DbType.String);
				StaticDbCommand.AddParameter(command, "NewHighWater", StaticDbCommand.Input<long>(Parameters, "NewHighWater"), DbType.Int64);
				SqlServerAffectedRows.Bind(command);
			}, command => SqlServerAffectedRows.Read(command, Parameters)).ConfigureAwait(false) == 1;
	}
}
