// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.DynamoDb;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.MultiTenancy;

/// <summary>
/// Locks the tenant-capability requirement over <see cref="ICloudNativeEventStore"/>: a document-database
/// event store registered under that contract must reach no started multi-tenant host unless its provider
/// attests a tenancy mechanism -- under every isolation strategy, and in every order the registration calls
/// can legally be written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why order is the whole point.</b> A service collection is a mutable list, so a sweep performed at the
/// instant multi-tenancy is composed sees only what was registered before it. Reversing two registration
/// calls is not a defect a consumer can be expected to avoid, and it must not change whether a safety gate
/// runs. The refusal arms below therefore come in a matched pair -- the same unattested store, registered
/// once before and once after -- because a gate that fires in only one of those orders is a gate the next
/// consumer walks around by writing their composition root in the other one.
/// </para>
/// <para>
/// <b>Liveness is not decoration here.</b> A gate asserted only through refusals is satisfied perfectly by
/// one that refuses every startup, so each refusal arm is paired with a permitted case differing in exactly
/// one thing: whether the provider attested. The sharding pair is load-bearing beyond that -- the routing
/// store's own attestation is what keeps the strategy usable at all, and the arms would catch its removal.
/// </para>
/// <para>
/// <b>What makes these arms non-vacuous.</b> Every arm arrives through the shipped registration extensions a
/// consumer calls, resolves through a real container, and starts the registered <see cref="IHostedService"/>
/// set. No arm names the guard type. A guard written correctly but never registered, registered only under
/// one strategy, or evaluated before the collection is complete cannot throw from here -- which is the
/// property being locked, stated as a property rather than as a mechanism.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudNativeEventStoreTenantCapabilityGateShould
{
	[Fact]
	public async Task RefuseToStart_WhenTheDocumentStoreIsRegisteredAfterMultiTenancy()
	{
		// The ordering hole. Legal, ordinary, and it used to start: multi-tenancy composes over a collection
		// that does not yet hold the provider, and the provider arrives a moment later carrying a store that
		// composes its document keys without the tenant.
		var services = new ServiceCollection();
		AddAttestingPrimaryEventStore(services);

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		AddUnattestedDocumentEventStore(services);

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

		// BOTH names, deliberately, and this is the arm that rules on the question the implementer raised.
		//
		// The refusal must LEAD with the contract: the requirement belongs to ICloudNativeEventStore, and
		// which capability satisfies it is a property of that contract, not of whichever class happens to
		// implement it. A consumer told only "DynamoDbEventStore" has a class name and no way to know what
		// to satisfy.
		//
		// It must ALSO name the registration: the contract is not a line anyone can edit. The provider puts
		// its concrete type in the collection, so "ICloudNativeEventStore is unattested" leaves a consumer
		// holding a hundred registrations to work out which of them produced it.
		//
		// Asserting only the first -- as a lock could -- leaves the second half deletable with the lock still
		// green, and an unactionable error is a gate a consumer routes around rather than satisfies.
		ex.Message.ShouldContain(nameof(ICloudNativeEventStore));
		ex.Message.ShouldContain(nameof(UnconfinedCloudEventStore));
	}

	[Fact]
	public async Task RefuseToStart_WhenTheDocumentStoreIsRegisteredBeforeMultiTenancy()
	{
		// The other half of the ordering pair. Without it, a repair that moved the assertion late rather than
		// making it order-independent would pass every other arm in this file while silently dropping the
		// order it used to cover. The pair asserts independence; either arm alone asserts only a direction.
		//
		// Measured: this order is refused at the EARLIER of the two sites, because the offending registration
		// is already present when multi-tenancy composes. That is a stronger outcome than the arm demands, and
		// it is why the assertion is written around the whole composition rather than around host start.
		var ex = await ShouldRefuseAsync(static services =>
		{
			AddAttestingPrimaryEventStore(services);

			AddUnattestedDocumentEventStore(services);

			_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		});

		ex.Message.ShouldContain(nameof(ICloudNativeEventStore));
	}

	[Fact]
	public async Task RefuseToStart_UnderShardingIsolation()
	{
		// Sharding does not exempt a store. It confines tenants only while the tenant-to-shard mapping is
		// injective, which is consumer configuration no startup check can establish -- so an unconfined
		// document store is no safer here than under row discrimination. Pre-fix this arm started twice
		// over: the contract carried no ownership declaration, and this strategy never enters the
		// composition-time sweep at all.
		var services = new ServiceCollection();

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.Sharding);

		AddUnattestedDocumentEventStore(services);

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

		ex.Message.ShouldContain(nameof(ICloudNativeEventStore));
	}

	[Fact]
	public async Task RefuseToStart_WhenShardingIsEnabledOnTheBuilderAfterMultiTenancyComposed()
	{
		// The third ordering, and the one with a moving part the other two do not exercise. Enabling sharding
		// on the builder rewrites the event-store wiring in place: it deletes every IEventStore descriptor and
		// every IEventStore capability marker, then re-registers the routing store with its own. That deletion
		// pass is scoped to IEventStore and leaves the document contract untouched, so the unattested
		// ICloudNativeEventStore survives a rewrite that looks, from the IEventStore side, like the store was
		// replaced with an attested one.
		//
		// The document store is registered AFTER multi-tenancy deliberately. Registering it before would be
		// caught by the composition-time sweep and this arm would pass without ever reaching the rewrite it
		// exists to cover -- a green for the wrong reason.
		var ex = await ShouldRefuseAsync(static services =>
		{
			AddAttestingPrimaryEventStore(services);

			_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

			AddUnattestedDocumentEventStore(services);

			_ = new ExcaliburEventSourcingBuilder(services)
				.EnableTenantSharding(static o => o.DefaultShardId = "shard-default");
		});

		ex.Message.ShouldContain(nameof(ICloudNativeEventStore));
	}

	[Fact]
	public async Task RefuseAStoreTheHostCouldOtherwiseResolveAndUse()
	{
		// Materiality. The arms above prove a configuration is refused; this one proves the thing being
		// refused is a live store a host would genuinely receive and read through, not a descriptor nobody
		// resolves. Without it, every refusal above is consistent with the gate objecting to a phantom.
		//
		// The document store arrives late so that composition succeeds and there is a built provider to
		// resolve from: resolution happens in exactly the window the gate closes -- the store is
		// constructible and usable, and the refusal is what stops it reaching traffic.
		var services = new ServiceCollection();

		// Logging, because this is the one arm that actually CONSTRUCTS the store rather than reading its
		// descriptor, and the provider's factory resolves a logger the way it does in a real host. Its
		// absence is what a container missing it would report, not anything about tenancy.
		_ = services.AddLogging();
		AddAttestingPrimaryEventStore(services);

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		AddUnattestedDocumentEventStore(services);

		await using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<ICloudNativeEventStore>();
		_ = store.ShouldBeOfType<UnconfinedCloudEventStore>();

		_ = await Should.ThrowAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));
	}

	[Fact]
	public async Task Start_WhenTheDocumentStoreAttests_InTheSameLateOrder()
	{
		// LIVENESS, matched to the first refusal arm on every axis the gate can see: same strategy, same
		// contract, same lateness relative to AddMultiTenancy. The single difference is that this store
		// arrives through the seam that supplies the ambient tenant and emits the matching capability in the
		// same act, so it cannot attest without having been wired.
		//
		// Without this arm, a guard that refused every registration made after AddMultiTenancy would satisfy
		// the refusal arms perfectly while making the ordering it was written to permit unusable.
		var services = new ServiceCollection();
		AddAttestingPrimaryEventStore(services);

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		_ = services.AddTenantAwareStore<ICloudNativeEventStore, TenantScopedCloudEventStore>();

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	[Fact]
	public async Task Start_WhenAShardingHostRegistersOnlyItsTenantRoutingStore()
	{
		// LIVENESS for the sharding strategy, and a lock on the routing store's own attestation.
		//
		// The coupling here is real and was verified by mutation, not by reading: enabling sharding deletes
		// the IEventStore capability markers along with the descriptors they described -- an attestation
		// outliving the store it attested is worse than none -- and re-registers the routing store through
		// the tenant-aware seam so it presents its own. Remove that re-registration and this arm goes red,
		// because the host is then a multi-tenant host whose IEventStore attests nothing at all.
		//
		// That is the arm's value: requiring the capability under sharding is only safe while the strategy's
		// own store satisfies it, and the two halves are edited in different files.
		var services = new ServiceCollection();

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.Sharding);

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	[Fact]
	public async Task Start_WhenShardingIsEnabledAfterMultiTenancyOverAnAttestingStore()
	{
		// LIVENESS matched to the third refusal arm: the same in-place rewrite of the event-store wiring,
		// over a host with nothing unattested left behind. It pins the far side of that deletion pass --
		// the markers are removed with their descriptors, and the routing store must re-attest before the
		// host starts. A deletion pass that removed the markers without re-registering would leave this
		// red while every refusal arm stayed green.
		var services = new ServiceCollection();
		AddAttestingPrimaryEventStore(services);

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		_ = new ExcaliburEventSourcingBuilder(services)
			.EnableTenantSharding(static o => o.DefaultShardId = "shard-default");

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	[Fact]
	public async Task Start_WhenTheShippedDocumentProvidersAttest()
	{
		// The arm that binds the repair rather than the gate. Each shipped document-store provider composes
		// the ambient tenant into its key, and its registration emits the matching capability in the same
		// act -- so a multi-tenant host that registers one starts.
		//
		// Registering under BOTH contracts is what makes this non-trivial: a provider that attested only
		// IEventStore would satisfy the primary gate and still be refused here on ICloudNativeEventStore,
		// leaving its confinement unreachable through the composition a consumer actually writes. That is
		// the state this arm would catch a regression back into, and it is invisible from the IEventStore
		// side.
		//
		// The provider extensions are called AFTER AddMultiTenancy on purpose: the composition-time sweep
		// cannot see a late registration, so a green here means the host-start sweep saw the attestation --
		// the later and stricter of the two sites.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		AddAttestingPrimaryEventStore(services);

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		_ = new ExcaliburEventSourcingBuilder(services)
			.UseDynamoDb(static db => db.ServiceUrl("http://localhost:8000").TableName("events"));

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));

		// Materiality, mirroring the refusal arm: the store the host would receive is the real provider
		// store, constructed through the shipped factory. No network call is made -- the DynamoDB client is
		// constructed lazily against the service URL and nothing here reads from it.
		_ = provider.GetRequiredService<ICloudNativeEventStore>().ShouldBeOfType<DynamoDbEventStore>();
	}

	/// <summary>
	/// Composes the given registration sequence, builds the container, and starts the host, asserting the
	/// configuration is refused.
	/// </summary>
	/// <remarks>
	/// The refusal may be raised at either of two legitimate sites: the composition-time sweep, when the
	/// offending registration is already present when multi-tenancy composes, or host start, when it arrives
	/// afterwards. Both refuse the same configuration before any traffic reaches it, and which one fires is a
	/// function of registration order -- the very thing these arms exist to make irrelevant. Asserting the
	/// property (this configuration does not reach a started host) rather than the site is what lets the
	/// ordering pair mean what it claims; pinning one site would turn a gate that legitimately fires earlier
	/// into a red arm. It does not weaken the pair: a late registration is invisible to the composition-time
	/// sweep, so a composition-time-only gate still leaves the late arm with nothing thrown at all.
	/// </remarks>
	private static async Task<InvalidOperationException> ShouldRefuseAsync(Action<IServiceCollection> compose)
	{
		var services = new ServiceCollection();

		return await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			compose(services);

			await using var provider = services.BuildServiceProvider();

			await StartHostedServicesAsync(provider);
		});
	}

	/// <summary>
	/// Starts every registered hosted service the way the host does. Resolving the set -- rather than naming
	/// the guard -- is what binds these arms to the production registration path.
	/// </summary>
	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(CancellationToken.None);
		}
	}

	/// <summary>
	/// Registers a primary event store through the seam that emits its capability inseparably, so the
	/// document-store arms fail for the document store's reason and never because the primary contract was
	/// itself unattested.
	/// </summary>
	private static void AddAttestingPrimaryEventStore(IServiceCollection services) =>
		_ = services.AddTenantAwareStore<IEventStore, TenantScopedEventStore>();

	/// <summary>
	/// Registers a document-database event store that attests nothing, through a bare registration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This used to be the shipped DynamoDB extension. It cannot be any more: every shipped document-store
	/// provider now composes the ambient tenant into its key and attests it, so using one here would make
	/// each refusal arm assert a property its subject no longer has, and the arms would go red for the
	/// reason the providers were repaired. The property under test is the GATE -- an unattested document
	/// store does not reach a started multi-tenant host -- which outlives any particular provider, so the
	/// subject is a store that genuinely attests nothing rather than one that used to.
	/// </para>
	/// <para>
	/// A consumer's own <see cref="ICloudNativeEventStore"/> is exactly that store, and it is the case that
	/// remains reachable: the shipped providers are covered by
	/// <see cref="Start_WhenTheShippedDocumentProvidersAttest"/>, and a third party implementing the contract
	/// is who this gate now protects. Registered bare, so no capability is emitted alongside it.
	/// </para>
	/// </remarks>
	private static void AddUnattestedDocumentEventStore(IServiceCollection services)
	{
		// The provider registration SHAPE matters, not just the contract: a provider puts its concrete store
		// type in the collection and aliases the contract onto it, which is what lets the refusal name the
		// registration as well as the contract. Registering the contract alone would produce a refusal that
		// names only the interface -- correct, but weaker than the arm asserts.
		services.TryAddSingleton<UnconfinedCloudEventStore>();
		services.TryAddSingleton<ICloudNativeEventStore>(
			static sp => sp.GetRequiredService<UnconfinedCloudEventStore>());
	}

	/// <summary>
	/// A primary event store whose constructor requires the ambient tenant, so the registration seam derives
	/// the scoping mechanism from the store itself and the attestation cannot be registered without it.
	/// </summary>
	private sealed class TenantScopedEventStore(ITenantContext tenantContext) : IEventStore
	{
		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
			long expectedVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult(AppendResult.CreateSuccess(0, null));
	}

	/// <summary>
	/// A document-store implementation with no tenancy mechanism at all: it takes no ambient tenant, so
	/// there is nothing for the registration seam to derive an attestation from. The subject of every
	/// refusal arm.
	/// </summary>
	private sealed class UnconfinedCloudEventStore : ICloudNativeEventStore
	{
		public Task<CloudEventLoadResult> LoadAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey,
			IConsistencyOptions? consistencyOptions, CancellationToken cancellationToken) =>
			Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudEventLoadResult> LoadFromVersionAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey, long fromVersion,
			IConsistencyOptions? consistencyOptions, CancellationToken cancellationToken) =>
			Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudAppendResult> AppendAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey,
			IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken) =>
			Task.FromResult(CloudAppendResult.CreateSuccess(0, 0));
	}

	/// <summary>
	/// A document-store implementation that confines its reads to the ambient tenant, standing in for the
	/// attesting provider the liveness arm needs. Registered through the same seam a provider uses, so its
	/// capability is emitted from the wiring rather than declared beside it.
	/// </summary>
	private sealed class TenantScopedCloudEventStore(ITenantContext tenantContext) : ICloudNativeEventStore
	{
		public Task<CloudEventLoadResult> LoadAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey,
			IConsistencyOptions? consistencyOptions, CancellationToken cancellationToken) =>
			Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudEventLoadResult> LoadFromVersionAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey, long fromVersion,
			IConsistencyOptions? consistencyOptions, CancellationToken cancellationToken) =>
			Task.FromResult(new CloudEventLoadResult([], 0));

		public Task<CloudAppendResult> AppendAsync(
			string aggregateId, string aggregateType, IPartitionKey partitionKey,
			IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken) =>
			Task.FromResult(CloudAppendResult.CreateSuccess(0, 0));
	}
}
