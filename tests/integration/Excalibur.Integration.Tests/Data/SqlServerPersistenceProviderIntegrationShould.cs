// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Data.Persistence;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PersistenceOptions = Excalibur.Data.SqlServer.Persistence.SqlServerPersistenceOptions;
using Provider = Excalibur.Data.SqlServer.Persistence.SqlServerPersistenceProvider;

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
public sealed class SqlServerPersistenceProviderIntegrationShould
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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(connectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

		// Act & Assert
		provider.ConnectionString.ShouldNotBeNullOrEmpty();
		provider.Name.ShouldBe("primary");
	}

	[Fact]
	public void ReportProviderProperties()
	{
		// Arrange
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

		// Act
		using var connection = await provider.CreateConnectionAsync(CancellationToken.None);

		// Assert
		_ = connection.ShouldNotBeNull();
	}

	[Fact]
	public void CreateTransactionScope()
	{
		// Arrange
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		metrics["Name"].ShouldBe("primary");
		metrics["IsAvailable"].ShouldBe(true);
	}

	[Fact]
	public async Task GetDatabaseStatisticsReturnsValidData()
	{
		// Arrange
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

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
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

		// Act
		provider.Dispose();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync()
	{
		// Arrange
		using var services = CreateServices(_fixture.ConnectionString);
		var provider = (Provider)services.GetRequiredService<ISqlPersistenceProvider>();

		// Act
		await provider.DisposeAsync();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods

	private static ServiceProvider CreateServices(string connectionString)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerPersistence(options =>
		{
			options.Name = "primary";
			options.ConnectionString = connectionString;
			options.CommandTimeout = 30;
			options.Connection.ConnectionTimeout = 15;
			options.Security.TrustServerCertificate = true;
			options.Pooling.EnableConnectionPooling = true;
			options.Pooling.MinPoolSize = 1;
			options.Pooling.MaxPoolSize = 10;
			options.Resiliency.MaxRetryAttempts = 3;
		});

		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = false,
			ValidateScopes = true,
		});
	}

	#endregion
}