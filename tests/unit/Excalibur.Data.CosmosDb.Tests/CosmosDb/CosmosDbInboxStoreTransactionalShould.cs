// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit lock for the Cosmos DB inbox store's transactional opt-out contract (exactly-once integrity):
/// the store must NOT advertise atomic handler-plus-mark unless a single shared partition is configured,
/// and must refuse — loudly — to run a transactional flow it cannot make atomic.
/// </summary>
/// <remarks>
/// Closes the VERIFY gap where <c>SupportsTransactional</c> gating and <c>AsCosmosBatch</c> enlistment
/// shipped impl-only. A Cosmos DB <c>TransactionalBatch</c> is single-partition, so atomicity of the
/// handler's writes with the processed-mark is only possible when they share a partition key; without one
/// the store advertises no transactional capability rather than falsely advertising atomicity.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait(TraitNames.Feature, TestFeatures.Inbox)]
public sealed class CosmosDbInboxStoreTransactionalShould
{
	private static CosmosDbInboxStore CreateStore(string? sharedPartitionKey)
	{
		var options = new CosmosDbInboxOptions
		{
			// A non-connecting connection string is sufficient: SupportsTransactional and the
			// no-shared-partition guard are pure option-gating and never open a connection.
			Client = { ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=dummy==;" },
			SharedPartitionKey = sharedPartitionKey,
		};

		return new CosmosDbInboxStore(
			Options.Create(options),
			A.Fake<ILogger<CosmosDbInboxStore>>(),
			tenantContext: TestTenantContext.SingleTenant);
	}

	[Fact]
	public void NotAdvertiseTransactional_WhenNoSharedPartitionKey()
	{
		var store = CreateStore(sharedPartitionKey: null);

		// No single shared partition -> a TransactionalBatch cannot span the handler writes + the mark,
		// so the store must NOT claim atomicity (no false atomic advertisement).
		store.SupportsTransactional.ShouldBeFalse();
	}

	[Fact]
	public void AdvertiseTransactional_WhenSharedPartitionKeyConfigured()
	{
		var store = CreateStore(sharedPartitionKey: "tenant-42");

		store.SupportsTransactional.ShouldBeTrue();
	}

	[Fact]
	public async Task RefuseTransactionalProcessing_WhenNoSharedPartitionKey()
	{
		var store = CreateStore(sharedPartitionKey: null);

		// Fail LOUD rather than silently degrade to a non-atomic flow while a caller believes it is atomic.
		var ex = await Should.ThrowAsync<NotSupportedException>(async () =>
			await store.TryProcessTransactionallyAsync(
				"msg-1",
				"OrderHandler",
				static (_, _) => ValueTask.CompletedTask,
				CancellationToken.None));

		ex.Message.ShouldContain("SharedPartitionKey");
	}

	[Fact]
	public void ExposeUnderlyingBatch_FromCosmosScope_ViaAsCosmosBatch()
	{
		var batch = A.Fake<TransactionalBatch>();
		var scope = new CosmosInboxTransactionScope(batch, new PartitionKey("tenant-42"));

		scope.AsCosmosBatch().ShouldBeSameAs(batch);
	}

	[Fact]
	public void FailLoud_WhenAsCosmosBatchCalledOnForeignProviderScope()
	{
		// A wrong-provider scope (e.g. a MongoDB scope) must surface a provider mismatch immediately,
		// not a null or an obscure cast failure.
		var foreignScope = A.Fake<IInboxTransactionScope>();

		var ex = Should.Throw<InvalidOperationException>(() => foreignScope.AsCosmosBatch());

		ex.Message.ShouldContain("not a Cosmos DB scope");
	}

	[Fact]
	public void ThrowArgumentNull_WhenAsCosmosBatchCalledOnNullScope()
	{
		IInboxTransactionScope scope = null!;

		Should.Throw<ArgumentNullException>(() => scope.AsCosmosBatch());
	}
}
