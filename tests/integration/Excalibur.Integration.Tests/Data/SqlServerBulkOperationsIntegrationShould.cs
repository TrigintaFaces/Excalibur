// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Integration tests for SQL Server bulk/batch operations against real SQL Server using TestContainers.
/// Tests ExecuteBatchAsync and ExecuteBulkAsync through the public SqlServerPersistenceProvider API.
/// </summary>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerBulkOperationsIntegrationShould : IAsyncLifetime
{
	private readonly SqlServerContainerFixture _fixture;
	private string _tableName = null!;

	public SqlServerBulkOperationsIntegrationShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public async Task InitializeAsync()
	{
		_tableName = $"BulkTest_{Guid.NewGuid():N}"[..30];

		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		await conn.ExecuteAsync($@"
			CREATE TABLE [{_tableName}] (
				Id INT PRIMARY KEY,
				Name NVARCHAR(200) NOT NULL,
				Value DECIMAL(18,2) NOT NULL,
				CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
			)");
	}

	public async Task DisposeAsync()
	{
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		await conn.ExecuteAsync($"DROP TABLE IF EXISTS [{_tableName}]");
	}

	[Fact]
	public async Task ExecuteBatchOfInsertRequests()
	{
		// Arrange
		using var provider = CreateProvider();
		var requests = new List<IDataRequest<IDbConnection, object>>();
		for (var i = 1; i <= 5; i++)
		{
			requests.Add(new InsertRequest(_tableName, i, $"Item-{i}", i * 10.5m, CancellationToken.None));
		}

		// Act
		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert
		results.ShouldNotBeNull();
		results.Count().ShouldBe(5);

		// Verify data was inserted
		var count = await CountRowsAsync();
		count.ShouldBe(5);
	}

	[Fact]
	public async Task ExecuteBatchOfMixedInsertAndUpdateRequests()
	{
		// Arrange — seed initial data
		await SeedRowsAsync(3);
		using var provider = CreateProvider();

		var requests = new List<IDataRequest<IDbConnection, object>>
		{
			new InsertRequest(_tableName, 4, "New-Item", 100m, CancellationToken.None),
			new UpdateRequest(_tableName, 1, "Updated-Item-1", CancellationToken.None),
			new InsertRequest(_tableName, 5, "Another-New", 200m, CancellationToken.None),
		};

		// Act
		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert
		results.ShouldNotBeNull();
		results.Count().ShouldBe(3);

		// Verify total count
		var count = await CountRowsAsync();
		count.ShouldBe(5);

		// Verify update was applied
		var updatedName = await GetNameByIdAsync(1);
		updatedName.ShouldBe("Updated-Item-1");
	}

	[Fact]
	public async Task ExecuteBulkInsertRequest()
	{
		// Arrange
		using var provider = CreateProvider();
		var request = new BulkInsertRequest(_tableName, 50, CancellationToken.None);

		// Act
		var result = await provider.ExecuteBulkAsync<int>(request, CancellationToken.None);

		// Assert
		result.ShouldBe(50);

		// Verify all rows were inserted
		var count = await CountRowsAsync();
		count.ShouldBe(50);
	}

	[Fact]
	public async Task ExecuteBatchWithSingleRequest()
	{
		// Arrange
		using var provider = CreateProvider();
		var requests = new List<IDataRequest<IDbConnection, object>>
		{
			new InsertRequest(_tableName, 1, "Single-Item", 99.99m, CancellationToken.None),
		};

		// Act
		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert
		results.ShouldNotBeNull();
		results.Count().ShouldBe(1);
		var count = await CountRowsAsync();
		count.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteBatchWithEmptyRequestList()
	{
		// Arrange
		using var provider = CreateProvider();
		var requests = new List<IDataRequest<IDbConnection, object>>();

		// Act
		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert
		results.ShouldNotBeNull();
		results.Count().ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteBulkInsertWithLargerDataSet()
	{
		// Arrange — 500 rows exercises bulk insert path
		using var provider = CreateProvider();
		var request = new BulkInsertRequest(_tableName, 500, CancellationToken.None);

		// Act
		var result = await provider.ExecuteBulkAsync<int>(request, CancellationToken.None);

		// Assert
		result.ShouldBe(500);
		var count = await CountRowsAsync();
		count.ShouldBe(500);
	}

	[Fact]
	public async Task ExecuteBatchDeleteRequests()
	{
		// Arrange — seed rows then delete some
		await SeedRowsAsync(10);
		using var provider = CreateProvider();
		var requests = new List<IDataRequest<IDbConnection, object>>
		{
			new DeleteRequest(_tableName, 3, CancellationToken.None),
			new DeleteRequest(_tableName, 5, CancellationToken.None),
			new DeleteRequest(_tableName, 7, CancellationToken.None),
		};

		// Act
		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert
		results.ShouldNotBeNull();
		var count = await CountRowsAsync();
		count.ShouldBe(7);
	}

	[Fact]
	public async Task ExecuteBatchPreservesDataIntegrity()
	{
		// Arrange — insert rows and verify exact values
		using var provider = CreateProvider();
		var requests = new List<IDataRequest<IDbConnection, object>>
		{
			new InsertRequest(_tableName, 100, "Precision-Test", 123.45m, CancellationToken.None),
			new InsertRequest(_tableName, 200, "Unicode-Test-\u00E9\u00E8\u00EA", 0.01m, CancellationToken.None),
		};

		// Act
		await provider.ExecuteBatchAsync(requests, CancellationToken.None);

		// Assert — verify exact data
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();

		var row1 = await conn.QuerySingleAsync<dynamic>(
			$"SELECT Name, Value FROM [{_tableName}] WHERE Id = 100");
		((string)row1.Name).ShouldBe("Precision-Test");
		((decimal)row1.Value).ShouldBe(123.45m);

		var row2 = await conn.QuerySingleAsync<dynamic>(
			$"SELECT Name FROM [{_tableName}] WHERE Id = 200");
		((string)row2.Name).ShouldBe("Unicode-Test-\u00E9\u00E8\u00EA");
	}

	[Fact]
	public async Task ThrowWhenExecuteBatchWithNullRequests()
	{
		// Arrange
		using var provider = CreateProvider();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			provider.ExecuteBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenExecuteBulkWithNullRequest()
	{
		// Arrange
		using var provider = CreateProvider();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			provider.ExecuteBulkAsync<int>(null!, CancellationToken.None));
	}

	#region Helper Methods

	private SqlServerPersistenceProvider CreateProvider()
	{
		var options = Options.Create(new SqlServerProviderOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Name = "bulk-test",
			CommandTimeout = 60,
			ConnectTimeout = 15,
			MaxPoolSize = 10,
			MinPoolSize = 1,
			EnablePooling = true,
			TrustServerCertificate = true,
			RetryCount = 3,
		});

		return new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);
	}

	private async Task<int> CountRowsAsync()
	{
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		return await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM [{_tableName}]");
	}

	private async Task<string> GetNameByIdAsync(int id)
	{
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		return await conn.QuerySingleAsync<string>($"SELECT Name FROM [{_tableName}] WHERE Id = @Id", new { Id = id });
	}

	private async Task SeedRowsAsync(int count)
	{
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		for (var i = 1; i <= count; i++)
		{
			await conn.ExecuteAsync(
				$"INSERT INTO [{_tableName}] (Id, Name, Value) VALUES (@Id, @Name, @Value)",
				new { Id = i, Name = $"Seed-{i}", Value = i * 1.0m });
		}
	}

	#endregion

	#region Test Data Request Types

	/// <summary>
	/// Insert request that inserts a single row.
	/// </summary>
	private sealed class InsertRequest : IDataRequest<IDbConnection, object>
	{
		public InsertRequest(string tableName, int id, string name, decimal value, CancellationToken cancellationToken)
		{
			var sql = $"INSERT INTO [{tableName}] (Id, Name, Value) VALUES (@Id, @Name, @Value); SELECT @Id";
			Parameters = new DynamicParameters();
			Parameters.Add("Id", id);
			Parameters.Add("Name", name);
			Parameters.Add("Value", value);
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return (object)result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "Insert";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// Update request that updates a single row by Id.
	/// </summary>
	private sealed class UpdateRequest : IDataRequest<IDbConnection, object>
	{
		public UpdateRequest(string tableName, int id, string newName, CancellationToken cancellationToken)
		{
			var sql = $"UPDATE [{tableName}] SET Name = @Name WHERE Id = @Id; SELECT @@ROWCOUNT";
			Parameters = new DynamicParameters();
			Parameters.Add("Id", id);
			Parameters.Add("Name", newName);
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return (object)result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "Update";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// Delete request that deletes a single row by Id.
	/// </summary>
	private sealed class DeleteRequest : IDataRequest<IDbConnection, object>
	{
		public DeleteRequest(string tableName, int id, CancellationToken cancellationToken)
		{
			var sql = $"DELETE FROM [{tableName}] WHERE Id = @Id; SELECT @@ROWCOUNT";
			Parameters = new DynamicParameters();
			Parameters.Add("Id", id);
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return (object)result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "Delete";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// Bulk insert request that inserts multiple rows using a loop with Dapper.
	/// </summary>
	private sealed class BulkInsertRequest : IDataRequest<IDbConnection, int>
	{
		public BulkInsertRequest(string tableName, int rowCount, CancellationToken cancellationToken)
		{
			var sql = $"INSERT INTO [{tableName}] (Id, Name, Value) VALUES (@Id, @Name, @Value)";
			Parameters = new DynamicParameters();
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var rows = Enumerable.Range(1, rowCount).Select(i => new
				{
					Id = i,
					Name = $"Bulk-{i}",
					Value = i * 0.5m,
				});

				var inserted = await conn.ExecuteAsync(sql, rows);
				return inserted;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<int>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "BulkInsert";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	#endregion
}
