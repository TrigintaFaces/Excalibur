// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.AuditLogging;

/// <summary>
/// Installs the boot-time gate that verifies the configured audit store is durable.
/// </summary>
/// <remarks>
/// Durability is discovered from the store itself: a durable store answers for
/// <see cref="Excalibur.Compliance.IDurableAuditStore" /> through <see cref="IServiceProvider.GetService(Type)" />,
/// so no separate attestation is registered and a store cannot be advertised as durable without being it.
/// </remarks>
public static class DurableAuditStoreRegistration
{
	/// <summary>
	/// Adds the boot-time gate that fails startup when audit logging is left on a volatile store without the
	/// host having accepted that explicitly.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	/// <remarks>
	/// <para>
	/// Audit durability is an <b>explicit opt-in</b>: there is no framework composition the runtime can
	/// trigger on, because "this host requires a durable audit trail" is a consumer intent, not something the
	/// presence of a store implies. Call this method when you require the trail to survive a restart. Absent
	/// this call, a volatile (in-memory) audit store is accepted silently — the correct behaviour for a
	/// dev/test or simple MediatR-replacement host.
	/// </para>
	/// <para>
	/// Once installed, the protective outcome is the one a host gets by saying nothing: within a gated host,
	/// silence means the durability requirement applies (a volatile store fails startup), never that it is
	/// waived. Accepting a volatile audit trail in a gated host requires setting
	/// <see cref="AuditLoggingOptions.AllowVolatileAuditStore" /> deliberately.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAuditDurabilityGate(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<
				Microsoft.Extensions.Options.IValidateOptions<AuditLoggingOptions>,
				AuditStoreDurabilityValidator>());

		_ = services.AddOptions<AuditLoggingOptions>().ValidateOnStart();

		return services;
	}
}
