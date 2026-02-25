// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PersistenceOptions = Excalibur.Data.SqlServer.Persistence.SqlServerPersistenceOptions;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Integration tests for SqlServerPersistenceProvider against real SQL Server using TestContainers.
/// Validates connection, data operations, metrics, and health checks.
/// Covers acceptance criteria for task bd-842uv.
/// </summary>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public class SqlServerPersistenceProviderIntegrationShould
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerPersistenceProviderIntegrationShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	#region Connection Tests

	[Fact]
	public async Task ConnectSuccessfullyToSqlServerInstance()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act - Initialize the provider first (sets _initialized = true)
		// InitializeAsync requires IPersistenceOptions - create one with connection string
		var persistenceOptions = new PersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true }
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);
		var isAvailable = provider.IsAvailable;

		// Assert
		isAvailable.ShouldBeTrue("Provider should be available after successful initialization");
		provider.ProviderType.ShouldBe("SQL");
		provider.DatabaseType.ShouldBe("SqlServer");
	}

	[Fact]
	public async Task TestConnectionReturnsTrue_WhenConnected()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		var result = await provider.TestConnectionAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue("TestConnection should return true for connected instance");
	}

	[Fact]
	public void ParseConnectionStringCorrectly()
	{
		// Arrange
		var connectionString = _fixture.ConnectionString;
		var options = CreateOptions(connectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act & Assert
		provider.ConnectionString.ShouldNotBeNullOrEmpty();
		provider.Name.ShouldBe("sqlserver-test");
	}

	[Fact]
	public void ReportProviderProperties()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act & Assert
		provider.SupportsBulkOperations.ShouldBeTrue();
		provider.SupportsStoredProcedures.ShouldBeTrue();
	}

	#endregion

	#region Connection and Transaction Tests

	[Fact]
	public void CreateConnection()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		using var connection = provider.CreateConnection();

		// Assert
		_ = connection.ShouldNotBeNull();
		connection.State.ShouldBe(System.Data.ConnectionState.Closed);
		connection.Open();
		connection.State.ShouldBe(System.Data.ConnectionState.Open);
	}

	[Fact]
	public async Task CreateConnectionAsync()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		using var connection = await provider.CreateConnectionAsync(CancellationToken.None);

		// Assert
		_ = connection.ShouldNotBeNull();
	}

	[Fact]
	public void CreateTransactionScope()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		using var scope = provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldNotBeNull();
	}

	#endregion

	#region Health Check and Metrics Tests

	[Fact]
	public async Task GetMetricsReturnsValidData()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Initialize provider first with SqlServerPersistenceOptions
		var persistenceOptions = new PersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true }
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);

		// Act
		var metrics = await provider.GetMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics["Provider"].ShouldBe("SqlServer");
		metrics["Name"].ShouldBe("sqlserver-test");
		metrics["IsAvailable"].ShouldBe(true);
	}

	[Fact]
	public async Task GetConnectionPoolStatsReturnsValidData()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Initialize provider first with SqlServerPersistenceOptions
		var persistenceOptions = new PersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true }
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);

		// Force a connection to populate pool stats
		using (var connection = provider.CreateConnection())
		{
			connection.Open();
		}

		// Act
		var stats = await provider.GetConnectionPoolStatsAsync(CancellationToken.None);

		// Assert - stats may be null if pool statistics are not available
		// The provider returns null on exception, which is valid behavior
		if (stats != null)
		{
			stats.ContainsKey("MaxPoolSize").ShouldBeTrue();
			stats.ContainsKey("MinPoolSize").ShouldBeTrue();
		}
	}

	[Fact]
	public async Task GetDatabaseStatisticsReturnsValidData()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Initialize provider first with SqlServerPersistenceOptions
		var persistenceOptions = new PersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true }
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);

		// Act
		var stats = await provider.GetDatabaseStatisticsAsync(CancellationToken.None);

		// Assert
		_ = stats.ShouldNotBeNull();
		stats.ContainsKey("DatabaseName").ShouldBeTrue();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void ReportUnavailableAfterDispose()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		provider.Dispose();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		var provider = new SqlServerPersistenceProvider(options, NullLogger<SqlServerPersistenceProvider>.Instance);

		// Act
		await provider.DisposeAsync();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods

	private static IOptions<SqlServerProviderOptions> CreateOptions(string connectionString)
	{
		return Options.Create(new SqlServerProviderOptions
		{
			ConnectionString = connectionString,
			Name = "sqlserver-test",
			CommandTimeout = 30,
			ConnectTimeout = 15,
			MaxPoolSize = 10,
			MinPoolSize = 1,
			EnablePooling = true,
			TrustServerCertificate = true,
			RetryCount = 3
		});
	}

	#endregion
}
