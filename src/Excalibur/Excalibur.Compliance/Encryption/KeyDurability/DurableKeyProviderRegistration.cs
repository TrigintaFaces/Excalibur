// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>
/// Installs the boot-time gate that verifies the configured key management provider keeps key material
/// across process restarts.
/// </summary>
/// <remarks>
/// Durability is discovered from the provider itself: a durable provider answers for
/// <see cref="IDurableKeyProvider" /> through <see cref="IServiceProvider.GetService(Type)" />, so no separate
/// attestation is registered and a provider cannot be advertised as durable without being it.
/// </remarks>
public static class DurableKeyProviderRegistration
{
	/// <summary>
	/// Adds the boot-time gate that fails startup when encryption is left on a volatile key provider
	/// without the host having accepted that explicitly.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	public static IServiceCollection AddKeyDurabilityGate(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KeyDurabilityOptions>, KeyDurabilityValidator>());

		_ = services.AddOptions<KeyDurabilityOptions>().ValidateOnStart();

		return services;
	}
}
