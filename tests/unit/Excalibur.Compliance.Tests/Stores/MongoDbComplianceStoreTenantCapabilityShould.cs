// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Stores.MongoDb;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Compliance.Tests.Stores;

/// <summary>
/// Binds the MongoDB compliance store's registration to the dep-gated tenant-aware seam, so the store can
/// attest the tenant scoping it actually performs.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> The store partitions every document it writes by tenant — the tenant
/// is part of the upsert key, which is what stops two tenants recording data for the same subject from
/// collapsing onto one document. But it was registered plainly, with
/// <c>TryAddSingleton&lt;IComplianceStore, MongoDbComplianceStore&gt;</c>, and a plain registration emits no
/// capability marker. A host that composed this store with multi-tenancy was therefore refused by the
/// startup gate for a store that does in fact scope by tenant: the store was correct and unusable, and the
/// obvious workaround — waiving the requirement — would have waived it for genuinely unscoped stores too.
/// The Postgres sibling has always gone through the seam; only MongoDB was left out.
/// </para>
/// <para>
/// <b>Why the marker cannot be registered on its own.</b> The seam derives the mechanism from the store's
/// own constructor and emits the marker in the same act as the wiring, so a store that was never handed a
/// tenant context cannot carry a truthful-looking attestation. That is why the constructor parameter is
/// required rather than optional: an optional one lets a store be built with nothing and still look wired.
/// The last arm holds that requirement in place.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> "A marker is present" is satisfied by a registration that emits the
/// marker and no longer produces a usable store, or that produces a second, unwired instance alongside the
/// attested one. The liveness arms assert the store still resolves, and that the contract and the concrete
/// type resolve to the <em>same</em> instance — the one the marker attests.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbComplianceStoreTenantCapabilityShould
{
	// Never contacted: no arm here issues a request. It only has to parse.
	private const string ConnectionString = "mongodb://localhost:27017";

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMongoDbComplianceStore(o =>
		{
			o.ConnectionString = ConnectionString;
			o.DatabaseName = "compliance";
		});

		return services.BuildServiceProvider();
	}

	[Fact]
	public void AdvertiseTenantScopingForTheStoreItRegisters()
	{
		// SAFETY. Without this marker the multi-tenancy gate fails closed on a store that scopes correctly,
		// so the only compositions left to a consumer are "no MongoDB compliance store" or "no gate".
		using var provider = BuildProvider();

		provider.GetService<ITenantScopingCapability<IComplianceStore>>().ShouldNotBeNull(
			"the store partitions every document by tenant, and the multi-tenancy gate refuses any store "
			+ "that does not attest it. Registering it plainly emits no attestation, so a correct store is "
			+ "rejected at startup.");
	}

	[Fact]
	public void StillResolveTheStoreItAdvertises()
	{
		// LIVENESS. A registration that emitted the marker and stopped producing a store would satisfy the
		// arm above while leaving IComplianceStore unresolvable.
		using var provider = BuildProvider();

		var store = provider.GetRequiredService<IComplianceStore>();

		_ = store.ShouldBeOfType<MongoDbComplianceStore>(
			"emitting a capability marker must not come at the cost of the registration it attests.");
	}

	[Fact]
	public void ResolveTheContractAndTheConcreteStoreToOneInstance()
	{
		// LIVENESS. The seam registers the CONCRETE type, because that is the instance the marker is bound
		// to. Mapping the contract to a second construction would leave the attested instance unused and the
		// used instance unattested -- the marker would be true of something nobody resolves.
		using var provider = BuildProvider();

		var byContract = provider.GetRequiredService<IComplianceStore>();
		var byConcreteType = provider.GetRequiredService<MongoDbComplianceStore>();

		byContract.ShouldBeSameAs(
			byConcreteType,
			"the marker attests the instance the seam built. If the contract forwards to a different one, "
			+ "the attestation describes an object the application never uses.");
	}

	[Fact]
	public void RefuseConstructionWithoutATenantContext()
	{
		// LIVENESS for the requirement the attestation rests on. If the parameter were optional again, a
		// store could be built having been handed nothing, silently widen to the untenanted partition, and
		// still be registered through the seam -- the marker would then attest a scoping that is not
		// happening, which is worse than the missing marker this change fixes.
		var options = MsOptions.Create(new MongoDbComplianceOptions { ConnectionString = ConnectionString });

		_ = Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(
				options,
				null!,
				NullLogger<MongoDbComplianceStore>.Instance),
			"a store that accepts no tenant context cannot honestly attest that it scopes by tenant, "
			+ "because nothing downstream can tell an unwired store from a deliberately untenanted one.");
	}
}
