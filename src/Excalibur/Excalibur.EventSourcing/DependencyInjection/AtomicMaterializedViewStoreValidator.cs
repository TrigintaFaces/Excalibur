// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Startup-time validator that fails loud at <see cref="IHost.StartAsync"/> when materialized views are
/// registered against a store that cannot persist a view and its checkpoint atomically.
/// </summary>
/// <remarks>
/// <para>
/// The processor's constructor performs the same check, but the processor is registered lazily and is first
/// resolved inside the refresh service's retry loop — which catches, logs, and retries. A configuration error
/// raised there is not a failure an operator ever sees: the host starts, the projection never advances, and
/// the log fills with an exception nobody reads. Placing the probe in the startup pipeline puts it ahead of
/// any domain workload, where a misconfigured deployment stops instead of quietly not working.
/// </para>
/// <para>
/// The probe resolves the store and reads its capability directly. It does not test for the presence of a
/// registration, because a registration is not evidence of the behaviour it is supposed to stand for.
/// </para>
/// <para>
/// A missing store is not this validator's concern: nothing to check, and the omission surfaces through the
/// store's own prerequisite validation. Only a store that exists and cannot deliver the guarantee is rejected.
/// </para>
/// </remarks>
internal sealed class AtomicMaterializedViewStoreValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public AtomicMaterializedViewStoreValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public void Validate()
	{
		var isService = _services.GetRequiredService<IServiceProviderIsService>();

		// No view builder registered means nothing is being projected, so there is no guarantee to hold.
		// AddMaterializedViews() ahead of any view is a legal call and must start.
		if (!isService.IsService(typeof(MaterializedViewBuilderRegistration)))
		{
			return;
		}

		// A processor supplied through UseProcessor<TProcessor>() owns its own persistence contract. Only the
		// built-in processor's contract is the framework's to enforce.
		if (!isService.IsService(typeof(DefaultMaterializedViewProcessorMarker)))
		{
			return;
		}

		// A view is declared and the built-in processor will persist it. It cannot, without a store.
		var viewStore = _services.GetService<IMaterializedViewStore>()
			?? throw new InvalidOperationException(
				"Materialized views are registered but no view store is configured. Views cannot be persisted "
				+ "and the projection will never advance. Call UseStore<TStore>() or UseStore(storeFactory) on "
				+ "the materialized-views builder.");

		// Only an exactly-once (accumulating) projection requires an atomic view store. If every registered
		// projection declares AtLeastOnceIdempotent, a non-atomic store (Elasticsearch/OpenSearch) is a
		// supported configuration and must start. Refuse only the genuine incompatibility — an exactly-once
		// projection wired to a non-atomic store, which would silently double-count on crash. Whether a store
		// can write atomically is a behavioural claim, and registration is not evidence of behaviour; Require
		// resolves and reads the capability directly.
		var registrations = _services.GetServices<MaterializedViewBuilderRegistration>();
		if (registrations.Any(r => r.Semantics == ViewDeliverySemantics.ExactlyOnce))
		{
			_ = AtomicViewStoreRequirement.Require(viewStore);
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
