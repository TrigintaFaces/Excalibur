// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Cdc.DynamoDb;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.LocalStack;

namespace Excalibur.Dispatch.Integration.Tests.Cdc;

/// <summary>
/// LocalStack container fixture for the DynamoDB CDC state-store conformance suite. Extends
/// <see cref="ContainerFixtureBase"/> so a missing container surfaces as a hard failure (never a silent
/// skip). Mirrors the LocalStack image and <c>SERVICES=dynamodb</c> shape already used elsewhere in this
/// project for DynamoDB-backed fixtures.
/// </summary>
#pragma warning disable CA1812 // Instantiated by the xUnit test runner via IClassFixture<T>.
public sealed class DynamoDbCdcContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets a DynamoDB client pointing at the LocalStack container. <see cref="DynamoDbCdcStateStore"/> has
	/// no <c>CreateTableIfNotExists</c> option of its own, so this fixture's caller provisions the table
	/// directly with this client before constructing the store.
	/// </summary>
	public IAmazonDynamoDB Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-cdc-conformance-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(credentials, config);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			_client?.Dispose();

			if (_container is not null)
			{
				await _container.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort - allow test host to exit cleanly.
		}
	}
}
#pragma warning restore CA1812

/// <summary>
/// Runs the shared CDC state-store conformance kit against the REAL <see cref="DynamoDbCdcStateStore"/> on
/// a LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CdcProviderConformanceTestKit"/> had no DynamoDB deriver, so every arm of the kit had never
/// exercised the JSON-over-base64 shard-position round-trip, the <c>pk</c>-keyed
/// <c>GetItem</c>/<c>PutItem</c>/<c>DeleteItem</c> calls, or the unfiltered <c>Scan</c> behind
/// <c>GetAllPositionsAsync</c> against a real table.
/// </para>
/// <para>
/// Each arm gets a freshly-created table, because <see cref="DynamoDbCdcStateStore"/> provisions nothing
/// itself and the kit's empty-store arm requires a table with no items.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Infrastructure", TestInfrastructure.DynamoDb)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DynamoDbCdcStateStoreConformanceTests
	: CdcProviderConformanceTestKit, IClassFixture<DynamoDbCdcContainerFixture>
{
	private const string StreamArn = "arn:aws:dynamodb:us-east-1:000000000000:table/cdc-conformance/stream/test";

	private readonly DynamoDbCdcContainerFixture _fixture;

	public DynamoDbCdcStateStoreConformanceTests(DynamoDbCdcContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override async Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a LocalStack DynamoDB container must be available - real-infra CDC conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working.");

		var cancellationToken = TestContext.Current.CancellationToken;
		var tableName = $"cdc_state_{Guid.NewGuid():N}";

		_ = await _fixture.Client.CreateTableAsync(
			new CreateTableRequest
			{
				TableName = tableName,
				KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
				AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
				BillingMode = BillingMode.PAY_PER_REQUEST,
			},
			cancellationToken).ConfigureAwait(false);

		await WaitForTableActiveAsync(tableName, cancellationToken).ConfigureAwait(false);

		ICdcStateStore store = new DynamoDbCdcStateStore(
			_fixture.Client,
			tableName,
			NullLogger<DynamoDbCdcStateStore>.Instance);

		return store;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Built through <see cref="DynamoDbCdcPosition"/> rather than as a free-form token, so the expected
	/// value is exactly what the store will hand back — <see cref="DynamoDbCdcPosition"/> IS a
	/// <see cref="ChangePosition"/>, so no lossy round-trip happens before the store's explicit
	/// <see cref="ICdcStateStore.SavePositionAsync"/> sees it. Each index gets its own shard sequence
	/// number so the positions are distinct and round-trip through the JSON-over-base64 encoding.
	/// </remarks>
	protected override ChangePosition CreateTestPosition(int index) =>
		DynamoDbCdcPosition.FromShardPositions(
			StreamArn,
			new Dictionary<string, string> { ["shard-0001"] = $"seq-{index:D6}" });

	private async Task WaitForTableActiveAsync(string tableName, CancellationToken cancellationToken)
	{
		// LocalStack normally reports ACTIVE immediately, but poll (bounded, not a fixed sleep) rather
		// than assume — a table used before it is ACTIVE fails the arm for a reason about this fixture,
		// not about the store.
		for (var attempt = 0; attempt < 30; attempt++)
		{
			var description = await _fixture.Client.DescribeTableAsync(tableName, cancellationToken)
				.ConfigureAwait(false);

			if (description.Table.TableStatus == TableStatus.ACTIVE)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		}

		throw new TimeoutException($"DynamoDB table '{tableName}' did not become ACTIVE in time.");
	}

	[Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
	[Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
	[Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
	[Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
	[Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
	[Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
	[Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
	[Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
	[Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
	[Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
	[Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
	[Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
	[Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();

	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
