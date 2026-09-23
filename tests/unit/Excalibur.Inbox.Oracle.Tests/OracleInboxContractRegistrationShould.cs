// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// <c>AddOracleInboxStore</c> registers the contract it is named for, not only keyed views of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>ORACLE'S SHARE OF A POPULATION, NOT A LONE BUG.</b> Every standalone store entry point builds its
/// store through <c>AddTenantAwareStore&lt;TContract, TStore&gt;</c>, whose registration infers its
/// service type from the FACTORY's return type — the concrete store, never <c>TContract</c>. Each
/// extension then added keyed views by hand and none added the unkeyed one, so a host calling only
/// <c>AddOracleInboxStore(...)</c> got a store it could not inject as <see cref="IInboxStore"/>. The
/// failure lands at startup, after wiring, where no documentation reaches the consumer.
/// </para>
/// <para>
/// <b>WHY THIS ARM LIVES HERE AND NOT WITH ITS SIBLINGS.</b> The other four standalone entry points are
/// bound together in <c>Excalibur.Integration.Tests</c>, which does not reference this package. Split so
/// that Oracle is covered rather than quietly excluded from a sweep that looks complete — the original
/// defect was itself an under-enumerated population, and a lock that repeats the omission would be a poor
/// guard against it.
/// </para>
/// <para>
/// <b>NO INFRASTRUCTURE IS TOUCHED.</b> These arms assert the SHAPE OF THE REGISTRATION and never build a
/// provider or open a connection, so no Oracle instance is required. Deliberate: binding a registration
/// defect to real infrastructure makes the lock skip in exactly the environments that most need it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class OracleInboxContractRegistrationShould
{
	private static ServiceCollection Registered()
	{
		var services = new ServiceCollection();
		_ = services.AddOracleInboxStore(o => o.ConnectionString = "user id=u;password=p;data source=d");

		return services;
	}

	/// <summary>
	/// SAFETY. The contract the method is named for must be resolvable unkeyed.
	/// </summary>
	[Fact]
	public void RegisterTheNonKeyedInboxStoreContract() =>
		Registered().Count(d => d.ServiceType == typeof(IInboxStore) && !d.IsKeyedService).ShouldBe(
			1,
			"AddOracleInboxStore must register IInboxStore unkeyed. Zero means a host that calls only this "
			+ "extension cannot inject the contract the method is named for, and meets that at startup "
			+ "rather than at wiring");

	/// <summary>
	/// SAFETY. TryAdd semantics — a second call must not shadow or duplicate the alias.
	/// </summary>
	[Fact]
	public void RegisterTheNonKeyedContractExactlyOnce_WhenCalledTwice()
	{
		var services = Registered();
		_ = services.AddOracleInboxStore(o => o.ConnectionString = "user id=u;password=p;data source=d");

		services.Count(d => d.ServiceType == typeof(IInboxStore) && !d.IsKeyedService).ShouldBe(
			1,
			"the alias must use TryAdd, so a second call leaves one descriptor rather than shadowing the first");
	}

	/// <summary>
	/// LIVENESS, and without it the arms above are satisfied by an extension that registers the alias and
	/// nothing else. The keyed view the alias FORWARDS to must survive.
	/// </summary>
	[Fact]
	public void StillRegisterTheKeyedDefaultView() =>
		Registered()
			.Count(d => d.ServiceType == typeof(IInboxStore)
				&& d.IsKeyedService
				&& (d.ServiceKey as string) == "default")
			.ShouldBe(
				1,
				"the keyed \"default\" registration must remain — the alias resolves THROUGH it, so removing "
				+ "it would replace one resolution failure with another");
}
