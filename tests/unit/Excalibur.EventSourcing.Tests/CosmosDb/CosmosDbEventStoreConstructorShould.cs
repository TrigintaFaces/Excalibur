// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbEventStore"/> constructor pattern.
/// Verifies constructor validation and initialization.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CosmosDbEventStoreConstructorShould : UnitTestBase
{
	private readonly CosmosClient _cosmosClient;
	private readonly ILogger<CosmosDbEventStore> _logger;
	private readonly IOptions<CosmosDbEventStoreOptions> _validOptions;

	public CosmosDbEventStoreConstructorShould()
	{
		_cosmosClient = (CosmosClient)System.Runtime.CompilerServices.RuntimeHelpers
			.GetUninitializedObject(typeof(CosmosClient));
		_logger = NullLogger<CosmosDbEventStore>.Instance;
		_validOptions = Options.Create(new CosmosDbEventStoreOptions
		{
			EventsContainerName = "test-events"
		});
	}

	#region Constructor Validation Tests

	[Fact]
	public void Constructor_WithNullCosmosClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStore(cosmosClient: null!, _validOptions, _logger));
		exception.ParamName.ShouldBe("cosmosClient");
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStore(_cosmosClient, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new CosmosDbEventStore(_cosmosClient, _validOptions, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Validation Tests

	#region CloudProviderType Tests

	[Fact]
	public void CloudProviderType_ReturnCosmosDb()
	{
		// Arrange
		var sut = new CosmosDbEventStore(_cosmosClient, _validOptions, _logger);

		// Assert
		sut.ProviderType.ShouldBe(Excalibur.Data.Abstractions.CloudNative.CloudProviderType.CosmosDb);
	}

	#endregion CloudProviderType Tests
}
