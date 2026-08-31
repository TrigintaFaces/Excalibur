// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Provides methods to manage the CDC (Change Data Capture) processing state in a database.
/// </summary>
public class CdcStateStore : ISqlServerCdcStateStore
{
	private readonly Func<IDbConnection> _connectionFactory;
	private readonly SqlServerCdcStateStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class.
	/// </summary>
	/// <param name="connectionFactory">
	/// Supplies a connection for each operation. The store opens the connection it is given, uses it for
	/// one statement, and disposes it, so pooled connections are returned promptly and two overlapping
	/// operations never share one.
	/// </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connectionFactory" /> is <c> null </c>. </exception>
	public CdcStateStore(Func<IDbConnection> connectionFactory)
		: this(connectionFactory, new SqlServerCdcStateStoreOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class with options.
	/// </summary>
	/// <param name="connectionFactory"> Supplies a connection for each operation. </param>
	/// <param name="options"> The CDC state store options. </param>
	public CdcStateStore(
		Func<IDbConnection> connectionFactory,
		IOptions<SqlServerCdcStateStoreOptions> options)
		: this(connectionFactory, options?.Value ?? throw new ArgumentNullException(nameof(options)))
	{
	}

	private CdcStateStore(Func<IDbConnection> connectionFactory, SqlServerCdcStateStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_connectionFactory = connectionFactory;
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
	}

	/// <inheritdoc />
	public async Task<IEnumerable<CdcProcessingState>> GetLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		CancellationToken cancellationToken)
	{
		var commandText = $"""
		                   SELECT
		                        DatabaseConnectionIdentifier,
		                        DatabaseName,
		                        TableName,
		                        LastProcessedLsn,
		                        LastProcessedSequenceValue,
		                        LastCommitTime,
		                        ProcessedAt
		                   FROM
		                        {_options.QualifiedTableName}
		                   WHERE
		                        DatabaseConnectionIdentifier = @databaseConnectionIdentifier
		                        AND
		                        DatabaseName = @databaseName
		                   ORDER BY
		                        MessageId DESC
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("databaseConnectionIdentifier", databaseConnectionIdentifier);
		parameters.Add("databaseName", databaseName);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		return await connection.QueryAsync<CdcProcessingState>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> UpdateLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		string tableName,
		byte[] position,
		byte[]? sequenceValue,
		DateTime? commitTime,
		long? fencingToken,
		CancellationToken cancellationToken)
	{
		// The checkpoint write is a non-decreasing compare-and-swap on FencingToken so a demoted-but-unaware
		// leader (split-brain: it still believes it leads) cannot regress or skip the LSN checkpoint. The
		// MATCHED update is guarded: it applies only when fencing is off (@fencingToken IS NULL), the row was
		// never fenced (target.FencingToken IS NULL — first fenced write over a pre-fencing row), or the
		// presented token is at least the stored one. A stale token fails the guard, no MATCHED action fires,
		// and because the row already matched the ON clause the NOT MATCHED insert does not fire either — so
		// 0 rows are written, which the caller reads as "superseded". FencingToken advances via COALESCE so an
		// unfenced write leaves the stored token untouched.
		var commandText = $"""
		                                 MERGE
		                                      {_options.QualifiedTableName} WITH (UPDLOCK, HOLDLOCK) AS target
		                                 USING ( VALUES
		                                      (@databaseConnectionIdentifier, @databaseName, @tableName, @position, @sequenceValue, @commitTime)) AS source
		                                      (DatabaseConnectionIdentifier,
		                   	DatabaseName,
		                   	TableName,
		                   	LastProcessedLsn,
		                   	LastProcessedSequenceValue, LastCommitTime)
		                   ON
		                   	target.DatabaseConnectionIdentifier = source.DatabaseConnectionIdentifier
		                   	AND
		                   	target.DatabaseName = source.DatabaseName
		                   	AND
		                   	target.TableName = source.TableName
		                   WHEN MATCHED AND (@fencingToken IS NULL OR target.FencingToken IS NULL OR target.FencingToken <= @fencingToken) THEN
		                   	UPDATE SET
		                   	LastProcessedLsn = source.LastProcessedLsn,
		                   	LastProcessedSequenceValue = source.LastProcessedSequenceValue,
		                   	LastCommitTime = source.LastCommitTime,
		                   	FencingToken = COALESCE(@fencingToken, target.FencingToken),
		                   	ProcessedAt = GETUTCDATE()
		                   WHEN NOT MATCHED THEN
		                   	INSERT (DatabaseConnectionIdentifier,
		                   	DatabaseName,
		                   	TableName,
		                   	LastProcessedLsn,
		                   	LastProcessedSequenceValue,
		                   	LastCommitTime, FencingToken, ProcessedAt)
		                       VALUES (source.DatabaseConnectionIdentifier, source.DatabaseName, source.TableName, source.LastProcessedLsn, source.LastProcessedSequenceValue, source.LastCommitTime, @fencingToken, GETUTCDATE());
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("databaseConnectionIdentifier", databaseConnectionIdentifier, DbType.String);
		parameters.Add("databaseName", databaseName, DbType.String);
		parameters.Add("tableName", tableName, DbType.String);
		parameters.Add("position", position, DbType.Binary, size: 10);
		parameters.Add("sequenceValue", sequenceValue, DbType.Binary, size: 10);
		parameters.Add("commitTime", commitTime, DbType.DateTime2);
		parameters.Add("fencingToken", fencingToken, DbType.Int64);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		return await connection.ExecuteAsync(command).ConfigureAwait(false);
	}

	// --- Generic ICdcStateStore adapter -------------------------------------------------------
	// Bridges the provider-neutral consumer-checkpoint contract onto the SQL Server state table.
	// The generic checkpoint for a consumer is stored as a dedicated row keyed by
	// DatabaseConnectionIdentifier = consumerId with empty DatabaseName/TableName discriminators,
	// mirroring the sentinel-row pattern used by the Postgres/Mongo/Cosmos/Dynamo/Firestore stores.

	private const string GenericDiscriminator = "";

	/// <inheritdoc />
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

		var commandText = $"""
		                   SELECT TOP 1 LastProcessedLsn, LastProcessedSequenceValue
		                   FROM {_options.QualifiedTableName}
		                   WHERE DatabaseConnectionIdentifier = @consumerId
		                         AND DatabaseName = @discriminator
		                         AND TableName = @discriminator
		                   ORDER BY MessageId DESC
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("consumerId", consumerId, DbType.String);
		parameters.Add("discriminator", GenericDiscriminator, DbType.String);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		var row = await connection
			.QuerySingleOrDefaultAsync<(byte[] LastProcessedLsn, byte[]? LastProcessedSequenceValue)?>(command)
			.ConfigureAwait(false);

		if (row is null)
		{
			return null;
		}

		var position = new CdcPosition(row.Value.LastProcessedLsn, row.Value.LastProcessedSequenceValue);
		return position.IsValid ? position.ToChangePosition() : null;
	}

	/// <inheritdoc />
	async Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
		ArgumentNullException.ThrowIfNull(position);

		var native = CdcPosition.FromChangePosition(position);
		_ = await UpdateLastProcessedPositionAsync(
				consumerId,
				GenericDiscriminator,
				GenericDiscriminator,
				native.Lsn,
				native.SequenceValue,
				commitTime: null,
				fencingToken: null,
				cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

		var commandText = $"""
		                   DELETE FROM {_options.QualifiedTableName}
		                   WHERE DatabaseConnectionIdentifier = @consumerId
		                         AND DatabaseName = @discriminator
		                         AND TableName = @discriminator
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("consumerId", consumerId, DbType.String);
		parameters.Add("discriminator", GenericDiscriminator, DbType.String);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		var affected = await connection.ExecuteAsync(command).ConfigureAwait(false);
		return affected > 0;
	}

	/// <inheritdoc />
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var commandText = $"""
		                   SELECT DatabaseConnectionIdentifier, LastProcessedLsn, LastProcessedSequenceValue
		                   FROM {_options.QualifiedTableName}
		                   WHERE DatabaseName = @discriminator AND TableName = @discriminator
		                   ORDER BY DatabaseConnectionIdentifier
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("discriminator", GenericDiscriminator, DbType.String);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		IEnumerable<(string DatabaseConnectionIdentifier, byte[] LastProcessedLsn, byte[]? LastProcessedSequenceValue)> rows;
		using (var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			rows = await connection
				.QueryAsync<(string DatabaseConnectionIdentifier, byte[] LastProcessedLsn, byte[]? LastProcessedSequenceValue)>(command)
				.ConfigureAwait(false);
		}

		foreach (var row in rows)
		{
			var position = new CdcPosition(row.LastProcessedLsn, row.LastProcessedSequenceValue);
			if (position.IsValid)
			{
				yield return (row.DatabaseConnectionIdentifier, position.ToChangePosition());
			}
		}
	}

	/// <summary>
	/// Asynchronously releases the unmanaged resources used by the <see cref="CdcStateStore" />.
	/// </summary>
	/// <returns> A value task that represents the asynchronous dispose operation. </returns>
	public async ValueTask DisposeAsync()
	{
		await DisposeCoreAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="CdcStateStore" />.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Core disposal implementation.
	/// </summary>
	/// <param name="disposing"> True if disposing managed resources; otherwise, false. </param>
	protected virtual void Dispose(bool disposing)
	{
		// Nothing to release: each operation opens and disposes its own connection. The disposal members
		// remain because the interface declares them.
	}

	/// <summary>
	/// Core asynchronous disposal implementation.
	/// </summary>
	/// <returns> A value task that represents the asynchronous disposal operation. </returns>
	protected virtual ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// Opens a connection for a single operation.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the open. </param>
	/// <returns> An open connection that the caller owns and must dispose. </returns>
	private async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		var connection = _connectionFactory();

		try
		{
			// DbConnection is the common case and opens without blocking a thread; the IDbConnection
			// fallback exists because the factory's type does not promise one.
			if (connection is DbConnection dbConnection)
			{
				await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				_ = connection.Ready();
			}

			return connection;
		}
		catch
		{
			connection.Dispose();
			throw;
		}
	}
}
