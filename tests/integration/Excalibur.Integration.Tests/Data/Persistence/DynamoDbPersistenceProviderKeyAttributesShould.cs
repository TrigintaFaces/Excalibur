// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.CloudNative;
using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-DynamoDB (LocalStack) lock proving the persistence provider's existence guards test the partition
/// key attribute the consumer CONFIGURED, not a hardcoded <c>pk</c>.
/// </summary>
/// <remarks>
/// <para>
/// The create guard was <c>attribute_not_exists(pk)</c> and the update guard <c>attribute_exists(pk)</c>,
/// while every key the provider composes uses <c>DefaultPartitionKeyAttribute</c>. On a table whose partition
/// key carries any other name no item ever has a <c>pk</c> attribute, so the create guard is always TRUE --
/// a second create of the same document silently OVERWRITES the first and reports success -- and the update
/// guard is always FALSE, so every update of an existing document fails as a precondition failure.
/// </para>
/// <para>
/// The batch arms also lock the document identity the batch path writes under: it resolved the id property
/// against the static <see cref="object"/> type the batch hands over, found none, and keyed every batch write
/// by a freshly generated id -- so a batch Create never met the item it duplicated.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Collection(DynamoDbPersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbPersistenceProviderKeyAttributesShould : IAsyncLifetime
{
	private const string RenamedPartitionKey = "tenantPartition";
	private const string RenamedSortKey = "documentKey";

	private readonly DynamoDbPersistenceProviderContainerFixture _fixture;
	private readonly List<string> _tables = [];

	public DynamoDbPersistenceProviderKeyAttributesShould(DynamoDbPersistenceProviderContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var table in _tables)
		{
			try
			{
				_ = await _fixture.Client.DeleteTableAsync(table, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ResourceNotFoundException)
			{
				// Already gone.
			}
		}
	}

	/// <summary>
	/// SAFETY — the defect. A second create of an existing document must be refused as a conflict and leave
	/// the original intact, on a table whose key attributes are renamed away from the defaults.
	/// </summary>
	[Fact]
	public async Task RefuseADuplicateCreate_WhenTheKeyAttributesAreRenamed()
	{
		var provider = await CreateProviderAsync(RenamedPartitionKey, RenamedSortKey).ConfigureAwait(false);
		await AssertDuplicateCreateIsRefusedAsync(provider).ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY — the same guard inside the atomic batch path. A batch that creates an existing document must
	/// be cancelled, and the existing document must survive.
	/// </summary>
	[Fact]
	public async Task RefuseADuplicateCreateInABatch_WhenTheKeyAttributesAreRenamed()
	{
		var provider = await CreateProviderAsync(RenamedPartitionKey, RenamedSortKey).ConfigureAwait(false);
		await AssertDuplicateBatchCreateIsRefusedAsync(provider).ConfigureAwait(false);
	}

	private static async Task AssertDuplicateBatchCreateIsRefusedAsync(DynamoDbPersistenceProvider provider)
	{
		var partitionKey = new PartitionKey($"p-{Guid.NewGuid():N}");
		var id = $"doc-{Guid.NewGuid():N}";

		var seeded = await provider.CreateAsync(new KeyedDocument(id, "original"), partitionKey, CancellationToken.None)
			.ConfigureAwait(false);
		seeded.Success.ShouldBeTrue($"seeding must succeed: {seeded.ErrorMessage}");

		var batch = await provider.ExecuteBatchAsync(
			partitionKey,
			[new CloudBatchCreateOperation(id, new KeyedDocument(id, "overwrite"))],
			CancellationToken.None).ConfigureAwait(false);

		batch.Success.ShouldBeFalse(
			"a batch Create of an existing document must be cancelled by its attribute_not_exists guard. Success "
			+ "means either the guard tested an attribute the item does not carry, or the batch keyed the write by "
			+ "a generated id instead of the document's own, so it never addressed the existing item at all.");

		var stored = await provider.GetByIdAsync<KeyedDocument>(id, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.Marker.ShouldBe("original");
	}

	/// <summary>
	/// SAFETY — the update guard. Updating a document that exists must succeed on a renamed-key table.
	/// </summary>
	[Fact]
	public async Task UpdateAnExistingDocument_WhenTheKeyAttributesAreRenamed()
	{
		var provider = await CreateProviderAsync(RenamedPartitionKey, RenamedSortKey).ConfigureAwait(false);
		await AssertUpdateOfExistingDocumentSucceedsAsync(provider).ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY — the update guard must still refuse a document that does not exist. An update guard that
	/// always passed would satisfy the arm above while turning every update into an upsert.
	/// </summary>
	[Fact]
	public async Task RefuseToUpdateAMissingDocument_WhenTheKeyAttributesAreRenamed()
	{
		var provider = await CreateProviderAsync(RenamedPartitionKey, RenamedSortKey).ConfigureAwait(false);
		var partitionKey = new PartitionKey($"p-{Guid.NewGuid():N}");
		var id = $"doc-{Guid.NewGuid():N}";

		var updated = await provider.UpdateAsync(
			new KeyedDocument(id, "phantom"), partitionKey, etag: null, CancellationToken.None).ConfigureAwait(false);

		updated.Success.ShouldBeFalse("an update of a document that does not exist must be refused, not upserted");
		updated.StatusCode.ShouldBe((int)HttpStatusCode.PreconditionFailed);
		(await provider.GetByIdAsync<KeyedDocument>(id, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS — the default key names still behave identically, so the fix did not trade one configuration
	/// for the other.
	/// </summary>
	[Fact]
	public async Task GuardCreateAndUpdate_WhenTheKeyAttributesAreTheDefaults()
	{
		var provider = await CreateProviderAsync("pk", "sk").ConfigureAwait(false);
		await AssertDuplicateCreateIsRefusedAsync(provider).ConfigureAwait(false);
		await AssertDuplicateBatchCreateIsRefusedAsync(provider).ConfigureAwait(false);
		await AssertUpdateOfExistingDocumentSucceedsAsync(provider).ConfigureAwait(false);
	}

	private static async Task AssertDuplicateCreateIsRefusedAsync(DynamoDbPersistenceProvider provider)
	{
		var partitionKey = new PartitionKey($"p-{Guid.NewGuid():N}");
		var id = $"doc-{Guid.NewGuid():N}";

		var first = await provider.CreateAsync(new KeyedDocument(id, "original"), partitionKey, CancellationToken.None)
			.ConfigureAwait(false);
		first.Success.ShouldBeTrue($"the first create must succeed: {first.ErrorMessage}");

		var second = await provider.CreateAsync(new KeyedDocument(id, "overwrite"), partitionKey, CancellationToken.None)
			.ConfigureAwait(false);

		second.Success.ShouldBeFalse(
			"a second create of the same document must be refused. Success means the attribute_not_exists guard "
			+ "tested an attribute the item does not carry, and the original was silently overwritten.");
		second.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);

		var stored = await provider.GetByIdAsync<KeyedDocument>(id, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.Marker.ShouldBe("original", "the refused create must not have replaced the stored document");
	}

	private static async Task AssertUpdateOfExistingDocumentSucceedsAsync(DynamoDbPersistenceProvider provider)
	{
		var partitionKey = new PartitionKey($"p-{Guid.NewGuid():N}");
		var id = $"doc-{Guid.NewGuid():N}";

		var created = await provider.CreateAsync(new KeyedDocument(id, "original"), partitionKey, CancellationToken.None)
			.ConfigureAwait(false);
		created.Success.ShouldBeTrue($"the create must succeed: {created.ErrorMessage}");

		var updated = await provider.UpdateAsync(
			new KeyedDocument(id, "updated"), partitionKey, etag: null, CancellationToken.None).ConfigureAwait(false);

		updated.Success.ShouldBeTrue(
			$"updating a document that exists must succeed, and instead returned {updated.StatusCode} "
			+ $"({updated.ErrorMessage}). A 412 means the attribute_exists guard tested an attribute the item "
			+ "does not carry.");

		var stored = await provider.GetByIdAsync<KeyedDocument>(id, partitionKey, null, CancellationToken.None)
			.ConfigureAwait(false);
		stored.ShouldNotBeNull();
		stored.Marker.ShouldBe("updated");
	}

	private async Task<DynamoDbPersistenceProvider> CreateProviderAsync(string partitionKeyAttribute, string sortKeyAttribute)
	{
		_fixture.DockerAvailable.ShouldBeTrue("LocalStack DynamoDB must be available — never skipped.");

		var table = $"provider_keys_{Guid.NewGuid():N}";
		_ = await _fixture.Client.CreateTableAsync(
			new CreateTableRequest
			{
				TableName = table,
				AttributeDefinitions =
				[
					new AttributeDefinition(partitionKeyAttribute, ScalarAttributeType.S),
					new AttributeDefinition(sortKeyAttribute, ScalarAttributeType.S)
				],
				KeySchema =
				[
					new KeySchemaElement(partitionKeyAttribute, KeyType.HASH),
					new KeySchemaElement(sortKeyAttribute, KeyType.RANGE)
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			},
			CancellationToken.None).ConfigureAwait(false);
		_tables.Add(table);

		for (var attempt = 0; ; attempt++)
		{
			var described = await _fixture.Client.DescribeTableAsync(table, CancellationToken.None).ConfigureAwait(false);
			if (described.Table.TableStatus == TableStatus.ACTIVE)
			{
				break;
			}

			attempt.ShouldBeLessThan(120, $"table '{table}' did not become ACTIVE");
			await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false); // delay-ok: poll pacing; the loop exits on TableStatus.ACTIVE and is capped by attempt count, not by the clock
		}

		return new DynamoDbPersistenceProvider(
			_fixture.Client,
			Options.Create(new DynamoDbOptions
			{
				Name = "dynamodb-key-attributes",
				DefaultTableName = table,
				DefaultPartitionKeyAttribute = partitionKeyAttribute,
				DefaultSortKeyAttribute = sortKeyAttribute,
			}),
			NullLogger<DynamoDbPersistenceProvider>.Instance);
	}

	private sealed record KeyedDocument(string Id, string Marker);
}
