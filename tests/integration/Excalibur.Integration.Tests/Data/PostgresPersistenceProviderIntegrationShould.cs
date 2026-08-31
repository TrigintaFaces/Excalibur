// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Data.Postgres.Persistence;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Integration tests for PostgresPersistenceProvider against real Postgres using TestContainers.
/// Validates connection, data operations, metrics, and health checks.
/// </summary>
[Collection(PostgresTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class PostgresPersistenceProviderIntegrationShould
{
	private readonly PostgresContainerFixture _fixture;

	public PostgresPersistenceProviderIntegrationShould(PostgresContainerFixture fixture)
	{
		_fixture = fixture;
	}

	#region Connection Tests

	[Fact]
	public async Task ConnectSuccessfullyToPostgresInstance()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

		// Act - Initialize the provider first (sets _initialized = true)
		var persistenceOptions = new PostgresPersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);
		var isAvailable = provider.IsAvailable;

		// Assert
		isAvailable.ShouldBeTrue("Provider should be available after successful initialization");
		provider.ProviderType.ShouldBe("SQL");
	}

	[Fact]
	public async Task TestConnectionReturnsTrue_WhenConnected()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

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
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

		// Act & Assert
		provider.ConnectionString.ShouldNotBeNullOrEmpty();
		provider.Name.ShouldBe("reporting");
	}

	#endregion

	#region Connection and Transaction Tests

	[Fact]
	public void CreateConnection()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

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
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

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
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

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
		using var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

		// Initialize provider first
		var persistenceOptions = new PostgresPersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString
		};
		await provider.InitializeAsync(persistenceOptions, CancellationToken.None);

		// Act
		var metrics = await provider.GetMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics["Provider"].ShouldBe("Postgres");
		metrics["Name"].ShouldBe("reporting");
		metrics["IsAvailable"].ShouldBe(true);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void ReportUnavailableAfterDispose()
	{
		// Arrange
		var options = CreateOptions(_fixture.ConnectionString);
		var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

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
		var provider = new PostgresPersistenceProvider(options, NullLogger<PostgresPersistenceProvider>.Instance);

		// Act
		await provider.DisposeAsync();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods

	private static IOptions<PostgresPersistenceOptions> CreateOptions(string connectionString)
	{
		return Options.Create(new PostgresPersistenceOptions
		{
			Name = "reporting",
			ConnectionString = connectionString,
			CommandTimeout = 30,
			ConnectionTimeout = 15,
			Pooling =
			{
				MaxPoolSize = 10,
				MinPoolSize = 1,
				EnableConnectionPooling = true,
			},
			Resilience =
			{
				MaxRetryAttempts = 3,
			},
		});
	}

	#endregion
}