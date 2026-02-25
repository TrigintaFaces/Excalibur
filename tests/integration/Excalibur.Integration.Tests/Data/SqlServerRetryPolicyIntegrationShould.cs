// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Integration tests for SqlServerRetryPolicy against real SQL Server using TestContainers.
/// Validates transient error classification, retry behavior with real connections,
/// and successful execution through the retry policy.
/// </summary>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerRetryPolicyIntegrationShould
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerRetryPolicyIntegrationShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void InitializeWithCorrectRetryCount()
	{
		// Arrange
		var options = CreateProviderOptions(retryCount: 5);

		// Act
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Assert
		sut.MaxRetryAttempts.ShouldBe(5);
		sut.BaseRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ClassifyTimeoutExceptionAsTransient()
	{
		// Arrange
		var sut = CreateRetryPolicy();

		// Act
		var result = sut.ShouldRetry(new TimeoutException("Connection timed out"));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ClassifyInvalidOperationWithTimeoutAsTransient()
	{
		// Arrange
		var sut = CreateRetryPolicy();

		// Act
		var result = sut.ShouldRetry(new InvalidOperationException("Timeout expired before the connection was available."));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ClassifyNonTransientExceptionAsNotRetryable()
	{
		// Arrange
		var sut = CreateRetryPolicy();

		// Act & Assert
		sut.ShouldRetry(new InvalidOperationException("Some other error")).ShouldBeFalse();
		sut.ShouldRetry(new ArgumentException("Bad argument")).ShouldBeFalse();
		sut.ShouldRetry(new NotSupportedException("Unsupported op")).ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteSimpleQueryThroughRetryPolicy()
	{
		// Arrange
		var sut = CreateRetryPolicy();
		var request = new SimpleSelectRequest(CancellationToken.None);

		// Act
		var result = await sut.ResolveAsync<IDbConnection, int>(
			request,
			() => CreateConnectionAsync(),
			CancellationToken.None);

		// Assert
		result.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteParameterizedQueryThroughRetryPolicy()
	{
		// Arrange
		var sut = CreateRetryPolicy();
		var request = new ParameterizedSelectRequest(42, CancellationToken.None);

		// Act
		var result = await sut.ResolveAsync<IDbConnection, int>(
			request,
			() => CreateConnectionAsync(),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task PropagateNonTransientExceptionWithoutRetry()
	{
		// Arrange
		var sut = CreateRetryPolicy(retryCount: 3);
		var callCount = 0;
		var request = new FailingRequest(() =>
		{
			callCount++;
			throw new InvalidOperationException("Non-transient failure");
		}, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			sut.ResolveAsync<IDbConnection, int>(
				request,
				() => CreateConnectionAsync(),
				CancellationToken.None));

		// Non-transient exceptions should not be retried — called exactly once
		callCount.ShouldBe(1);
	}

	[Fact]
	public void ThrowNotSupportedForDocumentRequests()
	{
		// Arrange
		var sut = CreateRetryPolicy();
		var request = A.Fake<IDocumentDataRequest<IDbConnection, int>>();

		// Act & Assert — ResolveDocumentAsync throws synchronously
		Should.Throw<NotSupportedException>(() =>
			sut.ResolveDocumentAsync(
				request,
				() => CreateConnectionAsync(),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteMultipleQueriesSequentiallyThroughRetryPolicy()
	{
		// Arrange
		var sut = CreateRetryPolicy();

		// Act - Execute multiple queries to verify connection factory is called each time
		var results = new List<int>();
		for (var i = 1; i <= 5; i++)
		{
			var request = new ParameterizedSelectRequest(i * 10, CancellationToken.None);
			var result = await sut.ResolveAsync<IDbConnection, int>(
				request,
				() => CreateConnectionAsync(),
				CancellationToken.None);
			results.Add(result);
		}

		// Assert
		results.ShouldBe([10, 20, 30, 40, 50]);
	}

	[Fact]
	public async Task ExecuteQueryWithRealTableData()
	{
		// Arrange — create a temp table and insert data
		var sut = CreateRetryPolicy();
		var tableName = $"RetryTest_{Guid.NewGuid():N}".Substring(0, 30);

		await using var setupConn = new SqlConnection(_fixture.ConnectionString);
		await setupConn.OpenAsync();
		await setupConn.ExecuteAsync($"CREATE TABLE [{tableName}] (Id INT, Name NVARCHAR(100))");
		await setupConn.ExecuteAsync($"INSERT INTO [{tableName}] (Id, Name) VALUES (1, 'Test')");
		await setupConn.ExecuteAsync($"INSERT INTO [{tableName}] (Id, Name) VALUES (2, 'Another')");

		try
		{
			// Act
			var request = new CountRequest(tableName, CancellationToken.None);
			var result = await sut.ResolveAsync<IDbConnection, int>(
				request,
				() => CreateConnectionAsync(),
				CancellationToken.None);

			// Assert
			result.ShouldBe(2);
		}
		finally
		{
			await using var cleanupConn = new SqlConnection(_fixture.ConnectionString);
			await cleanupConn.OpenAsync();
			await cleanupConn.ExecuteAsync($"DROP TABLE IF EXISTS [{tableName}]");
		}
	}

	#region Helper Methods

	private SqlServerRetryPolicy CreateRetryPolicy(int retryCount = 3) =>
		new(CreateProviderOptions(retryCount), NullLogger.Instance);

	private static SqlServerProviderOptions CreateProviderOptions(int retryCount = 3) =>
		new()
		{
			RetryCount = retryCount,
			CommandTimeout = 30,
			ConnectTimeout = 15,
			TrustServerCertificate = true,
		};

	private async Task<IDbConnection> CreateConnectionAsync()
	{
		var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();
		return connection;
	}

	#endregion

	#region Test Data Request Types

	/// <summary>
	/// Simple SELECT 1 request for testing retry policy execution.
	/// </summary>
	private sealed class SimpleSelectRequest : IDataRequest<IDbConnection, int>
	{
		public SimpleSelectRequest(CancellationToken cancellationToken)
		{
			var sql = "SELECT 1";
			Parameters = new DynamicParameters();
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<int>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "SimpleSelect";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// Parameterized SELECT request for testing retry policy execution.
	/// </summary>
	private sealed class ParameterizedSelectRequest : IDataRequest<IDbConnection, int>
	{
		public ParameterizedSelectRequest(int value, CancellationToken cancellationToken)
		{
			var sql = "SELECT @Value";
			Parameters = new DynamicParameters();
			Parameters.Add("Value", value);
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<int>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "ParameterizedSelect";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// Count rows request for testing retry policy with real table data.
	/// </summary>
	private sealed class CountRequest : IDataRequest<IDbConnection, int>
	{
		public CountRequest(string tableName, CancellationToken cancellationToken)
		{
			var sql = $"SELECT COUNT(*) FROM [{tableName}]";
			Parameters = new DynamicParameters();
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn =>
			{
				var result = await conn.QuerySingleAsync<int>(Command);
				return result;
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<int>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "Count";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>
	/// A request that invokes a custom action, used to verify non-transient exceptions are not retried.
	/// </summary>
	private sealed class FailingRequest : IDataRequest<IDbConnection, int>
	{
		private readonly Action _onExecute;

		public FailingRequest(Action onExecute, CancellationToken cancellationToken)
		{
			_onExecute = onExecute;
			Parameters = new DynamicParameters();
			Command = new CommandDefinition("SELECT 1", Parameters, cancellationToken: cancellationToken);
			ResolveAsync = _ =>
			{
				_onExecute();
				return Task.FromResult(0);
			};
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<int>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "Failing";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}

	#endregion
}
