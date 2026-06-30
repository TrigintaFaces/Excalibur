// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="DynamoDbEventStore"/> using the EventStore
/// Conformance Test Kit against a LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// The store is constructed with the fixture's <em>default-config</em> DynamoDB and DynamoDB Streams
/// clients (no custom serializers), so the suite exercises the real wire behaviour a default consumer
/// client produces — not a mocked client that could certify a non-functional provider.
/// </para>
/// <para>
/// Each test gets its own uniquely-named events table. The store's client-injecting constructor leaves it
/// uninitialised, so its <c>CreateTableIfNotExists</c> path creates the table on first use; cleanup deletes
/// that table via the fixture client.
/// </para>
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<DynamoDbEventStoreContainerFixture>
{
	private readonly DynamoDbEventStoreContainerFixture _fixture;
	private string? _tableName;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The DynamoDb container fixture.</param>
	public DynamoDbEventStoreConformanceShould(DynamoDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			$"LocalStack DynamoDB container must be available for real-infra conformance (never skipped): {_fixture.InitializationError}");

		// Unique table per test for isolation; the store auto-creates it via CreateTableIfNotExists.
		_tableName = $"{_fixture.TableName}_{Guid.NewGuid():N}";

		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = _tableName,
			CreateTableIfNotExists = true,
			UseOnDemandCapacity = true,
			EnableStreams = true
		});

		var store = new DynamoDbEventStore(
			_fixture.Client,
			_fixture.StreamsClient,
			options,
			NullLogger<DynamoDbEventStore>.Instance);

		return Task.FromResult<IEventStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_tableName is not null)
		{
			await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);
			_tableName = null;
		}
	}
}
