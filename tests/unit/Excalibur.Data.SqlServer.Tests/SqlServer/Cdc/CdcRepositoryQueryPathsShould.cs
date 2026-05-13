// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Query-path behavior tests for <see cref="CdcRepository"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcRepositoryQueryPathsShould : UnitTestBase
{
	[Fact]
	public async Task GetNextLsnAsync_WithCaptureInstance_NormalizesDottedNameInQuery()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		var expected = new byte[] { 0x0A, 0x0B };
		connection.EnqueueReaderResult(CreateSingleValueTable("Next_LSN", typeof(byte[]), expected));
		await using var repository = new CdcRepository(connection);

		// Act
		var result = await repository.GetNextLsnAsync("dbo.Orders", [0x01], CancellationToken.None);

		// Assert
		result.SequenceEqual(expected).ShouldBeTrue();
		connection.Commands.Count.ShouldBe(1);
		connection.Commands[0].CommandText.ShouldContain("FROM cdc.dbo_Orders_CT");
		connection.Commands[0].ParameterValues["LastProcessedLsn"].ShouldBeOfType<byte[]>();
	}

	[Fact]
	public async Task GetNextLsnAsync_ReturnsNull_WhenNoRowsAreAvailable()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueReaderResult(CreateSingleValueTable("Next_LSN", typeof(byte[]), DBNull.Value));
		await using var repository = new CdcRepository(connection);

		// Act
		var result = await repository.GetNextLsnAsync("dbo.Orders", [0x01], CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		connection.Commands.Count.ShouldBe(1);
	}

	[Fact]
	public async Task GetMinPositionAsync_WithCaptureInstance_NormalizesParameterValue()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		var expected = new byte[] { 0x11, 0x22 };
		connection.EnqueueReaderResult(CreateSingleValueTable("Value", typeof(byte[]), expected));
		await using var repository = new CdcRepository(connection);

		// Act
		var result = await repository.GetMinPositionAsync("sales.Invoices", CancellationToken.None);

		// Assert
		result.SequenceEqual(expected).ShouldBeTrue();
		connection.Commands.Count.ShouldBe(1);
		connection.Commands[0].CommandText.ShouldContain("fn_cdc_get_min_lsn");
		connection.Commands[0].ParameterValues["CaptureInstance"].ShouldBe("sales_Invoices");
	}

	[Fact]
	public async Task ChangesExistAsync_ReturnsTrue_WhenAnyCaptureInstanceHasChanges()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueScalarResult(null);
		connection.EnqueueScalarResult(1);
		await using var repository = new CdcRepository(connection);

		// Act
		var exists = await repository.ChangesExistAsync(
			[0x10],
			[0x20],
			["dbo.Orders", "sales.Invoices"],
			CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
		connection.Commands.Count.ShouldBe(2);
		connection.Commands[0].CommandText.ShouldContain("fn_cdc_get_all_changes_dbo_Orders");
		connection.Commands[1].CommandText.ShouldContain("fn_cdc_get_all_changes_sales_Invoices");
	}

	[Fact]
	public async Task ChangesExistAsync_StopsAfterFirstPositiveMatch()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueScalarResult(1);
		await using var repository = new CdcRepository(connection);

		// Act
		var exists = await repository.ChangesExistAsync(
			[0x10],
			[0x20],
			["dbo.Orders", "sales.Invoices"],
			CancellationToken.None);

		// Assert
		exists.ShouldBeTrue();
		connection.Commands.Count.ShouldBe(1);
		connection.Commands[0].CommandText.ShouldContain("fn_cdc_get_all_changes_dbo_Orders");
	}

	[Fact]
	public async Task ChangesExistAsync_ReturnsFalse_WhenNoCaptureInstanceHasChanges()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueScalarResult(0);
		connection.EnqueueScalarResult(null);
		await using var repository = new CdcRepository(connection);

		// Act
		var exists = await repository.ChangesExistAsync(
			[0x10],
			[0x20],
			["dbo.Orders", "sales.Invoices"],
			CancellationToken.None);

		// Assert
		exists.ShouldBeFalse();
		connection.Commands.Count.ShouldBe(2);
	}

	[Fact]
	public async Task FetchChangesAsync_MapsBusinessColumns_AndFiltersSystemColumns()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		var commitTime = DateTime.UtcNow;
		connection.EnqueueReaderResult(CreateChangesTable(
			operationCode: 2,
			tableName: "dbo.Orders",
			commitTime: commitTime,
			position: [0x01, 0x02],
			sequenceValue: [0x0A],
			orderId: 42,
			status: DBNull.Value));
		await using var repository = new CdcRepository(connection);

		// Act
		var rows = (await repository.FetchChangesAsync(
				"dbo.Orders",
				batchSize: 100,
				fromLsn: [0x01, 0x02],
				toLsn: [0xFF, 0xFF],
				lastSequenceValue: null,
				lastOperation: CdcOperationCodes.Unknown,
				cancellationToken: CancellationToken.None))
			.ToList();

		// Assert
		rows.Count.ShouldBe(1);
		rows[0].TableName.ShouldBe("dbo.Orders");
		rows[0].OperationCode.ShouldBe(CdcOperationCodes.Insert);
		rows[0].CommitTime.ShouldBe(commitTime);
		rows[0].Changes.Keys.ShouldContain("OrderId");
		rows[0].Changes.Keys.ShouldContain("Status");
		rows[0].Changes.Keys.ShouldNotContain("__$start_lsn");
		rows[0].Changes.Keys.ShouldNotContain("TableName");
		rows[0].Changes["OrderId"].ShouldBe(42);
		rows[0].Changes["Status"].ShouldBe(DBNull.Value);
		rows[0].DataTypes["OrderId"].ShouldBe(typeof(int));
		rows[0].DataTypes["Status"].ShouldBe(typeof(string));

		connection.Commands.Count.ShouldBe(1);
		connection.Commands[0].CommandText.ShouldContain("fn_cdc_get_all_changes_dbo_Orders");
	}

	[Fact]
	public async Task FetchChangesAsync_MapsUnknownOperationCodeToUnknown()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueReaderResult(CreateChangesTable(
			operationCode: 999,
			tableName: "dbo.Orders",
			commitTime: DateTime.UtcNow,
			position: [0x03],
			sequenceValue: [0x04],
			orderId: 7,
			status: "UnknownState"));
		await using var repository = new CdcRepository(connection);

		// Act
		var row = (await repository.FetchChangesAsync(
				"dbo.Orders",
				batchSize: 10,
				fromLsn: [0x03],
				toLsn: [0xFF],
				lastSequenceValue: null,
				lastOperation: CdcOperationCodes.Unknown,
				cancellationToken: CancellationToken.None))
			.Single();

		// Assert
		row.OperationCode.ShouldBe(CdcOperationCodes.Unknown);
	}

	[Fact]
	public async Task FetchChangesAsync_PopulatesSequenceResumeParameters()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		connection.EnqueueReaderResult(CreateChangesTable(
			operationCode: 4,
			tableName: "dbo.Orders",
			commitTime: DateTime.UtcNow,
			position: [0x03],
			sequenceValue: [0x07],
			orderId: 77,
			status: "ok"));
		await using var repository = new CdcRepository(connection);

		// Act
		_ = await repository.FetchChangesAsync(
			"dbo.Orders",
			batchSize: 25,
			fromLsn: [0x03],
			toLsn: [0xFF],
			lastSequenceValue: [0x07],
			lastOperation: CdcOperationCodes.UpdateBefore,
			cancellationToken: CancellationToken.None);

		// Assert
		connection.Commands.Count.ShouldBe(1);
		connection.Commands[0].ParameterValues["batchSize"].ShouldBe(25);
		connection.Commands[0].ParameterValues["fromLsn"].ShouldBeOfType<byte[]>();
		connection.Commands[0].ParameterValues["toLsn"].ShouldBeOfType<byte[]>();
		connection.Commands[0].ParameterValues["lastSequenceValue"].ShouldBeOfType<byte[]>();
		connection.Commands[0].ParameterValues["lastOperation"].ShouldBe((int)CdcOperationCodes.UpdateBefore);
	}

	[Fact]
	public async Task FetchChangesAsync_UsesRangeQuery_WithFromLsnAndToLsn()
	{
		// Arrange — verify the SQL query uses @fromLsn and @toLsn range parameters
		// instead of the old single-LSN point query pattern (Sprint 824, bd-ko74ik).
		var connection = new RecordingDbConnection();
		connection.EnqueueReaderResult(CreateChangesTable(
			operationCode: 2,
			tableName: "dbo.Orders",
			commitTime: DateTime.UtcNow,
			position: [0x01],
			sequenceValue: [0x02],
			orderId: 1,
			status: "ok"));
		await using var repository = new CdcRepository(connection);

		// Act
		_ = await repository.FetchChangesAsync(
			"dbo.Orders",
			batchSize: 50,
			fromLsn: [0x01],
			toLsn: [0x99],
			lastSequenceValue: null,
			lastOperation: CdcOperationCodes.Unknown,
			cancellationToken: CancellationToken.None);

		// Assert — range query passes both LSN boundaries
		connection.Commands.Count.ShouldBe(1);
		var sql = connection.Commands[0].CommandText;
		sql.ShouldContain("@fromLsn");
		sql.ShouldContain("@toLsn");
		sql.ShouldNotContain("@lsn");
		connection.Commands[0].ParameterValues["fromLsn"].ShouldBeOfType<byte[]>();
		connection.Commands[0].ParameterValues["toLsn"].ShouldBeOfType<byte[]>();
	}

	[Fact]
	public async Task FetchChangesAsync_ThrowsArgumentNullException_WhenToLsnIsNull()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		await using var repository = new CdcRepository(connection);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.FetchChangesAsync(
				"dbo.Orders",
				batchSize: 10,
				fromLsn: [0x01],
				toLsn: null!,
				lastSequenceValue: null,
				lastOperation: CdcOperationCodes.Unknown,
				cancellationToken: CancellationToken.None));
	}

	[Fact]
	public async Task FetchChangesAsync_ThrowsArgumentNullException_WhenFromLsnIsNull()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		await using var repository = new CdcRepository(connection);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await repository.FetchChangesAsync(
				"dbo.Orders",
				batchSize: 10,
				fromLsn: null!,
				toLsn: [0xFF],
				lastSequenceValue: null,
				lastOperation: CdcOperationCodes.Unknown,
				cancellationToken: CancellationToken.None));
	}

	[Fact]
	public async Task FetchChangesAsync_SharesDataTypesDictionary_AcrossRowsInBatch()
	{
		// Arrange — verify the pre-computed DataTypes dictionary is shared (Sprint 824, bd-znaw4w).
		var connection = new RecordingDbConnection();
		var table = new DataTable();
		_ = table.Columns.Add("TableName", typeof(string));
		_ = table.Columns.Add("CommitTime", typeof(DateTime));
		_ = table.Columns.Add("Position", typeof(byte[]));
		_ = table.Columns.Add("SequenceValue", typeof(byte[]));
		_ = table.Columns.Add("OperationCode", typeof(int));
		_ = table.Columns.Add("__$start_lsn", typeof(byte[]));
		_ = table.Columns.Add("__$seqval", typeof(byte[]));
		_ = table.Columns.Add("__$operation", typeof(int));
		_ = table.Columns.Add("OrderId", typeof(int));
		var now = DateTime.UtcNow;
		_ = table.Rows.Add("dbo.Orders", now, new byte[] { 0x01 }, new byte[] { 0x01 }, 2, new byte[] { 0x01 }, new byte[] { 0x01 }, 2, 1);
		_ = table.Rows.Add("dbo.Orders", now, new byte[] { 0x01 }, new byte[] { 0x02 }, 2, new byte[] { 0x01 }, new byte[] { 0x02 }, 2, 2);
		connection.EnqueueReaderResult(table);
		await using var repository = new CdcRepository(connection);

		// Act
		var rows = (await repository.FetchChangesAsync(
				"dbo.Orders",
				batchSize: 50,
				fromLsn: [0x01],
				toLsn: [0xFF],
				lastSequenceValue: null,
				lastOperation: CdcOperationCodes.Unknown,
				cancellationToken: CancellationToken.None))
			.ToList();

		// Assert — both rows share the same DataTypes dictionary instance
		rows.Count.ShouldBe(2);
		ReferenceEquals(rows[0].DataTypes, rows[1].DataTypes).ShouldBeTrue(
			"DataTypes dictionary should be shared across rows in the same batch for allocation reduction.");
	}

	[Fact]
	public async Task DisposeAsync_DisposesUnderlyingConnection()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		var repository = new CdcRepository(connection);

		// Act
		await repository.DisposeAsync();

		// Assert
		connection.IsAsyncDisposed.ShouldBeTrue();
	}

	[Fact]
	public void Dispose_DisposesUnderlyingConnection()
	{
		// Arrange
		var connection = new RecordingDbConnection();
		var repository = new CdcRepository(connection);

		// Act
		repository.Dispose();

		// Assert
		connection.IsDisposed.ShouldBeTrue();
	}

	private static DataTable CreateSingleValueTable(string columnName, Type columnType, object value)
	{
		var table = new DataTable();
		_ = table.Columns.Add(columnName, columnType);
		_ = table.Rows.Add(value);
		return table;
	}

	private static DataTable CreateChangesTable(
		int operationCode,
		string tableName,
		DateTime commitTime,
		byte[] position,
		byte[] sequenceValue,
		int orderId,
		object status)
	{
		var table = new DataTable();
		_ = table.Columns.Add("TableName", typeof(string));
		_ = table.Columns.Add("CommitTime", typeof(DateTime));
		_ = table.Columns.Add("Position", typeof(byte[]));
		_ = table.Columns.Add("SequenceValue", typeof(byte[]));
		_ = table.Columns.Add("OperationCode", typeof(int));
		_ = table.Columns.Add("__$start_lsn", typeof(byte[]));
		_ = table.Columns.Add("__$seqval", typeof(byte[]));
		_ = table.Columns.Add("__$operation", typeof(int));
		_ = table.Columns.Add("OrderId", typeof(int));
		_ = table.Columns.Add("Status", typeof(string));

		_ = table.Rows.Add(
			tableName,
			commitTime,
			position,
			sequenceValue,
			operationCode,
			position,
			sequenceValue,
			operationCode,
			orderId,
			status);

		return table;
	}

	private sealed class RecordingDbConnection : DbConnection
	{
		private readonly Queue<DataTable> _readerResults = [];
		private readonly Queue<object?> _scalarResults = [];
		private ConnectionState _state = ConnectionState.Open;

		public List<ExecutedCommand> Commands { get; } = [];

		public bool IsDisposed { get; private set; }

		public bool IsAsyncDisposed { get; private set; }

		private string _connectionString = "Server=localhost;Database=TestDb";

		[AllowNull]
		public override string ConnectionString
		{
			get => _connectionString;
			set => _connectionString = value ?? string.Empty;
		}

		public override string Database => "TestDb";

		public override string DataSource => "TestSource";

		public override string ServerVersion => "1.0";

		public override ConnectionState State => _state;

		public void EnqueueReaderResult(DataTable table) => _readerResults.Enqueue(table);

		public void EnqueueScalarResult(object? value) => _scalarResults.Enqueue(value);

		public override void ChangeDatabase(string databaseName) => _ = databaseName;

		public override void Close() => _state = ConnectionState.Closed;

		public override void Open() => _state = ConnectionState.Open;

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
			throw new NotSupportedException("Transactions are not required for this recording test connection.");

		protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);

		internal DbDataReader ExecuteReader(RecordingDbCommand command)
		{
			Record(command);
			if (_readerResults.Count == 0)
			{
				throw new InvalidOperationException("No reader result enqueued for command execution.");
			}

			return _readerResults.Dequeue().CreateDataReader();
		}

		internal object? ExecuteScalar(RecordingDbCommand command)
		{
			Record(command);
			if (_scalarResults.Count == 0)
			{
				throw new InvalidOperationException("No scalar result enqueued for command execution.");
			}

			return _scalarResults.Dequeue();
		}

		private void Record(RecordingDbCommand command)
		{
			var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			foreach (DbParameter parameter in command.Parameters)
			{
				var parameterName = parameter.ParameterName;
				if (parameterName.StartsWith('@'))
				{
					parameterName = parameterName[1..];
				}

				parameters[parameterName] = parameter.Value;
			}

			Commands.Add(new ExecutedCommand(command.CommandText ?? string.Empty, parameters));
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				IsDisposed = true;
				_state = ConnectionState.Closed;
			}

			base.Dispose(disposing);
		}

		public override async ValueTask DisposeAsync()
		{
			IsAsyncDisposed = true;
			IsDisposed = true;
			_state = ConnectionState.Closed;
			await base.DisposeAsync();
		}
	}

	private sealed class RecordingDbCommand(RecordingDbConnection connection) : DbCommand
	{
		private readonly SqlParameterCollection _parameters = new SqlCommand().Parameters;
		private string _commandText = string.Empty;

		[AllowNull]
		public override string CommandText
		{
			get => _commandText;
			set => _commandText = value ?? string.Empty;
		}

		public override int CommandTimeout { get; set; }

		public override CommandType CommandType { get; set; } = CommandType.Text;

		public override bool DesignTimeVisible { get; set; }

		public override UpdateRowSource UpdatedRowSource { get; set; }

		protected override DbConnection? DbConnection { get; set; } = connection;

		protected override DbParameterCollection DbParameterCollection => _parameters;

		protected override DbTransaction? DbTransaction { get; set; }

		public override void Cancel()
		{
		}

		public override int ExecuteNonQuery() => 0;

		public override object? ExecuteScalar() => connection.ExecuteScalar(this);

		public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ExecuteScalar());
		}

		public override void Prepare()
		{
		}

		protected override DbParameter CreateDbParameter() => new SqlParameter();

		protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => connection.ExecuteReader(this);

		protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(ExecuteDbDataReader(behavior));
		}
	}

	private sealed record ExecutedCommand(
		string CommandText,
		IReadOnlyDictionary<string, object?> ParameterValues);
}
