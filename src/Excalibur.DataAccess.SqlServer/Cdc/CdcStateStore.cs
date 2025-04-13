using System.Data;

using Dapper;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Provides methods to manage the CDC (Change Data Capture) processing state in a database.
/// </summary>
public class CdcStateStore : ICdcStateStore
{
	private readonly IDbConnection _connection;

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcStateStore" /> class using an <see cref="IDbConnection" />.
	/// </summary>
	/// <param name="connection"> The database connection to use. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is <c> null </c>. </exception>
	public CdcStateStore(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);

		_connection = connection;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcStateStore" /> class using a database abstraction <see cref="IDb" />.
	/// </summary>
	/// <param name="db"> The database abstraction providing the connection. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is <c> null </c>. </exception>
	public CdcStateStore(IDb db)
	{
		ArgumentNullException.ThrowIfNull(db);

		_connection = db.Connection;
	}

	/// <inheritdoc />
	public Task<IEnumerable<CdcProcessingState>> GetLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT
		                              DatabaseConnectionIdentifier,
		                              DatabaseName,
		                              TableName,
		                              LastProcessedLsn,
		                              LastProcessedSequenceValue,
		                              LastCommitTime,
		                              ProcessedAt
		                           FROM
		                               Cdc.CdcProcessingState
		                           WHERE
		                              DatabaseConnectionIdentifier = @databaseConnectionIdentifier
		                           AND
		                              DatabaseName = @databaseName
		                           ORDER BY
		                               Id DESC
		                           """;

		var parameters = new DynamicParameters();
		parameters.Add("databaseConnectionIdentifier", databaseConnectionIdentifier);
		parameters.Add("databaseName", databaseName);

		var command = new CommandDefinition(
			CommandText,
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
		const string CommandText = """
		                           MERGE
		                              Cdc.CdcProcessingState AS target
		                           USING ( VALUES
		                              (@databaseConnectionIdentifier, @databaseName, @tableName, @position, @sequenceValue, @commitTime)) AS source
		                              (DatabaseConnectionIdentifier, DatabaseName, TableName, LastProcessedLsn, LastProcessedSequenceValue, LastCommitTime)
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
		                              INSERT (DatabaseConnectionIdentifier, DatabaseName, TableName, LastProcessedLsn, LastProcessedSequenceValue, LastCommitTime, ProcessedAt)
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
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().ExecuteAsync(command);
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_connection.Dispose();
		}
	}

	protected virtual async ValueTask DisposeAsyncCore() => await SafeDisposeAsync(_connection).ConfigureAwait(false);

	private static async ValueTask SafeDisposeAsync(object resource)
	{
		switch (resource)
		{
			case IAsyncDisposable resourceAsyncDisposable:
				await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;

			case IDisposable disposable:
				disposable.Dispose();
				break;

			default:
				break;
		}
	}
}
