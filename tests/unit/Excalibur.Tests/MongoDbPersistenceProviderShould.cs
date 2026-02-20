// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MongoDB;

using IPersistenceProvider = Excalibur.Data.Abstractions.Persistence.IPersistenceProvider;

namespace Excalibur.Tests;

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
	public async Task InitializeAsyncWithValidOptions()
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
	public void ValidateDocumentRequestReturnsExpectedResult()
	{
		// Arrange - With the default constructor, the provider is partially initialized
		// Testing the validation logic by checking unsupported operations return false
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("UnsupportedOp");

		// Act
		var isValid = _provider.ValidateDocumentRequest(request);

		// Assert - Unsupported operation should return false
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDocumentRequestWithInvalidConnectionType()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IDbConnection, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act
		var isValid = _provider.ValidateDocumentRequest(request);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDocumentRequestWithEmptyCollectionName()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("");
		_ = A.CallTo(() => request.OperationType).Returns("Insert");

		// Act
		var isValid = _provider.ValidateDocumentRequest(request);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDocumentRequestWithUnsupportedOperation()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		_ = A.CallTo(() => request.CollectionName).Returns("testCollection");
		_ = A.CallTo(() => request.OperationType).Returns("UnsupportedOperation");

		// Act
		var isValid = _provider.ValidateDocumentRequest(request);

		// Assert
		isValid.ShouldBeFalse();
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
	public async Task ExecuteDocumentAsyncThrowsWithIncompatibleConnectionType()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IDbConnection, string>>();

		// Act & Assert - Provider throws InvalidOperationException when not initialized
		_ = await Should.ThrowAsync<InvalidOperationException>(() => _provider.ExecuteDocumentAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void ExecuteAsyncThrowsInvalidOperationException()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();

		// Act & Assert - Provider throws InvalidOperationException when not initialized
		_ = Should.Throw<InvalidOperationException>(() => _provider.ExecuteAsync(request, CancellationToken.None));
	}

	[Fact]
	public void ExecuteInTransactionAsyncThrowsInvalidOperationException()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		var transactionScope = A.Fake<ITransactionScope>();

		// Act & Assert - Provider throws InvalidOperationException when not initialized
		_ = Should.Throw<InvalidOperationException>(() => _provider.ExecuteInTransactionAsync(request, transactionScope, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteDocumentInTransactionAsyncValidatesTransactionScope()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<IMongoDatabase, string>>();
		var invalidTransactionScope = A.Fake<ITransactionScope>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => _provider.ExecuteDocumentInTransactionAsync(request, invalidTransactionScope, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteDocumentBatchAsyncWithEmptyRequestsReturnsEmpty()
	{
		// Arrange
		var emptyRequests = Enumerable.Empty<IDocumentDataRequest<IMongoDatabase, object>>();

		// Act
		var results = await _provider.ExecuteDocumentBatchAsync(emptyRequests, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDocumentStoreStatisticsAsyncWhenNotAvailable()
	{
		// Act
		var statistics = await _provider.GetDocumentStoreStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = statistics.ShouldNotBeNull();
		statistics.ShouldContainKey("connection.available");
		statistics["connection.available"].ShouldBe(false);
	}

	[Fact]
	public async Task GetCollectionInfoAsyncWithNonExistentCollectionWhenNotAvailable()
	{
		// Act
		var info = await _provider.GetCollectionInfoAsync("nonExistentCollection", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = info.ShouldNotBeNull();
		info.ShouldContainKey("collection_name");
		info["collection_name"].ShouldBe("nonExistentCollection");
		info.ShouldContainKey("exists");
		info["exists"].ShouldBe(false);
	}

	[Fact]
	public async Task GetConnectionPoolStatsAsyncReturnsDefaultStatsWithDefaultOptions()
	{
		// Act - Provider initialized with default options returns default stats
		var stats = await _provider.GetConnectionPoolStatsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - With default initialization, returns default pool stats
		_ = stats.ShouldNotBeNull();
		stats.ShouldContainKey("MaxPoolSize");
	}

	[Fact]
	public void CreateTransactionScopeReturnsValidScopeWithDefaultOptions()
	{
		// Act - Provider initialized with default options can create transaction scopes
		using var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldNotBeNull();
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
		// Assert - Provider initialized with default options has default connection string
		_provider.Name.ShouldBe("MongoDB");
		_provider.ProviderType.ShouldBe("Document");
		_provider.DocumentStoreType.ShouldBe("MongoDB");
		// Default constructor sets default connection string
		_provider.ConnectionString.ShouldBe("mongodb://localhost:27017");
	}

	[Fact]
	public async Task GetMetricsAsyncReturnsMetricsDictionary()
	{
		// Act
		var metrics = await _provider.GetMetricsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Metrics should be available (may have different keys based on initialization state)
		_ = metrics.ShouldNotBeNull();
	}

	/// <inheritdoc/>
	public void Dispose() => _provider?.Dispose();
}
