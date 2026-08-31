// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Options marker whose <c>ValidateOnStart</c> registration triggers the erasure-capability fail-closed
/// gate at host startup. Carries no configuration; it exists only to hang a startup validation off.
/// </summary>
internal sealed class TieredErasureGuardOptions;

/// <summary>
/// Fails at host startup when event-store erasure is enabled but the composed event store cannot answer
/// the <see cref="IEventStoreErasure"/> capability probe — the state a cold-tier (hot/cold) composition
/// produces, because the tiered store can erase the hot tier only and the archived range has no erase
/// surface. Reporting it at boot, rather than at the first right-to-erasure request, means the consumer
/// learns their composition cannot erase while they can still change it, not while a statutory clock is
/// already running.
/// </summary>
/// <remarks>
/// The gate keys on the capability the erasure contributor actually requires, not on which providers are
/// registered: it asks the composed keyed <c>"default"</c> store for <see cref="IEventStoreErasure"/>,
/// the same probe the contributor's factory applies, moved from first-request to boot. A store that
/// answers passes, so an ordinary erasure host is unaffected; any decorator that denies the capability —
/// the tiered decorator today, and any later one — is caught without this gate needing to know it exists.
/// The contributor keeps its own throw as the floor for a host-less composition, where
/// <c>ValidateOnStart</c> never runs.
/// </remarks>
internal sealed class TieredErasureGuard(IServiceProvider serviceProvider)
	: IValidateOptions<TieredErasureGuardOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TieredErasureGuardOptions options)
	{
		// Resolved here rather than in the constructor so the store graph is built only when this gate
		// actually runs. Every keyed "default" event store registration is a singleton (providers register
		// it as one, and both decoration seams preserve the original descriptor's lifetime), so resolving
		// from the root provider is valid; the provider constructors take connection factories and clients
		// and open nothing.
		var eventStore = serviceProvider.GetKeyedService<IEventStore>("default");

		// No event store registered at all is a different composition error, reported by whoever requires
		// one. This gate speaks only to a store that is present and cannot erase.
		if (eventStore is null || eventStore.GetService(typeof(IEventStoreErasure)) is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"Event-store erasure is enabled but the composed IEventStore ({eventStore.GetType().Name}) does "
			+ "not answer the IEventStoreErasure capability probe, so a right-to-erasure request against it "
			+ "could not be honoured. Cold-tier archival and event-store erasure are not supported together: "
			+ "the tiered store erases the hot tier only, and the archived range has no erase surface, so the "
			+ "erase would leave the archived copies behind. Remove tiered storage, or do not enable "
			+ "event-store erasure, until cold-tier erasure is available.");
	}
}
