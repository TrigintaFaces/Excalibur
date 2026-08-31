// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Locks client-lifetime ownership for the DynamoDB types that accept a consumer-supplied
/// <see cref="IAmazonDynamoDB"/>.
/// </summary>
/// <remarks>
/// <para>
/// Whoever creates the client disposes it. A consumer who constructs a client, registers it as a shared
/// singleton, and hands it to a store keeps ownership of it: disposing that store must leave the client
/// usable for every other consumer of the same instance. Where the store constructed the client itself,
/// disposing the store must still dispose the client, or the fix trades a double-dispose for a leak.
/// </para>
/// <para>
/// Both directions are asserted per type. The safety arm alone is satisfied by a store that disposes
/// nothing at all, so it cannot stand on its own.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Disposal")]
public sealed class DynamoDbClientOwnershipDisposalShould
{
	private static IOptions<DynamoDbAuthorizationOptions> AuthOptions()
	{
		var opts = new DynamoDbAuthorizationOptions();
		opts.Connection.Region = "us-east-1";
		return Options.Create(opts);
	}

	private static IOptions<DynamoDbOptions> ProviderOptions()
	{
		var opts = new DynamoDbOptions();
		opts.Connection.Region = "us-east-1";
		return Options.Create(opts);
	}

	/// <summary>
	/// Substitutes the client a self-constructing store would have built with an observable fake.
	/// The store still believes it OWNS the client, so this asserts the owning path's disposal
	/// behaviour without reaching AWS.
	/// </summary>
	private static IAmazonDynamoDB PlantOwnedClient(object store, string field)
	{
		var fake = A.Fake<IAmazonDynamoDB>();
		var f = store.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"field {field} not found on {store.GetType().Name}");
		f.SetValue(store, fake);
		return fake;
	}

	// ---------- DynamoDbGrantStore ----------

	[Fact]
	public void GrantStore_WithConsumerSuppliedClient_LeavesClientUsable()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var store = new DynamoDbGrantStore(client, AuthOptions(), NullLogger<DynamoDbGrantStore>.Instance);

		store.Dispose();

		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task GrantStore_WithConsumerSuppliedClient_LeavesClientUsableOnAsyncDispose()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var store = new DynamoDbGrantStore(client, AuthOptions(), NullLogger<DynamoDbGrantStore>.Instance);

		await store.DisposeAsync();

		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void GrantStore_WithSelfCreatedClient_DisposesIt()
	{
		var store = new DynamoDbGrantStore(AuthOptions(), NullLogger<DynamoDbGrantStore>.Instance);
		var owned = PlantOwnedClient(store, "_client");

		store.Dispose();

		A.CallTo(() => owned.Dispose()).MustHaveHappened();
	}

	// ---------- DynamoDbActivityGroupGrantStore ----------

	[Fact]
	public void ActivityGroupGrantStore_WithConsumerSuppliedClient_LeavesClientUsable()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var store = new DynamoDbActivityGroupGrantStore(
			client, AuthOptions(), NullLogger<DynamoDbActivityGroupGrantStore>.Instance);

		store.Dispose();

		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void ActivityGroupGrantStore_WithSelfCreatedClient_DisposesIt()
	{
		var store = new DynamoDbActivityGroupGrantStore(
			AuthOptions(), NullLogger<DynamoDbActivityGroupGrantStore>.Instance);
		var owned = PlantOwnedClient(store, "_client");

		store.Dispose();

		A.CallTo(() => owned.Dispose()).MustHaveHappened();
	}

	// ---------- DynamoDbPersistenceProvider ----------

	[Fact]
	public void PersistenceProvider_WithConsumerSuppliedClient_LeavesClientUsable()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(
			client, ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		provider.Dispose();

		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task PersistenceProvider_WithConsumerSuppliedClient_LeavesClientUsableOnAsyncDispose()
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var provider = new DynamoDbPersistenceProvider(
			client, ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		await provider.DisposeAsync();

		A.CallTo(() => client.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public void PersistenceProvider_WithSelfCreatedClient_DisposesIt()
	{
		var provider = new DynamoDbPersistenceProvider(
			ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);
		var owned = PlantOwnedClient(provider, "_client");

		provider.Dispose();

		A.CallTo(() => owned.Dispose()).MustHaveHappened();
	}

	// ---------- disposal stays idempotent ----------

	[Fact]
	public async Task AllThree_DoubleDispose_DoesNotThrow()
	{
		var grant = new DynamoDbGrantStore(
			A.Fake<IAmazonDynamoDB>(), AuthOptions(), NullLogger<DynamoDbGrantStore>.Instance);
		var groups = new DynamoDbActivityGroupGrantStore(
			A.Fake<IAmazonDynamoDB>(), AuthOptions(), NullLogger<DynamoDbActivityGroupGrantStore>.Instance);
		var provider = new DynamoDbPersistenceProvider(
			A.Fake<IAmazonDynamoDB>(), ProviderOptions(), NullLogger<DynamoDbPersistenceProvider>.Instance);

		grant.Dispose();
		await grant.DisposeAsync();
		groups.Dispose();
		await groups.DisposeAsync();
		provider.Dispose();
		await provider.DisposeAsync();
	}
}
