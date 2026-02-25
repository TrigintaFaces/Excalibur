// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DynamoDb;

/// <summary>
/// Unit tests for the <see cref="DynamoDbEventStore"/> constructor pattern.
/// Verifies constructor validation and initialization.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbEventStoreConstructorShould : UnitTestBase
{
	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly ILogger<DynamoDbEventStore> _logger;
	private readonly IOptions<DynamoDbEventStoreOptions> _validOptions;

	public DynamoDbEventStoreConstructorShould()
	{
		_client = A.Fake<IAmazonDynamoDB>();
		_streamsClient = A.Fake<IAmazonDynamoDBStreams>();
		_logger = A.Fake<ILogger<DynamoDbEventStore>>();
		_validOptions = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = "TestEvents"
		});
	}

	#region Constructor Validation Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange & Act
		var store = new DynamoDbEventStore(_client, _streamsClient, _validOptions, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStore(client: null!, _streamsClient, _validOptions, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void Constructor_WithNullStreamsClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStore(_client, streamsClient: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("streamsClient");
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStore(_client, _streamsClient, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new DynamoDbEventStore(_client, _streamsClient, _validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Validation Tests

	#region CloudProviderType Tests

	[Fact]
	public void ProviderType_ReturnsDynamoDb()
	{
		// Arrange
		var store = new DynamoDbEventStore(_client, _streamsClient, _validOptions, _logger);

		// Act & Assert
		store.ProviderType.ShouldBe(CloudProviderType.DynamoDb);
	}

	#endregion CloudProviderType Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new DynamoDbEventStore(_client, _streamsClient, _validOptions, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_DoesNotDisposeInjectedClient()
	{
		// Arrange - Injected clients should not be disposed by the store
		// The DI container is responsible for disposing injected dependencies
		var store = new DynamoDbEventStore(_client, _streamsClient, _validOptions, _logger);

		// Act
		await store.DisposeAsync();

		// Assert - Injected clients should NOT be disposed
		A.CallTo(() => _client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task DisposeAsync_DoesNotDisposeInjectedStreamsClient()
	{
		// Arrange - Injected clients should not be disposed by the store
		// The DI container is responsible for disposing injected dependencies
		var store = new DynamoDbEventStore(_client, _streamsClient, _validOptions, _logger);

		// Act
		await store.DisposeAsync();

		// Assert - Injected clients should NOT be disposed
		A.CallTo(() => _streamsClient.Dispose()).MustNotHaveHappened();
	}

	#endregion Dispose Tests
}
