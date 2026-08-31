// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.MultiTenancy;

/// <summary>
/// Refuses to start the host when any registered contract that declares
/// <see cref="TenantOwnedAttribute"/> presents neither <see cref="ITenantScopingCapability{TContract}"/>
/// nor <see cref="ITenantPartitionedCapability{TContract}"/>, for every tenant-isolation strategy.
/// </summary>
/// <remarks>
/// <para>
/// This is the order-independent half of a two-part gate. The composition-time sweep asks the same
/// question of the same registrations, but it asks it at the instant <c>AddMultiTenancy</c> runs, and a
/// service collection is a mutable list: a store registered <em>after</em> that call is never seen, the
/// assertion never evaluates for it, and startup succeeds into the unconfined configuration. Reversing
/// two registration calls must not change whether a safety gate runs. Evaluating the assertion once the
/// collection is complete removes that degree of freedom by construction.
/// </para>
/// <para>
/// It is registered for every multi-tenant host rather than for one isolation strategy. Sharding does not
/// exempt a store: sharding confines tenants only while the tenant-to-shard mapping is injective, and the
/// mapping is consumer configuration that no startup check can establish, so a store whose reads are not
/// confined by some mechanism of its own is no safer there than under row discrimination.
/// </para>
/// <para>
/// The predicate is the same one the composition-time sweep uses, not a second copy of the rule. It
/// derives coverage from the registration and from each contract's own declaration, so a tenant-owned
/// contract added to the framework, or declared by a consumer, is covered the moment it is declared -
/// there is no list here to forget to update.
/// </para>
/// <para>
/// The descriptor list is read, never resolved. This guard is a singleton holding the root provider,
/// where resolving a scoped store would throw or root a captive, and inspecting registration also avoids
/// opening a provider connection as a side effect of validating configuration. Reading the descriptors
/// rather than probing <see cref="IServiceProviderIsService"/> is deliberate and load-bearing: that probe
/// answers for non-keyed registrations only, and the cloud-native providers register their terminal store
/// under a service key, so a probe-based form of this guard would report a pass over precisely the
/// registrations it exists to refuse.
/// </para>
/// </remarks>
// DO NOT convert this to a BackgroundService. IHostedService.StartAsync throwing REFUSES THE START,
// deterministically and before traffic; BackgroundService.ExecuteAsync throwing is governed by
// HostOptions.BackgroundServiceExceptionBehavior and runs asynchronously to startup, so the host may
// already be serving requests before the gate fires. That change would be invisible in the signature.
internal sealed class TenantOwnedCapabilityStartupValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceCollection _services;
	private readonly TenantIsolationStrategy _strategy;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantOwnedCapabilityStartupValidator"/> class.
	/// </summary>
	/// <param name="services">
	/// The collection the host was composed from. Held rather than snapshotted: the assertion must see
	/// every registration, including those made after <c>AddMultiTenancy</c>.
	/// </param>
	/// <param name="strategy">
	/// The isolation strategy the host selected. The per-contract requirements replayed below are properties
	/// of row discrimination, so replaying them under a different strategy would refuse configurations that
	/// composition time never refused -- which is a widening, not an order-independence fix.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public TenantOwnedCapabilityStartupValidator(IServiceCollection services, TenantIsolationStrategy strategy)
	{
		ArgumentNullException.ThrowIfNull(services);

		_services = services;
		_strategy = strategy;
	}

	/// <summary>
	/// Asserts every registered tenant-owned contract presents a tenant capability before the host starts.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the start should be canceled.</param>
	/// <returns>A completed task when the configuration is permitted.</returns>
	/// <exception cref="InvalidOperationException">
	/// A registered tenant-owned contract presents neither tenant capability.
	/// </exception>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Validate()
	{
		// The specific per-contract requirements, replayed against the completed collection. The attribute
		// sweep below is a FLOOR that accepts either capability, and for at least one contract the two are not
		// interchangeable -- an outbox attesting ambient scoping is claiming a mechanism that would be a defect
		// if it were real -- so the floor alone cannot carry every contract into order-independence. This is
		// the same list the composition-time pass evaluates, not a second copy of it.
		if (_strategy == TenantIsolationStrategy.RowDiscriminator)
		{
			_ = MultiTenancyServiceCollectionExtensions.AssertPerContractCapabilities(_services);
		}

		_ = MultiTenancyServiceCollectionExtensions.RequireEveryTenantOwnedContractPresentsACapability(_services);
	}

	/// <summary>
	/// Does nothing; the guard holds no resources.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the shutdown should no longer be graceful.</param>
	/// <returns>A completed task.</returns>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
