using System.Data;
using System.Data.Common;
using System.Globalization;

using Dapper;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Provides methods for interacting with SQL Server Change Data Capture (CDC) tables.
/// </summary>
public class CdcRepository : ICdcRepository
{
	private readonly IDbConnection _connection;

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcRepository" /> class using a provided database connection.
	/// </summary>
	/// <param name="connection"> The database connection to use for CDC queries. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is null. </exception>
	public CdcRepository(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);

		_connection = connection;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcRepository" /> class using a database wrapper.
	/// </summary>
	/// <param name="db"> The database wrapper providing the connection. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is null. </exception>
	public CdcRepository(IDb db)
	{
		ArgumentNullException.ThrowIfNull(db);

		_connection = db.Connection;
	}

	/// <inheritdoc />
	public Task<byte[]> GetNextLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_increment_lsn(@LastProcessedLsn);";

		var parameters = new DynamicParameters();
		parameters.Add("LastProcessedLsn", lastProcessedLsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleAsync<byte[]>(command);
	}

	/// <inheritdoc />
	public Task<byte[]?> GetNextLsnAsync(string captureInstance, byte[] lastProcessedLsn, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(captureInstance);
		ArgumentNullException.ThrowIfNull(lastProcessedLsn);

		var CommandText = $"""
		                   SELECT Top 1 __$start_lsn AS Next_LSN
		                   FROM cdc.{captureInstance}_CT
		                   WHERE __$start_lsn > @LastProcessedLsn
		                   ORDER BY __$start_lsn;
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("LastProcessedLsn", lastProcessedLsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleOrDefaultAsync<byte[]?>(command);
	}

	/// <inheritdoc />
	public Task<DateTime?> GetLsnToTimeAsync(byte[] lsn, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT CAST(sys.fn_cdc_map_lsn_to_time(@Lsn) AS DATETIME2(3)) AS LSN_TIME;";

		var parameters = new DynamicParameters();
		parameters.Add("Lsn", lsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleOrDefaultAsync<DateTime?>(command);
	}

	/// <inheritdoc />
	public Task<byte[]?> GetTimeToLsnAsync(DateTime lsnDate, string relationalOperator, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_map_time_to_lsn(@RelationalOperator, @LsnDate);";

		var parameters = new DynamicParameters();
		parameters.Add("LsnDate", lsnDate);
		parameters.Add("RelationalOperator", relationalOperator);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleAsync<byte[]?>(command);
	}

	/// <inheritdoc />
	public Task<byte[]> GetMinPositionAsync(string captureInstance, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_get_min_lsn(@CaptureInstance);";

		var parameters = new DynamicParameters();
		parameters.Add("CaptureInstance", captureInstance);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleAsync<byte[]>(command);
	}

	/// <inheritdoc />
	public Task<byte[]> GetMaxPositionAsync(CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_get_max_lsn();";

		var command = new CommandDefinition(
			CommandText,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return _connection.Ready().QuerySingleAsync<byte[]>(command);
	}

	/// <inheritdoc />
	public async Task<bool> ChangesExistAsync(
		byte[] fromPosition,
		byte[] toPosition,
		IEnumerable<string> captureInstances,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(captureInstances);

		foreach (var captureInstance in captureInstances)
		{
			var commandText = $"""
			                   SELECT TOP 1 1
			                   FROM cdc.fn_cdc_get_all_changes_{captureInstance}(@from_lsn, @to_lsn, N'all update old')
			                   """;

			var parameters = new DynamicParameters();
			parameters.Add("from_lsn", fromPosition, DbType.Binary, size: 10);
			parameters.Add("to_lsn", toPosition, DbType.Binary, size: 10);

			var command = new CommandDefinition(
				commandText,
				parameters: parameters,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken);

			var exists = await _connection.Ready().ExecuteScalarAsync<int?>(command).ConfigureAwait(false);
			if (exists is > 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <inheritdoc />
	public Task<byte[]?> GetNextValidLsn(byte[] lastProcessedLsn, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                 SELECT TOP 1 tran_begin_lsn
		                                 FROM cdc.lsn_time_mapping
		                                 WHERE tran_begin_lsn > @lastProcessedLsn
		                                 AND tran_begin_time IS NOT NULL
		                                 ORDER BY tran_begin_lsn ASC;
		                           """;

		var parameters = new DynamicParameters();
		parameters.Add("lastProcessedLsn", lastProcessedLsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(CommandText, parameters: parameters, cancellationToken: cancellationToken);

		return _connection.Ready().QueryFirstOrDefaultAsync<byte[]>(command);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<CdcRow>> FetchChangesAsync(
		string captureInstance,
		int batchSize,
		byte[] lsn,
		byte[]? lastSequenceValue,
		CdcOperationCodes lastOperation,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(captureInstance);
		ArgumentNullException.ThrowIfNull(lsn);

		var commandText = $"""
		                   SELECT TOP (@batchSize)
		                      '{captureInstance}' AS TableName,
		                      sys.fn_cdc_map_lsn_to_time(__$start_lsn) AS CommitTime,
		                      __$start_lsn AS Position,
		                      __$seqval AS SequenceValue,
		                      __$operation AS OperationCode,
		                      *
		                   FROM cdc.fn_cdc_get_all_changes_{captureInstance}(@lsn, @lsn, N'all update old')
		                   WHERE
		                      __$start_lsn = @lsn
		                      AND
		                      (
		                         @lastSequenceValue IS NULL
		                         OR
		                         (
		                            (__$seqval > @lastSequenceValue)
		                            OR
		                            (@lastOperation = 3 AND __$seqval = @lastSequenceValue)
		                         )
		                      )
		                   ORDER BY
		                      __$start_lsn,
		                      __$seqval,
		                      __$operation
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("@batchSize", batchSize, DbType.Int32);
		parameters.Add("lsn", lsn, DbType.Binary, size: 10);
		parameters.Add("lastSequenceValue", lastSequenceValue, DbType.Binary);
		parameters.Add("@lastOperation", (int)lastOperation, DbType.Int32);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);
		var reader = (DbDataReader)await _connection.Ready().ExecuteReaderAsync(command).ConfigureAwait(false);
		var resultList = new List<CdcRow>();

		await using (reader.ConfigureAwait(false))
		{
			var dataTypes = new Dictionary<string, Type?>();

			for (var i = 0; i < reader.FieldCount; i++)
			{
				var columnName = reader.GetName(i);
				var fieldType = reader.GetFieldType(i);
				dataTypes.Add(columnName, fieldType);
			}

			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				var changes = new Dictionary<string, object>();
				foreach (var columnName in dataTypes.Keys.Where(
							 (string columnName) => !(columnName.StartsWith("__$", StringComparison.InvariantCultureIgnoreCase)
													  || columnName == "TableName" || columnName == "CommitTime"
													  || columnName == "OperationCode" || columnName == "SequenceValue")))
				{
					changes[columnName] = reader[columnName];
				}

				var cdcRow = new CdcRow
				{
					TableName = (string)reader["TableName"],
					OperationCode = GetOperationCode(Convert.ToInt32(reader["OperationCode"], CultureInfo.CurrentCulture)),
					Lsn = (byte[])reader["Position"],
					SeqVal = (byte[])reader["SequenceValue"],
					CommitTime = (DateTime)reader["CommitTime"],
					Changes = changes,
					DataTypes = dataTypes.Where((KeyValuePair<string, Type> ct) => changes.ContainsKey(ct.Key)).ToDictionary(
									 (KeyValuePair<string, Type> kvp) => kvp.Key,
									 (KeyValuePair<string, Type> kvp) => kvp.Value)
				};

				resultList.Add(cdcRow);
			}
		}

		return resultList;
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	protected virtual async ValueTask DisposeAsyncCore()
	{
		await CastAndDispose(_connection).ConfigureAwait(false);

		return;

		static async ValueTask CastAndDispose(IDisposable resource)
		{
			switch (resource)
			{
				case IAsyncDisposable resourceAsyncDisposable:
					await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
					break;

				default:
					resource.Dispose();
					break;
			}
		}
	}

	private static CdcOperationCodes GetOperationCode(int operationCode) =>
		operationCode switch
		{
			1 => CdcOperationCodes.Delete,
			2 => CdcOperationCodes.Insert,
			3 => CdcOperationCodes.UpdateBefore,
			4 => CdcOperationCodes.UpdateAfter,
			_ => CdcOperationCodes.Unknown
		};
}
