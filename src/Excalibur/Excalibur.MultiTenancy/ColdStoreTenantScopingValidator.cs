// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.MultiTenancy;

/// <summary>
/// Refuses to start the host when a cold event-tier is resolvable but its provider does not present the
/// tenant-scoping capability, under row-discriminator tenant isolation.
/// </summary>
/// <remarks>
/// <para>
/// This guard exists because the equivalent registration-time check cannot hold the property on its own. A
/// predicate over <see cref="IServiceCollection"/> is a statement about one instant in a mutable list, not an
/// invariant over reachable state: registering the cold store <em>after</em> multi-tenancy means the
/// registration-time predicate never sees it, the assertion never evaluates, and startup succeeds into the
/// unsafe configuration. Reversing two registration calls must not change whether a safety gate runs.
/// </para>
/// <para>
/// Evaluating the same assertion against the finished container makes the outcome independent of registration
/// order by construction — there is no ordering of the two calls that reaches a started host with a cold tier
/// whose provider cannot scope tenants. Resolvability is queried through
/// <see cref="IServiceProviderIsService"/> rather than by resolving the store, so the check neither constructs
/// a provider client nor opens a connection as a side effect of validating configuration.
/// </para>
/// <para>
/// What this refuses is a cold tier whose provider does not <em>attest</em> that it scopes tenants. The
/// tenant-scoped event store enforces tenant presence and then delegates without a row predicate, so
/// isolation on the cold leg lives entirely in the cold provider. A provider that does not partition by
/// tenant returns another tenant's archived events on a hot miss, and an archive trim computed from that
/// read deletes hot events that were never written for it.
/// </para>
/// <para>
/// The attestation is deliberately the capability marker and not an inspection of the key the provider
/// builds. A key shape is not observable from here, and a provider that partitions correctly today can stop
/// doing so in a later version without this assembly changing — so the provider declares the guarantee and
/// owns it. A first-party cold provider that does partition by tenant is expected to present the marker; if
/// one partitions but does not present it, the defect is the missing marker, not this guard.
/// </para>
/// </remarks>
// DO NOT "TIDY" THIS INTO A BackgroundService. It implements IHostedService DIRECTLY, deliberately, and the
// two are not interchangeable here even though both are "a hosted service":
//
//   IHostedService.StartAsync throws      -> the host REFUSES TO START. Hard, deterministic, before traffic.
//   BackgroundService.ExecuteAsync throws -> governed by HostOptions.BackgroundServiceExceptionBehavior, and
//                                            it runs ASYNCHRONOUSLY to startup — the host may already be
//                                            serving requests before the gate ever fires.
//
// This gate exists to refuse a configuration outright. Moving it to ExecuteAsync silently converts a
// fail-closed startup gate into a race, and nothing about the type signature would reveal the change.
internal sealed class ColdStoreTenantScopingValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProviderIsService _isService;

	/// <summary>
	/// Initializes a new instance of the <see cref="ColdStoreTenantScopingValidator"/> class.
	/// </summary>
	/// <param name="isService">Resolvability probe over the finished container.</param>
	/// <exception cref="ArgumentNullException"><paramref name="isService"/> is <see langword="null"/>.</exception>
	public ColdStoreTenantScopingValidator(IServiceProviderIsService isService)
	{
		ArgumentNullException.ThrowIfNull(isService);

		_isService = isService;
	}

	/// <summary>
	/// Asserts the cold-tier tenant-scoping capability before the host starts.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the start should be canceled.</param>
	/// <returns>A completed task when the configuration is permitted.</returns>
	/// <exception cref="InvalidOperationException">
	/// A cold event tier is resolvable but its provider does not present
	/// <see cref="ITenantScopingCapability{TContract}"/> for <see cref="IColdEventStore"/>.
	/// </exception>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Validate();
		return Task.CompletedTask;
	}

	public void Validate()
	{
		if (_isService.IsService(typeof(IColdEventStore))
			&& !_isService.IsService(typeof(ITenantScopingCapability<IColdEventStore>)))
		{
			throw new InvalidOperationException(
				$"{nameof(IColdEventStore)} is registered but its provider is not tenant-scoping-capable; "
				+ $"{nameof(TenantIsolationStrategy.RowDiscriminator)} requires a tenant-aware provider. "
				+ "Tenant isolation on the archived (cold) tier lives entirely in the cold provider, because "
				+ "the tenant-scoped event store enforces tenant presence and then delegates without a row "
				+ "predicate. Register a cold-tier provider that partitions archived events by tenant and "
				+ "presents that capability, or do not enable tiered storage under this isolation strategy.");
		}
	}

	/// <summary>
	/// Does nothing; the guard holds no resources.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the shutdown should no longer be graceful.</param>
	/// <returns>A completed task.</returns>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
