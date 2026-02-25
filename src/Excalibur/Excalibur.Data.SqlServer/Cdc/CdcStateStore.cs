// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides methods to manage the CDC (Change Data Capture) processing state in a database.
/// </summary>
public class CdcStateStore : ICdcStateStore
{
	private readonly IDbConnection _connection;
	private readonly SqlServerCdcStateStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class using an <see cref="IDbConnection" />.
	/// </summary>
	/// <param name="connection"> The database connection to use. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is <c> null </c>. </exception>
	public CdcStateStore(IDbConnection connection)
		: this(connection, new SqlServerCdcStateStoreOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class using an <see cref="IDbConnection" /> and options.
	/// </summary>
	/// <param name="connection">The database connection to use.</param>
	/// <param name="options">The CDC state store options.</param>
	public CdcStateStore(
		IDbConnection connection,
		IOptions<SqlServerCdcStateStoreOptions> options)
		: this(connection, options?.Value ?? throw new ArgumentNullException(nameof(options)))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class using a database abstraction <see cref="IDb" />.
	/// </summary>
	/// <param name="db"> The database abstraction providing the connection. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is <c> null </c>. </exception>
	public CdcStateStore(IDb db)
		: this(db, new SqlServerCdcStateStoreOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcStateStore" /> class using a database abstraction <see cref="IDb" /> and options.
	/// </summary>
	/// <param name="db">The database abstraction providing the connection.</param>
	/// <param name="options">The CDC state store options.</param>
	public CdcStateStore(
		IDb db,
		IOptions<SqlServerCdcStateStoreOptions> options)
		: this(db, options?.Value ?? throw new ArgumentNullException(nameof(options)))
	{
	}

	private CdcStateStore(IDbConnection connection, SqlServerCdcStateStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(connection);

		_connection = connection;
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
	}

	private CdcStateStore(IDb db, SqlServerCdcStateStoreOptions options)
		: this(db?.Connection ?? throw new ArgumentNullException(nameof(db)), options)
	{
	}

	/// <inheritdoc />
	public Task<IEnumerable<CdcProcessingState>> GetLastProcessedPositionAsync(
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

		return _connection.Ready().QueryAsync<CdcProcessingState>(command);
	}

	/// <inheritdoc />
	public Task<int> UpdateLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		string tableName,
		byte[] position,
		byte[]? sequenceValue,
		DateTime? commitTime,
		CancellationToken cancellationToken)
	{
		var commandText = $"""
		                                 MERGE
		                                      {_options.QualifiedTableName} AS target
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
		                   WHEN MATCHED THEN
		                   	UPDATE SET
		                   	LastProcessedLsn = source.LastProcessedLsn,
		                   	LastProcessedSequenceValue = source.LastProcessedSequenceValue,
		                   	LastCommitTime = source.LastCommitTime,
		                   	ProcessedAt = GETUTCDATE()
		                   WHEN NOT MATCHED THEN
		                   	INSERT (DatabaseConnectionIdentifier,
		                   	DatabaseName,
		                   	TableName,
		                   	LastProcessedLsn,
		                   	LastProcessedSequenceValue,
		                   	LastCommitTime, ProcessedAt)
		                       VALUES (source.DatabaseConnectionIdentifier, source.DatabaseName, source.TableName, source.LastProcessedLsn, source.LastProcessedSequenceValue, source.LastCommitTime, GETUTCDATE());
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("databaseConnectionIdentifier", databaseConnectionIdentifier, DbType.String);
		parameters.Add("databaseName", databaseName, DbType.String);
		parameters.Add("tableName", tableName, DbType.String);
		parameters.Add("position", position, DbType.Binary, size: 10);
		parameters.Add("sequenceValue", sequenceValue, DbType.Binary, size: 10);
		parameters.Add("commitTime", commitTime, DbType.DateTime2);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().ExecuteAsync(command);
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
		if (disposing)
		{
			_connection.Dispose();
		}
	}

	/// <summary>
	/// Core asynchronous disposal implementation.
	/// </summary>
	/// <returns> A value task that represents the asynchronous disposal operation. </returns>
	protected virtual async ValueTask DisposeCoreAsync() => await CdcDisposalHelper.SafeDisposeAsync(_connection).ConfigureAwait(false);
}
