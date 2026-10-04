// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Asserts that each public store registration leaves its CONTRACT resolvable, not merely its concrete
/// type.
/// </summary>
/// <remarks>
/// <para>
/// <b>The class this guards.</b> <c>AddTenantAwareStore&lt;TContract, TStore&gt;</c> registers the concrete
/// store and the tenant-capability marker — and NOT the contract, because the lambda it uses infers the
/// concrete type from its factory. Every one of its roughly one hundred call sites must therefore register
/// the contract itself, in a second statement, and the omission is silent: the registration call succeeds,
/// the marker is present, and resolving the interface returns <see langword="null"/> until something else
/// happens to register it.
/// </para>
/// <para>
/// <b>Why the fix is a test rather than a change to the helper.</b> Registering the contract inside the
/// helper was tried and reverted: it runs before the caller's own statements, so it wins the <c>TryAdd</c>
/// race against alias and decorator registrations that come after — the outbox's telemetry decorator was
/// silently bypassed, which an existing arm caught. The helper cannot know whether a decorator is coming,
/// so resolution order must stay with the caller and the omission has to be caught here instead.
/// </para>
/// <para>
/// Each arm resolves through a real provider. A connection string that goes nowhere is fine: these
/// registrations are factory-based, so nothing opens a connection until an operation runs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class StoreContractsResolveShould
{
	private const string Unused = "Server=unused;Database=unused;";

	private static ServiceProvider Build(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		register(services);
		return services.BuildServiceProvider();
	}

	/// <summary>
	/// The SQL Server snapshot store is reached BY KEY, and this arm asserts that rather than non-keyed
	/// resolution — which it originally asserted, wrongly.
	/// </summary>
	/// <remarks>
	/// <b>Corrected after reading the design instead of assuming it.</b> This arm first asserted that
	/// non-keyed <c>ISnapshotStore</c> resolves, and it does not: the provider registers the contract under
	/// the keys <c>"sqlserver"</c> and <c>"default"</c>, with the telemetry-wrapped instance behind them,
	/// exactly as the event store does. Non-keyed absence is a deliberate consequence of that pattern, not
	/// an omission, and asserting otherwise would have driven a "fix" that bypassed the telemetry wrapper —
	/// the same mistake an outbox arm caught elsewhere. What a consumer needs is the store it asked for
	/// under the key the design uses, which is what this now binds.
	/// </remarks>
	[Fact]
	public void ResolveISnapshotStoreByKeyAfterAddSqlServerSnapshotStore()
	{
		using var provider = Build(s => s.AddSqlServerSnapshotStore(() => new SqlConnection(Unused)));

		provider.GetRequiredKeyedService<ISnapshotStore>("sqlserver").ShouldNotBeNull();
		provider.GetRequiredKeyedService<ISnapshotStore>("default").ShouldNotBeNull(
			"the default key must forward, so a host that does not name a provider still gets one");
	}

	[Fact]
	public void ResolveICursorMapStoreAfterAddSqlServerCursorMapStore()
	{
		using var provider = Build(s => s.AddSqlServerCursorMapStore(Unused));

		provider.GetService<ICursorMapStore>().ShouldNotBeNull();
	}

	[Fact]
	public void ResolveISubscriptionCheckpointStoreAfterItsRegistration()
	{
		using var provider = Build(
			s => s.AddSqlServerSubscriptionCheckpointStore(() => new SqlConnection(Unused)));

		provider.GetService<ISubscriptionCheckpointStore>().ShouldNotBeNull();
	}
}
