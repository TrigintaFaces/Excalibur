// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Binds the CDC state store to the <see cref="CosmosClient"/> a consumer supplied, rather than one built
/// from the configured connection string regardless.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> The store constructed a client from
/// <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/> unconditionally, so a consumer could not
/// supply a client they had configured. A connection string carries an account key and nothing else, which
/// put three things out of reach: token-credential authentication — forbidden-key-auth is a policy many
/// organisations enforce outright, making the store unusable for them at any price — a custom
/// <c>HttpClientFactory</c> for a proxy, a certificate, or an emulator, and the choice of Gateway mode,
/// serializer, or retry configuration.
/// </para>
/// <para>
/// <b>Why the existing coverage could not catch it.</b> Nothing supplied a client, because nothing could.
/// The conformance suite handed the store a connection string and measured what the store's own client did
/// with it; the property that was wrong had no arm at all.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> A preference that always won would be as wrong as one that never
/// did: a host that configures nothing but a connection string must still get a working store. Asserting
/// only the first arm would be satisfied by an implementation that throws whenever no client is supplied.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbCdcStateStoreClientResolutionShould : UnitTestBase
{
	// Never contacted: every arm here either resolves a store or disposes one, and none issues a request.
	// The key is only shaped like one so the SDK's connection-string parser accepts it.
	private const string ConnectionString =
		"AccountEndpoint=https://cdc-state-store.documents.azure.com:443/;AccountKey=dGVzdA==;";

	// A DIFFERENT endpoint from the connection string above, so "the store used the supplied client" is
	// distinguishable from "the store built one that happens to work the same way". Reference equality is
	// the assertion; the distinct address is what makes a failure legible.
	private const string SuppliedClientConnectionString =
		"AccountEndpoint=https://consumer-registered.documents.azure.com:443/;AccountKey=dGVzdA==;";

	private static CosmosClient? ClientOf(CosmosDbCdcStateStore store) =>
		(CosmosClient?)typeof(CosmosDbCdcStateStore)
			.GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(store);

	private static CosmosDbCdcStateStoreOptions ValidOptions() => new()
	{
		ConnectionString = ConnectionString,
		DatabaseId = "cdc",
		ContainerId = "cdc-state",
	};

	[Fact]
	public void UseTheCosmosClientSuppliedToTheConstructor()
	{
		using var supplied = new CosmosClient(SuppliedClientConnectionString);

		using var store = new CosmosDbCdcStateStore(
			supplied,
			MsOptions.Create(ValidOptions()),
			NullLogger<CosmosDbCdcStateStore>.Instance);

		ClientOf(store).ShouldBeSameAs(
			supplied,
			"a client the caller configured -- for token-credential auth, a custom HttpClientFactory, "
			+ "Gateway mode, or a serializer -- must be the one the store talks through. Building one from "
			+ "the connection string regardless is what made every one of those unreachable.");
	}

	[Fact]
	public void UseTheCosmosClientTheConsumerRegistered()
	{
		using var registered = new CosmosClient(SuppliedClientConnectionString);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(registered);
		_ = services.AddCosmosDbCdcStateStore(o =>
		{
			o.ConnectionString = ConnectionString;
			o.DatabaseId = "cdc";
			o.ContainerId = "cdc-state";
		});

		using var provider = services.BuildServiceProvider();
		var store = (CosmosDbCdcStateStore)provider.GetRequiredService<ICosmosDbCdcStateStore>();

		ClientOf(store).ShouldBeSameAs(
			registered,
			"a deliberately registered CosmosClient must be the one the store uses. Building a client from "
			+ "the configured connection string regardless makes the registration appear to take effect "
			+ "while the store reads and writes somewhere else entirely.");
	}

	[Fact]
	public void StillBuildAClientWhenTheConsumerRegisteredNone()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddCosmosDbCdcStateStore(o =>
		{
			o.ConnectionString = ConnectionString;
			o.DatabaseId = "cdc";
			o.ContainerId = "cdc-state";
		});

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<ICosmosDbCdcStateStore>();

		// The liveness half. Preferring a supplied client must not become a requirement for one --
		// configuring the store by connection string alone is the common shape and stays supported.
		store.ShouldNotBeNull("a host that registers no client must still resolve a working store.");
		ClientOf((CosmosDbCdcStateStore)store).ShouldNotBeNull(
			"the store must always end up with a client to talk through.");
	}

	[Fact]
	public void AcceptOptionsCarryingNoConnectionStringWhenAClientIsRegistered()
	{
		// The scenario the whole change exists for: an organisation that forbids key auth registers a client
		// built from a TokenCredential, so there is no connection string to give and none is needed.
		using var registered = new CosmosClient(SuppliedClientConnectionString);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(registered);
		_ = services.AddCosmosDbCdcStateStore(o =>
		{
			o.DatabaseId = "cdc";
			o.ContainerId = "cdc-state";
		});

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<ICosmosDbCdcStateStore>();

		store.ShouldNotBeNull(
			"demanding a connection string the store will never read would reject the token-credential host "
			+ "this overload exists to serve -- the fix would be half-done, refusing at startup the very "
			+ "configuration it just made reachable.");
		ClientOf((CosmosDbCdcStateStore)store).ShouldBeSameAs(registered);
	}

	[Fact]
	public void StillRejectOptionsCarryingNoConnectionStringWhenNoClientIsRegistered()
	{
		// The safety half of the arm above. Waiving the requirement unconditionally would turn a startup
		// failure with a clear message into a null-endpoint failure at first use.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddCosmosDbCdcStateStore(o =>
		{
			o.DatabaseId = "cdc";
			o.ContainerId = "cdc-state";
		});

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<ICosmosDbCdcStateStore>(),
			"a store with no client and no connection string has nothing to connect to, and must say so at "
			+ "startup rather than at first use.");
	}

	[Fact]
	public void LeaveASuppliedClientUsableAfterTheStoreIsDisposed()
	{
		// Ownership. A supplied client is a shared singleton the host also hands to its other stores, so
		// disposing it here would tear down connections belonging to code that never asked this store for
		// anything -- and the store is disposed first in exactly the hosts most likely to share one.
		using var supplied = new CosmosClient(SuppliedClientConnectionString);

		var store = new CosmosDbCdcStateStore(
			supplied,
			MsOptions.Create(ValidOptions()),
			NullLogger<CosmosDbCdcStateStore>.Instance);

		store.Dispose();

		_ = Should.NotThrow(
			() => supplied.GetDatabase("cdc"),
			"the store disposed a client it did not own. The host still holds this client and its other "
			+ "stores still use it.");
	}

	[Fact]
	public void DisposeTheClientItBuiltItself()
	{
		// The liveness half of ownership: not disposing a supplied client must not become never disposing
		// one, which would leak a connection pool per store the host builds.
		var store = new CosmosDbCdcStateStore(
			MsOptions.Create(ValidOptions()),
			NullLogger<CosmosDbCdcStateStore>.Instance);

		var built = ClientOf(store);
		built.ShouldNotBeNull();

		store.Dispose();

		_ = Should.Throw<ObjectDisposedException>(
			() => built.GetDatabase("cdc"),
			"a client the store built is the store's to dispose, and nothing else holds a reference to it.");
	}
}
