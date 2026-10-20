// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.Data.SqlServer.Authorization;
using Excalibur.Domain;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.SqlServer.Authorization;

/// <summary>
/// Binds the durability capability of <see cref="SqlServerGrantStore" /> and the startup gate that reads it.
/// </summary>
/// <remarks>
/// The failure these arms prevent is a false negative in a security control, which is the direction that
/// costs most. The store persists grants to SQL Server and advances the user revision inside its own
/// commits, but it did not declare <see cref="IDurableGrantStore" />, so the startup durability gate asked
/// it whether it was durable and was told no. A host wiring the documented SQL Server path therefore
/// refused to start, and the only remedy the gate's own message offered was to set
/// <c>AllowVolatileGrantStore</c> — switching the control off for the entire host. A correct consumer was
/// funnelled into disabling a security gate because a durable provider under-reported itself.
///
/// The sibling file asserts registration — that a <c>ServiceDescriptor</c> for the store exists. That is
/// why this shipped: a descriptor is present either way. These arms resolve the real provider and start it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class SqlServerGrantStoreDurabilityShould
{
	/// <summary>
	/// CAPABILITY: the type declares the durability marker.
	/// </summary>
	/// <remarks>
	/// RED against pre-fix source: the base list was
	/// <c>IGrantStore, IGrantQueryStore, IActivityGroupGrantStore, IActivityGroupGrantReplacement,
	/// IAtomicGrantRevocationStore</c> — identical to the Postgres store's except for this interface.
	/// </remarks>
	[Fact]
	public void Declare_the_durable_grant_store_marker()
	{
		// IsAssignableFrom, not ShouldBeAssignableTo: the latter would ask whether the Type OBJECT
		// implements the marker, which no RuntimeType does, so it fails identically whether or not
		// the store declares the interface -- a red that proves nothing.
		typeof(IDurableGrantStore).IsAssignableFrom(typeof(SqlServerGrantStore)).ShouldBeTrue(
			"SQL Server persists grants and is a Tier 1 provider, so it must answer the durability gate");
	}

	/// <summary>
	/// CAPABILITY: an instance answers the durability probe through <c>GetService</c>.
	/// </summary>
	/// <remarks>
	/// The marker alone is not enough — the gate probes through <see cref="IGrantStore.GetService" />, and
	/// this store answers capability queries from an explicit arm list rather than by reflection, so an
	/// interface added to the base list with no matching arm would still report null.
	/// </remarks>
	[Fact]
	public void Answer_the_durability_probe_through_GetService()
	{
		var store = new SqlServerGrantStore(A.Fake<IDomainDb>());

		store.GetService(typeof(IDurableGrantStore)).ShouldBeAssignableTo<IDurableGrantStore>();
	}

	/// <summary>
	/// LIVENESS: the documented SQL Server host starts.
	/// </summary>
	/// <remarks>
	/// This is the arm that reproduces the reported failure. It resolves the options the gate validates,
	/// which is what <c>ValidateOnStart</c> does at host start-up. RED against pre-fix source with an
	/// <see cref="OptionsValidationException" />.
	/// </remarks>
	[Fact]
	public void Start_a_host_wired_to_the_sql_server_grant_store()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDomainDb>());
		_ = services.AddExcaliburA3().UseSqlServer();

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(
			() => provider.GetRequiredService<IOptions<GrantDurabilityOptions>>().Value,
			"a durable Tier 1 provider must satisfy the durability gate without AllowVolatileGrantStore");
	}

	/// <summary>
	/// SAFETY: the gate still refuses a store that is genuinely volatile.
	/// </summary>
	/// <remarks>
	/// Paired with the liveness arm above and not optional. Without it, the cheapest way to make that arm
	/// pass is a gate that accepts everything, and the suite would be satisfied by deleting the control
	/// this file exists to defend.
	/// </remarks>
	[Fact]
	public void Still_refuse_a_volatile_grant_store()
	{
		// A store that answers no capability probe, which is exactly what a volatile one does.
		var volatileStore = A.Fake<IGrantStore>();
		A.CallTo(() => volatileStore.GetService(typeof(IDurableGrantStore))).Returns(null);

		var services = new ServiceCollection();
		_ = services.AddExcaliburA3();
		services.AddSingleton(volatileStore);

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<GrantDurabilityOptions>>().Value,
			"a store that does not answer the durability probe must still fail start-up");
	}
}
