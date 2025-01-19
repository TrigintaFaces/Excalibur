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

	/// <summary>
	///     Gets the underlying database connection, ensuring it is in a ready state.
	/// </summary>
	internal IDbConnection Connection => _connection.Ready();

	/// <summary>
	///     Retrieves the last processed CDC position for a specified database and connection.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database. </param>
	/// <param name="cancellationToken"> The cancellation token to stop the operation if needed. </param>
	/// <returns> The last processed CDC position as a <see cref="CdcProcessingState" />, or <c> null </c> if not found. </returns>
	public async Task<CdcProcessingState?> GetLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT TOP 1
		                              DatabaseConnectionIdentifier,
		                              DatabaseName,
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

		var command = new CommandDefinition(CommandText, parameters: parameters, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await Connection.QuerySingleOrDefaultAsync<CdcProcessingState?>(command).ConfigureAwait(false);
	}

	/// <summary>
	///     Updates the last processed CDC position for a specified database and connection.
	/// </summary>
	/// <param name="databaseConnectionIdentifier"> The identifier for the database connection. </param>
	/// <param name="databaseName"> The name of the database. </param>
	/// <param name="position"> The LSN of the last processed position. </param>
	/// <param name="sequenceValue"> The sequence value of the last processed row. </param>
	/// <param name="commitTime"> The commit time of the last processed row. </param>
	/// <param name="cancellationToken"> The cancellation token to stop the operation if needed. </param>
	public async Task UpdateLastProcessedPositionAsync(
		string databaseConnectionIdentifier,
		string databaseName,
		byte[] position,
		byte[]? sequenceValue,
		DateTime commitTime,
		CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           MERGE
		                              Cdc.CdcProcessingState AS target
		                           USING ( VALUES
		                              (@databaseConnectionIdentifier, @databaseName, @position, @sequenceValue, @commitTime)) AS source
		                              (DatabaseConnectionIdentifier, DatabaseName, LastProcessedLsn, LastProcessedSequenceValue, LastCommitTime)
		                           ON
		                              target.DatabaseConnectionIdentifier = source.DatabaseConnectionIdentifier
		                           AND
		                              target.DatabaseName = source.DatabaseName
		                           WHEN MATCHED THEN
		                              UPDATE SET
		                                  LastProcessedLsn = source.LastProcessedLsn,
		                                  LastProcessedSequenceValue = source.LastProcessedSequenceValue,
		                                  LastCommitTime = source.LastCommitTime,
		                                  ProcessedAt = GETUTCDATE()
		                           WHEN NOT MATCHED THEN
		                              INSERT (DatabaseConnectionIdentifier, DatabaseName, LastProcessedLsn, LastProcessedSequenceValue, LastCommitTime)
		                              VALUES (source.DatabaseConnectionIdentifier, source.DatabaseName, source.LastProcessedLsn, source.LastProcessedSequenceValue, source.LastCommitTime);
		                           """;

		var parameters = new DynamicParameters();
		parameters.Add("databaseConnectionIdentifier", databaseConnectionIdentifier, DbType.String);
		parameters.Add("databaseName", databaseName, DbType.String);
		parameters.Add("position", position, DbType.Binary, size: 10);
		parameters.Add("sequenceValue", sequenceValue, DbType.Binary, size: 10);
		parameters.Add("commitTime", commitTime, DbType.DateTime2);

		var command = new CommandDefinition(CommandText, parameters: parameters, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		_ = await Connection.ExecuteAsync(command).ConfigureAwait(false);
	}
}
