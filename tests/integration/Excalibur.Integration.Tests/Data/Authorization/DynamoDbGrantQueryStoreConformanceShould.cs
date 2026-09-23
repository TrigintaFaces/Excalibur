// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.A3.Authorization;
using Excalibur.Data.DynamoDb.Authorization;
using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Inbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Integration.Tests.Data.Authorization;

/// <summary>
/// Holds <see cref="DynamoDbGrantStore"/> to the shared <see cref="IGrantQueryStore"/> contract on a
/// LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// The grant store never creates its table (its initialization only describes it), so this suite
/// provisions a per-instance table with the key schema the store's items carry: <c>tenant_id</c> hash,
/// <c>sk</c> range, and the user index on <c>gsi_user_id</c>/<c>gsi_sk</c>. The table is deleted on the
/// way out. The store borrows the fixture's default-config client, which it does not dispose.
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
[Trait("Pattern", "STORE")]
public sealed class DynamoDbGrantQueryStoreConformanceShould : GrantQueryStoreConformanceTestBase
{
	private const string UserIndexName = "UserIndex";

	private readonly DynamoDbInboxStoreContainerFixture _fixture;
	private readonly string _tableName = $"grants_conf_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbGrantQueryStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The LocalStack DynamoDB fixture, shared by the collection.</param>
	public DynamoDbGrantQueryStoreConformanceShould(DynamoDbInboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync().ConfigureAwait(false);
		if (_fixture.DockerAvailable)
		{
			await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override async Task<IGrantStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available for real-infra conformance (never skipped): "
			+ _fixture.InitializationError);

		_ = await _fixture.Client.CreateTableAsync(
			new CreateTableRequest
			{
				TableName = _tableName,
				BillingMode = BillingMode.PAY_PER_REQUEST,
				AttributeDefinitions =
				[
					new AttributeDefinition("tenant_id", ScalarAttributeType.S),
					new AttributeDefinition("sk", ScalarAttributeType.S),
					new AttributeDefinition("gsi_user_id", ScalarAttributeType.S),
					new AttributeDefinition("gsi_sk", ScalarAttributeType.S),
				],
				KeySchema = [new KeySchemaElement("tenant_id", KeyType.HASH), new KeySchemaElement("sk", KeyType.RANGE)],
				GlobalSecondaryIndexes =
				[
					new GlobalSecondaryIndex
					{
						IndexName = UserIndexName,
						KeySchema = [new KeySchemaElement("gsi_user_id", KeyType.HASH), new KeySchemaElement("gsi_sk", KeyType.RANGE)],
						Projection = new Projection { ProjectionType = ProjectionType.ALL },
					},
				],
			}).ConfigureAwait(false);

		return new DynamoDbGrantStore(
			_fixture.Client,
			Options.Create(new DynamoDbAuthorizationOptions { GrantsTableName = _tableName, UserIndexName = UserIndexName }),
			NullLogger<DynamoDbGrantStore>.Instance);
	}
}
