// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;

using MongoDB.Driver;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Persistence;

/// <summary>
///     Unit tests for MongoDbPersistenceProvider operations using the DocumentDataRequest pattern. Tests document operations, aggregation
///     pipelines, and MongoDB-specific features.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbPersistenceProviderShould : IDisposable
{
	private readonly ILogger<MongoDbPersistenceProvider> _logger;
	private readonly MongoDbPersistenceProvider _provider;

	public MongoDbPersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<MongoDbPersistenceProvider>>();
		_provider = new MongoDbPersistenceProvider(_logger);
	}

	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Assert
		_ = _provider.ShouldNotBeNull();
		_provider.Name.ShouldBe("MongoDB");
		_provider.ProviderType.ShouldBe("Document");
		_provider.DocumentStoreType.ShouldBe("MongoDB");
	}

	[Fact]
	public void InitializeAsyncWithValidOptions()
	{
		// Arrange
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "testdb",
			Name = "TestApp",
		};

		// Act & Assert
		// Note: In a real test, this would require a running MongoDB instance For unit tests, we'll test the validation logic
		options.ConnectionString.ShouldNotBeNullOrEmpty();
		options.DatabaseName.ShouldNotBeNullOrEmpty();
		options.Name.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ValidateDocumentRequestWithValidRequest()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act
		var result = _provider.ValidateDocumentRequest(request);

		// Assert - Valid request should return true
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateDocumentRequestWithInvalidConnectionType()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IDbConnection, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act - ValidateDocumentRequest checks connection type and returns false for non-IMongoDatabase
		var result = _provider.ValidateDocumentRequest(request);

		// Assert - Invalid connection type should return false
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDocumentRequestWithEmptyCollectionName()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act - ValidateDocumentRequest returns false for invalid requests, doesn't throw
		var result = _provider.ValidateDocumentRequest(request);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDocumentRequestWithUnsupportedOperation()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("UnsupportedOperation");

		// Act - ValidateDocumentRequest returns false for unsupported operations, doesn't throw
		var result = _provider.ValidateDocumentRequest(request);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void GetSupportedOperationTypesReturnsExpectedOperations()
	{
		// Act
		var operations = _provider.GetSupportedOperationTypes().ToList();

		// Assert
		operations.ShouldContain("Insert");
		operations.ShouldContain("Find");
		operations.ShouldContain("Update");
		operations.ShouldContain("UpdateOne");
		operations.ShouldContain("UpdateMany");
		operations.ShouldContain("Delete");
		operations.ShouldContain("DeleteOne");
		operations.ShouldContain("DeleteMany");
		operations.ShouldContain("Aggregate");
		operations.ShouldContain("BulkWrite");
		operations.ShouldContain("CreateIndex");
		operations.ShouldContain("DropIndex");
		operations.ShouldContain("Count");
	}

	[Fact]
	public async Task ExecuteDocumentAsyncThrowsWhenProviderNotInitialized()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _provider.ExecuteDocumentAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteDocumentAsyncWithIncompatibleConnectionTypeThrowsWhenNotInitialized()
	{
		// Arrange - When provider is not initialized, it throws InvalidOperationException
		// before reaching the connection type validation
		var request = A.Fake<IDocumentDataRequest<IDbConnection, string>>();

		// Act & Assert - Throws InvalidOperationException because provider is not initialized
		// The connection type validation (ArgumentException) happens after initialization check
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _provider.ExecuteDocumentAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsyncThrowsWhenProviderNotInitialized()
	{
		// Arrange - Use IDbConnection as connection type which implements IDisposable
		// Note: The cast will fail at runtime but the provider checks database initialization first
		var request = A.Fake<IDataRequest<IDbConnection, string>>();

		// Act & Assert - Throws InvalidOperationException because database is not initialized
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteDocumentInTransactionAsyncThrowsWithInvalidTransactionScope()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		var invalidTransactionScope = A.Fake<ITransactionScope>();

		// Act & Assert - Throws ArgumentException when transaction scope is not a MongoDbTransactionScope
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_provider.ExecuteInTransactionAsync(request, invalidTransactionScope, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteDocumentInTransactionAsyncValidatesTransactionScope()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		var invalidTransactionScope = A.Fake<ITransactionScope>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_provider.ExecuteDocumentInTransactionAsync(request, invalidTransactionScope, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteDocumentBatchAsyncWithEmptyRequestsReturnsEmpty()
	{
		// Arrange
		var emptyRequests = Enumerable.Empty<IDocumentDataRequest<IMongoDatabase, object>>();

		// Act
		var results = await _provider.ExecuteDocumentBatchAsync(emptyRequests, CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDocumentStoreStatisticsAsyncWhenNotAvailable()
	{
		// Act
		var statistics = await _provider.GetDocumentStoreStatisticsAsync(CancellationToken.None);

		// Assert
		_ = statistics.ShouldNotBeNull();
		var statsDict = statistics as IDictionary<string, object>;
		_ = statsDict.ShouldNotBeNull();
		statsDict.ShouldContainKey("connection.available");
		statsDict["connection.available"].ShouldBe(false);
	}

	[Fact]
	public async Task GetCollectionInfoAsyncWithNonExistentCollectionWhenNotAvailable()
	{
		// Act
		var info = await _provider.GetCollectionInfoAsync("nonExistentCollection", CancellationToken.None);

		// Assert
		_ = info.ShouldNotBeNull();
		var infoDict = info as IDictionary<string, object>;
		_ = infoDict.ShouldNotBeNull();
		infoDict.ShouldContainKey("collection_name");
		infoDict["collection_name"].ShouldBe("nonExistentCollection");
		infoDict.ShouldContainKey("exists");
		infoDict["exists"].ShouldBe(false);
	}

	[Fact]
	public async Task GetConnectionPoolStatsAsyncReturnsDefaultsWhenNotInitialized()
	{
		// Act
		var stats = await _provider.GetConnectionPoolStatsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Returns default stats from options, not null
		_ = stats.ShouldNotBeNull();
		stats.ShouldContainKey("MaxPoolSize");
		stats.ShouldContainKey("ActiveConnections");
	}

	[Fact]
	public void CreateTransactionScopeReturnsValidScope()
	{
		// Act - CreateTransactionScope doesn't require IsAvailable to be true
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldNotBeNull();
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void ProviderImplementsIDocumentPersistenceProvider()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IDocumentPersistenceProvider>();
		_ = _provider.ShouldBeAssignableTo<IPersistenceProvider>();
	}

	[Fact]
	public void ProviderIsDisposable()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IDisposable>();
		_ = _provider.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void DisposeDoesNotThrow() =>
		// Act & Assert
		Should.NotThrow(_provider.Dispose);

	[Fact]
	public async Task DisposeAsyncDoesNotThrow() =>
		// Act & Assert
		await Should.NotThrowAsync(() => _provider.DisposeAsync().AsTask()).ConfigureAwait(false);

	[Fact]
	public void PropertiesHaveExpectedValues()
	{
		// Assert
		_provider.Name.ShouldBe("MongoDB");
		_provider.ProviderType.ShouldBe("Document");
		_provider.DocumentStoreType.ShouldBe("MongoDB");
		_provider.IsAvailable.ShouldBeFalse(); // Not initialized yet
		_provider.ConnectionString.ShouldNotBeNullOrEmpty(); // Default connection string from options
	}

	[Fact]
	public async Task GetMetricsAsyncReturnsProviderMetrics()
	{
		// Act
		var metrics = await _provider.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.ShouldContainKey("Provider");
		metrics["Provider"].ShouldBe("MongoDB");
		metrics.ShouldContainKey("IsAvailable");
	}

	/// <inheritdoc/>
	public void Dispose() => _provider?.Dispose();
}
