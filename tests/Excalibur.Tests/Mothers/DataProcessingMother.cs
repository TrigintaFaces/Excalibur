using System.Data;

using Dapper;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;

namespace Excalibur.Tests.Mothers;

public static class DataProcessingMother
{
	public static async Task EnsureDatabaseInitializedAsync(IDbConnection connection, DatabaseEngine engine)
	{
		ArgumentNullException.ThrowIfNull(connection);

		try
		{
			var commands = engine switch
			{
				DatabaseEngine.SqlServer => new[]
				{
					"""
					IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DataProcessor')
					EXEC('CREATE SCHEMA DataProcessor');
					""",
					"""
					IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DataTaskRequests' AND xtype='U')
					CREATE TABLE DataProcessor.DataTaskRequests (
						DataTaskId UNIQUEIDENTIFIER PRIMARY KEY,
						CreatedAt DATETIME NOT NULL,
						RecordType NVARCHAR(100) NOT NULL,
						Attempts INT NOT NULL,
						MaxAttempts INT NOT NULL,
						CompletedCount INT NOT NULL DEFAULT 0
					);
					""",
					"DELETE FROM DataProcessor.DataTaskRequests"
				},
				DatabaseEngine.PostgreSql => new[]
				{
					"CREATE SCHEMA IF NOT EXISTS DataProcessor;", """
					                                              CREATE TABLE IF NOT EXISTS DataProcessor.DataTaskRequests (
					                                              	DataTaskId UUID PRIMARY KEY,
					                                              	CreatedAt TIMESTAMP NOT NULL,
					                                              	RecordType VARCHAR(100) NOT NULL,
					                                              	Attempts INT NOT NULL,
					                                              	MaxAttempts INT NOT NULL,
					                                              	CompletedCount INT NOT NULL DEFAULT 0
					                                              );
					                                              """
				},
				_ => throw new NotSupportedException($"Unsupported database engine: {engine}")
			};

			foreach (var command in commands)
			{
				_ = await connection.ExecuteAsync(command).ConfigureAwait(false);
			}
		}
		finally
		{
			connection.Close();
		}
	}

	public static async Task SeedDataTaskAsync(IDbConnection connection, DatabaseEngine engine, string recordType)
	{
		var command = engine switch
		{
			DatabaseEngine.SqlServer or DatabaseEngine.PostgreSql => """
			                                                         INSERT INTO DataProcessor.DataTaskRequests (
			                                                         	DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts, CompletedCount)
			                                                         VALUES (@DataTaskId, @CreatedAt, @RecordType, @Attempts, @MaxAttempts, @CompletedCount);
			                                                         """,
			_ => throw new NotSupportedException($"Unsupported database engine: {engine}")
		};

		var parameters = new
		{
			DataTaskId = Guid.NewGuid(),
			CreatedAt = DateTime.UtcNow,
			RecordType = recordType,
			Attempts = 0,
			MaxAttempts = 3,
			CompletedCount = 0
		};

		_ = await connection.ExecuteAsync(command, parameters).ConfigureAwait(false);
	}

	public static async Task InsertDataTasks(IDataOrchestrationManager manager, string recordType, int count)
	{
		ArgumentNullException.ThrowIfNull(manager);

		for (var i = 0; i < count; i++)
		{
			_ = await manager.AddDataTaskForRecordType(recordType).ConfigureAwait(true);
		}
	}

	public static async Task<int> GetTaskCount(IDbConnection connection) =>
		await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM DataProcessor.DataTaskRequests").ConfigureAwait(true);

	public static async Task<int> GetAttempts(Guid dataTaskId, IDbConnection conn) =>
		await conn.ExecuteScalarAsync<int>("SELECT Attempts FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @Id",
			new { Id = dataTaskId }).ConfigureAwait(true);

	public static async Task<long> GetCompletedCount(Guid dataTaskId, IDbConnection conn) =>
		await conn.ExecuteScalarAsync<int>("SELECT CompletedCount FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @Id",
			new { Id = dataTaskId }).ConfigureAwait(true);

	public static DataTaskRequest CreateDataTaskRequest(string recordType, int attempts = 0, int maxAttempts = 3, int completedCount = 0) =>
		new()
		{
			DataTaskId = Guid.NewGuid(),
			CreatedAt = DateTime.UtcNow,
			RecordType = recordType,
			Attempts = attempts,
			MaxAttempts = maxAttempts,
			CompletedCount = completedCount
		};

	public static IEnumerable<DataTaskRequest> CreateMultipleDataTaskRequests(int count, string recordType) =>
		Enumerable.Range(1, count)
			.Select(_ => CreateDataTaskRequest(recordType));
}
