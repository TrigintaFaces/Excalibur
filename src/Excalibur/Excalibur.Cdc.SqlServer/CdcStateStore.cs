// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

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

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		using var command = CreateCommand(connection, commandText);
		AddParameter(command, "databaseConnectionIdentifier", databaseConnectionIdentifier, databaseConnectionIdentifier is null ? null : DbType.String);
		AddParameter(command, "databaseName", databaseName, databaseName is null ? null : DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<CdcProcessingState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadState(reader));
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) { }
        return rows;
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

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		using var command = CreateCommand(connection, commandText);
		AddParameter(command, "databaseConnectionIdentifier", databaseConnectionIdentifier, DbType.String);
		AddParameter(command, "databaseName", databaseName, DbType.String);
		AddParameter(command, "tableName", tableName, DbType.String);
		AddParameter(command, "position", position, DbType.Binary, 10);
		AddParameter(command, "sequenceValue", sequenceValue, DbType.Binary, 10);
		AddParameter(command, "commitTime", commitTime, DbType.DateTime2);
		AddParameter(command, "fencingToken", fencingToken, DbType.Int64);
		return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		using var command = CreateCommand(connection, commandText);
		AddParameter(command, "consumerId", consumerId, DbType.String);
		AddParameter(command, "discriminator", GenericDiscriminator, DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) { }
            return null;
        }
        var lsn = await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? null : await reader.GetFieldValueAsync<byte[]>(0, cancellationToken).ConfigureAwait(false);
        var sequence = await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false) ? null : await reader.GetFieldValueAsync<byte[]>(1, cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Sequence contains more than one element");
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) { }
        var position = new CdcPosition(lsn!, sequence);
		return position.IsValid ? position.ToChangePosition() : null;
	}

	/// <inheritdoc />
	async Task<CdcPositionSaveOutcome> ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
		ArgumentNullException.ThrowIfNull(position);

		var native = CdcPosition.FromChangePosition(position);
		var rowsWritten = await UpdateLastProcessedPositionAsync(
				consumerId,
				GenericDiscriminator,
				GenericDiscriminator,
				native.Lsn,
				native.SequenceValue,
				commitTime: null,
				fencingToken: null,
				cancellationToken)
			.ConfigureAwait(false);

		// This path presents no fencing token, and the MERGE's MATCHED guard begins with
		// `@fencingToken IS NULL` -- so the guard is satisfied unconditionally here and the update always
		// fires. There is therefore no reachable staleness decision to report, and mapping zero rows to
		// RefusedAsStale would describe a comparison this statement never performed. A zero is instead
		// unexplained, which is precisely Unknown: the caller must treat the checkpoint as not saved.
		return rowsWritten > 0 ? CdcPositionSaveOutcome.Advanced : CdcPositionSaveOutcome.Unknown;
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

		using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		using var command = CreateCommand(connection, commandText);
		AddParameter(command, "consumerId", consumerId, DbType.String);
		AddParameter(command, "discriminator", GenericDiscriminator, DbType.String);
		var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

        // Buffer before yielding: a paused consumer must not retain the operation's SQL session.
        var rows = new List<(string DatabaseConnectionIdentifier, byte[] LastProcessedLsn, byte[]? LastProcessedSequenceValue)>();
        using (var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        using (var command = CreateCommand(connection, commandText))
        {
            AddParameter(command, "discriminator", GenericDiscriminator, DbType.String);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? null! : reader.GetString(0),
                    await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false) ? null! : await reader.GetFieldValueAsync<byte[]>(1, cancellationToken).ConfigureAwait(false),
                    await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false) ? null : await reader.GetFieldValueAsync<byte[]>(2, cancellationToken).ConfigureAwait(false)));
            }
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false)) { }
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

    private static CdcProcessingState ReadState(DbDataReader reader) => new()
    {
        DatabaseConnectionIdentifier = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
        DatabaseName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        TableName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        LastProcessedLsn = reader.IsDBNull(3) ? new byte[10] : reader.GetFieldValue<byte[]>(3),
        LastProcessedSequenceValue = reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4),
        LastCommitTime = reader.IsDBNull(5) ? default : reader.GetDateTime(5),
        ProcessedAt = reader.IsDBNull(6) ? default : reader.GetFieldValue<DateTimeOffset>(6),
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Private callers supply fixed checkpoint SQL and a validated QualifiedTableName; all identifier values and positions are bound parameters.")]
    private static DbCommand CreateCommand(IDbConnection connection, string sql)
    {
        var candidate = connection.CreateCommand();
        if (candidate is not DbCommand command)
        {
            candidate.Dispose();
            throw new InvalidOperationException("CDC state operations require an asynchronous DbCommand.");
        }
        try
        {
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType? type, int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Direction = ParameterDirection.Input;
        parameter.Value = value ?? DBNull.Value;
        if (type.HasValue) { parameter.DbType = type.Value; }
        if (value is string { Length: <= 4000 }) { parameter.Size = 4000; }
        if (size.HasValue) { parameter.Size = size.Value; }
        command.Parameters.Add(parameter);
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
