// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.CosmosDb;
using Excalibur.Outbox.DynamoDb;
using Excalibur.Outbox.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Author-independent lock on the tenant gate for the <em>change-feed</em> outbox contract,
/// <see cref="ICloudNativeOutboxStore"/> — the contract Cosmos DB, DynamoDB and Firestore actually register.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks, and why it is the dangerous kind.</b> The outbox gate keys on
/// <see cref="IOutboxStore"/>. These three providers do not implement that interface and never register it —
/// they register <see cref="ICloudNativeOutboxStore"/>, a standalone contract that derives from nothing the
/// gate had heard of. So the gate did not reject them: <b>it never fired at all</b>. A host selecting
/// row-discriminator multi-tenancy on any of these three started cleanly, reported success, and ran an outbox
/// no capability check covered. That is the silent half of this defect class. A refused host is loud and
/// someone files a bug the same morning; an ungated one ships.
/// </para>
/// <para>
/// <b>Why the partitioned seam and not the scoped one.</b> None of these three stores reads an ambient tenant
/// on any path — the packages contain no reference to <see cref="ITenantContext"/> at all. Each persists the
/// tenant on the row it writes and hands that value back in <see cref="CloudOutboxMessage.TenantId"/> when the
/// change feed reads it, so the owning tenant is re-established from the row. That is exactly what
/// <see cref="ITenantPartitionedCapability{TContract}"/> attests. A store confining these reads to an ambient
/// tenant would read it as absent on the trigger path, return the empty set, and stall publication for every
/// tenant — while passing any arm that only checks one tenant cannot see another tenant's rows.
/// </para>
/// <para>
/// <b>Both arms, and neither is redundant here.</b> The safety arms prove the gate now fires on this contract;
/// they are red before the fix, because before it nothing threw for any cloud-native registration. The
/// liveness arms prove the gate <em>admits</em> a correctly wired host — and they are the arms that would
/// catch this fix being done the wrong way. Marking a contract tenant-owned without giving its providers a way
/// to attest converts a silent leak into a startup refusal no consumer can satisfy, which is a different bug,
/// not a fix. The liveness arms are green before this change too, for the wrong reason: nothing was gating
/// them. That is stated rather than hidden, because it is the precise sense in which a liveness-only suite was
/// structurally incapable of seeing this defect.
/// </para>
/// <para>
/// <b>The registration shape is load-bearing.</b> These providers register the contract with a plain,
/// un-keyed <c>TryAddSingleton</c>, through no tenant-aware seam. The gate's coverage sweep enumerates service
/// descriptors and reads the attribute off the contract, so it must reach a self-registered contract with no
/// special seam involved. <see cref="RefuseAPlainSelfRegisteredCloudNativeOutbox_ThatAttestsNothing"/> asserts
/// exactly that shape rather than a convenient one, and a keyed arm covers the other shape a provider might
/// adopt.
/// </para>
/// <para>
/// <b>Real container, production path.</b> Every liveness arm wires the host through <c>AddExcalibur</c> to
/// <c>AddOutbox</c> to the provider's own <c>Use*</c> method and resolves from a real
/// <see cref="ServiceProvider"/>. A lock that registered the marker itself would prove only that the gate
/// reads a marker it was handed. No cloud infrastructure is required: each store captures its options and
/// logger and validates them without connecting.
/// </para>
/// <para>
/// <b>What these arms do not prove.</b> That the writes actually populate the discriminator, or that a read
/// serves one tenant and not another. That is observable only against real infrastructure and is held by the
/// conformance round-trip, not by a registration-time marker.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CloudNativeOutboxTenantAttestationShould
{
	/// <summary>Never opened: the Cosmos store validates this and captures it without connecting.</summary>
	private const string UnusedCosmosConnectionString =
		"AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

	/// <summary>Never opened: the DynamoDB store captures the endpoint without connecting.</summary>
	private const string UnusedDynamoDbServiceUrl = "http://localhost:8000";

	/// <summary>Never contacted: the Firestore store captures the project id without connecting.</summary>
	private const string UnusedFirestoreProjectId = "test-project";

	// ---- LIVENESS: the gate ADMITS each correctly wired cloud-native provider ---------------------------

	[Theory]
	[InlineData(CloudNativeProvider.CosmosDb)]
	[InlineData(CloudNativeProvider.DynamoDb)]
	[InlineData(CloudNativeProvider.Firestore)]
	public void AdmitACorrectlyWiredCloudNativeOutbox_UnderRowDiscriminator(CloudNativeProvider provider)
	{
		var services = BuildCloudNativeOutboxHost(provider);

		Should.NotThrow(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			$"RowDiscriminator must ADMIT a correctly wired {provider} outbox. This store carries the tenant "
			+ "on the row and re-establishes it when the change feed reads the row back, which is the "
			+ "mechanism ITenantPartitionedCapability attests. Marking the contract tenant-owned without "
			+ "giving its providers a way to attest would convert a silent leak into a startup refusal no "
			+ "consumer could satisfy — a different defect, not a fix.");
	}

	[Theory]
	[InlineData(CloudNativeProvider.CosmosDb)]
	[InlineData(CloudNativeProvider.DynamoDb)]
	[InlineData(CloudNativeProvider.Firestore)]
	public void AttestRowPartitionedTenancy_ForEachCloudNativeProvider(CloudNativeProvider provider)
	{
		using var serviceProvider = BuildCloudNativeOutboxHost(provider).BuildServiceProvider();

		_ = serviceProvider.GetRequiredService<ITenantPartitionedCapability<ICloudNativeOutboxStore>>()
			.ShouldNotBeNull(
				$"The {provider} outbox must present ITenantPartitionedCapability<ICloudNativeOutboxStore>, "
				+ "emitted by AddTenantAwareStore inseparably from the store registration. A marker "
				+ "registered on its own would be the lying-marker defect: the gate passes and the store it "
				+ "claims to attest was never wired.");
	}

	[Theory]
	[InlineData(CloudNativeProvider.CosmosDb, typeof(CosmosDbOutboxStore))]
	[InlineData(CloudNativeProvider.DynamoDb, typeof(DynamoDbOutboxStore))]
	[InlineData(CloudNativeProvider.Firestore, typeof(FirestoreOutboxStore))]
	public async Task ResolveTheCloudNativeOutboxUndecorated_ThroughTheProductionRegistrationPath(
		CloudNativeProvider provider,
		Type expectedStoreType)
	{
		// Disposed asynchronously: this arm actually constructs the store, and these stores are
		// IAsyncDisposable, so a synchronous container dispose throws.
		await using var serviceProvider = BuildCloudNativeOutboxHost(provider).BuildServiceProvider();

		// Resolved through the real container rather than asserted from a descriptor: a registration that
		// cannot be constructed satisfies a descriptor scan and still fails the consumer at runtime. This is
		// the arm that would catch the attestation being emitted for a store the factory never wires.
		var store = serviceProvider.GetRequiredService<ICloudNativeOutboxStore>();

		store.ShouldBeOfType(
			expectedStoreType,
			"The cloud-native outbox must resolve as the provider's own store. A tenant-scoping wrapper on "
			+ "this contract would read the ambient tenant as absent on the change-feed path, claim the empty "
			+ "set, and stall publication for every tenant while looking safe.");
	}

	// ---- SAFETY: the gate now FIRES on this contract, in the shape the providers actually use -----------

	[Fact]
	public void RefuseAPlainSelfRegisteredCloudNativeOutbox_ThatAttestsNothing()
	{
		var services = new ServiceCollection();

		// The exact production shape: a plain, un-keyed TryAddSingleton of the contract, registered through
		// no tenant-aware seam. If the coverage sweep did not reach a descriptor like this one, the gate
		// would silently never fire for Cosmos, DynamoDB or Firestore, and every liveness arm above would
		// pass for the wrong reason — which is precisely the state this bead reports.
		services.TryAddSingleton(A.Fake<ICloudNativeOutboxStore>());

		var thrown = Should.Throw<InvalidOperationException>(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			"RowDiscriminator must REFUSE a cloud-native outbox that proves no tenant capability. Before this "
			+ "fix it did not: the contract carried no tenant-owned declaration, the sweep skipped it, and the "
			+ "host started with an unconfined outbox and no error.");

		thrown.Message.ShouldContain(
			nameof(ICloudNativeOutboxStore),
			Case.Sensitive,
			"The refusal must name the contract that failed. A consumer cannot act on 'some store is "
			+ "unscoped' when the container holds a hundred registrations, and naming IOutboxStore here "
			+ "would send them to a contract this provider does not implement.");
	}

	[Fact]
	public void RefuseAKeyedCloudNativeOutbox_ThatAttestsNothing()
	{
		var services = new ServiceCollection();

		// The other registration shape a provider might adopt. Covered so that a provider moving to keyed
		// registration cannot silently fall out of the gate's reach.
		_ = services.AddKeyedSingleton(
			"cosmosdb",
			(IServiceProvider _, object? _) => A.Fake<ICloudNativeOutboxStore>());

		var thrown = Should.Throw<InvalidOperationException>(
			() => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
			"RowDiscriminator must reject a KEYED cloud-native outbox that proves no tenant capability. If "
			+ "the gate does not see keyed descriptors, a provider switching to keyed registration silently "
			+ "leaves the gate's reach.");

		thrown.Message.ShouldContain(
			nameof(ICloudNativeOutboxStore),
			Case.Sensitive,
			"The refusal must name the contract that failed.");
	}

	// ---- SAFETY: each provider attests the mechanism it has, and not the one it does not ----------------

	[Theory]
	[InlineData(CloudNativeProvider.CosmosDb)]
	[InlineData(CloudNativeProvider.DynamoDb)]
	[InlineData(CloudNativeProvider.Firestore)]
	public void NotAttestAmbientTenantScoping_ForAnyCloudNativeProvider(CloudNativeProvider provider)
	{
		using var serviceProvider = BuildCloudNativeOutboxHost(provider).BuildServiceProvider();

		serviceProvider.GetService<ITenantScopingCapability<ICloudNativeOutboxStore>>().ShouldBeNull(
			$"The {provider} outbox must not present ITenantScopingCapability<ICloudNativeOutboxStore>. That "
			+ "marker attests that the store applies the ambient tenant discriminator to every operation, and "
			+ "this store reads no ambient tenant on any path. Presenting it would satisfy the gate while the "
			+ "documentation described a confinement that does not exist.");
	}

	/// <summary>The three change-feed outbox providers, each registering <see cref="ICloudNativeOutboxStore"/>.</summary>
	public enum CloudNativeProvider
	{
		/// <summary>Azure Cosmos DB; change feed to an Azure Function.</summary>
		CosmosDb,

		/// <summary>Amazon DynamoDB; streams to a Lambda.</summary>
		DynamoDb,

		/// <summary>Google Cloud Firestore; trigger to a Cloud Function.</summary>
		Firestore,
	}
	/// <summary>Wires a host through the given provider's production outbox registration path.</summary>
	private static ServiceCollection BuildCloudNativeOutboxHost(CloudNativeProvider provider)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddOutbox(outbox =>
		{
			switch (provider)
			{
				case CloudNativeProvider.CosmosDb:
					_ = outbox.UseCosmosDb(static cosmos => cosmos
						.ConnectionString(UnusedCosmosConnectionString)
						.DatabaseName("outbox_unused"));
					break;

				case CloudNativeProvider.DynamoDb:
					_ = outbox.UseDynamoDb(static dynamo => dynamo.ServiceUrl(UnusedDynamoDbServiceUrl));
					break;

				default:
					_ = outbox.UseFirestore(static firestore =>
						firestore.ProjectId(UnusedFirestoreProjectId));
					break;
			}
		}));

		return services;
	}
}
