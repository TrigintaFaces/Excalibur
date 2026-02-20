// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Data.Common;
using System.Globalization;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Validation;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides methods for interacting with SQL Server Change Data Capture (CDC) tables.
/// </summary>
public class CdcRepository : ICdcRepository, ICdcRepositoryLsnMapping
{
	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRepository" /> class using a provided database connection.
	/// </summary>
	/// <param name="connection"> The database connection to use for CDC queries. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is null. </exception>
	public CdcRepository(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);

		_connection = connection;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcRepository" /> class using a database wrapper.
	/// </summary>
	/// <param name="db"> The database wrapper providing the connection. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is null. </exception>
	public CdcRepository(IDb db)
	{
		ArgumentNullException.ThrowIfNull(db);

		_connection = db.Connection;
	}

	/// <inheritdoc />
	public async Task<byte[]> GetNextLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_increment_lsn(@LastProcessedLsn);";

		var parameters = new DynamicParameters();
		parameters.Add("LastProcessedLsn", lastProcessedLsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await _connection.Ready().QuerySingleAsync<byte[]>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<byte[]?> GetNextLsnAsync(string captureInstance, byte[] lastProcessedLsn, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(captureInstance);
		ArgumentNullException.ThrowIfNull(lastProcessedLsn);
		SqlIdentifierValidator.ThrowIfInvalid(captureInstance, nameof(captureInstance));

		var commandText = $"""
		                   SELECT Top 1 __$start_lsn AS Next_LSN
		                   FROM cdc.{captureInstance}_CT
		                   WHERE __$start_lsn > @LastProcessedLsn
		                   ORDER BY __$start_lsn;
		                   """;

		var parameters = new DynamicParameters();
		parameters.Add("LastProcessedLsn", lastProcessedLsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			commandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await _connection.Ready().QuerySingleOrDefaultAsync<byte[]?>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<DateTime?> GetLsnToTimeAsync(byte[] lsn, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT CAST(sys.fn_cdc_map_lsn_to_time(@Lsn) AS DATETIME2(3)) AS LSN_TIME;";

		var parameters = new DynamicParameters();
		parameters.Add("Lsn", lsn, DbType.Binary, size: 10);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await _connection.Ready().QuerySingleOrDefaultAsync<DateTime?>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<byte[]?> GetTimeToLsnAsync(DateTime lsnDate, string relationalOperator, CancellationToken cancellationToken)
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

		return await _connection.Ready().QuerySingleAsync<byte[]?>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<byte[]> GetMinPositionAsync(string captureInstance, CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_get_min_lsn(@CaptureInstance);";

		var parameters = new DynamicParameters();
		parameters.Add("CaptureInstance", captureInstance);

		var command = new CommandDefinition(
			CommandText,
			parameters: parameters,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await _connection.Ready().QuerySingleAsync<byte[]>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<byte[]> GetMaxPositionAsync(CancellationToken cancellationToken)
	{
		const string CommandText = "SELECT sys.fn_cdc_get_max_lsn();";

		var command = new CommandDefinition(
			CommandText,
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		return await _connection.Ready().QuerySingleAsync<byte[]>(command).ConfigureAwait(false);
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
			SqlIdentifierValidator.ThrowIfInvalid(captureInstance, nameof(captureInstances));

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
	public async Task<byte[]?> GetNextValidLsnAsync(byte[] lastProcessedLsn, CancellationToken cancellationToken)
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

		return await _connection.Ready().QueryFirstOrDefaultAsync<byte[]>(command).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "CDC query method requires many Dapper/ADO.NET/CDC types; coupling is inherent to the domain.")]
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
		SqlIdentifierValidator.ThrowIfInvalid(captureInstance, nameof(captureInstance));

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
		                   		(@lastOperation = 3 AND __$seqval = @lastSequenceValue AND __$operation = 4)
		                   		OR
		                   		(__$seqval > @lastSequenceValue)
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
			var dataTypes = new Dictionary<string, Type?>(StringComparer.Ordinal);

			for (var i = 0; i < reader.FieldCount; i++)
			{
				var columnName = reader.GetName(i);
				var fieldType = reader.GetFieldType(i);
				dataTypes.Add(columnName, fieldType);
			}

			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				var changes = new Dictionary<string, object>(StringComparer.Ordinal);
				foreach (var columnName in dataTypes.Keys.Where(columnName =>
							 !(columnName.StartsWith("__$", StringComparison.InvariantCultureIgnoreCase) ||
							   string.Equals(columnName, "TableName", StringComparison.OrdinalIgnoreCase) ||
							   string.Equals(columnName, "CommitTime", StringComparison.OrdinalIgnoreCase) ||
							   string.Equals(columnName, "OperationCode", StringComparison.OrdinalIgnoreCase) ||
							   string.Equals(columnName, "SequenceValue", StringComparison.OrdinalIgnoreCase))))
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
					DataTypes = dataTypes
						.Where(ct => changes.ContainsKey(ct.Key) && ct.Value != null)
						.ToDictionary(
							kvp => kvp.Key,
							kvp => kvp.Value!,
							StringComparer.Ordinal),
				};

				resultList.Add(cdcRow);
			}
		}

		return resultList;
	}

	/// <summary>
	/// Asynchronously releases the unmanaged resources used by the <see cref="CdcRepository" />.
	/// </summary>
	/// <returns> A value task that represents the asynchronous dispose operation. </returns>
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="CdcRepository" />.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Core asynchronous disposal implementation.
	/// </summary>
	/// <returns> A value task that represents the asynchronous disposal operation. </returns>
	// DisposeCoreAsync is the standard .NET IAsyncDisposable pattern name per framework design guidelines
	// See: https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync#implement-the-async-dispose-pattern
	// R0.8: Asynchronous method name should end with 'Async'
#pragma warning disable RCS1046

	protected virtual async ValueTask DisposeAsyncCore() => await CdcDisposalHelper.SafeDisposeAsync(_connection).ConfigureAwait(false);

#pragma warning restore RCS1046 // Asynchronous method name should end with 'Async'

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

	private static CdcOperationCodes GetOperationCode(int operationCode) =>
		operationCode switch
		{
			1 => CdcOperationCodes.Delete,
			2 => CdcOperationCodes.Insert,
			3 => CdcOperationCodes.UpdateBefore,
			4 => CdcOperationCodes.UpdateAfter,
			_ => CdcOperationCodes.Unknown,
		};
}
