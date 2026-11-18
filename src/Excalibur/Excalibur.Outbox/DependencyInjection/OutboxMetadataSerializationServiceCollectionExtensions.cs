// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Metadata-only registration for generated outbox header serialization.</summary>
public static class OutboxMetadataSerializationServiceCollectionExtensions
{
	/// <summary>Registers explicit outbox metadata configuration without registering workers or stores.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serialization">The explicit metadata configuration.</param>
	/// <returns>The service collection.</returns>
	/// <exception cref="ArgumentNullException">An argument is null.</exception>
	/// <exception cref="InvalidOperationException">A different or opaque configuration registration already exists.</exception>
	/// <remarks>
	/// Registering the same instance again is idempotent. Conflicting configuration, including keyed,
	/// implementation-type and factory registrations, is rejected without invoking a factory or building
	/// an intermediate provider. Explicit provider registration overloads can capture a store-specific configuration instead of
	/// this common default. Separate service providers keep independent configuration. This registration
	/// does not add independently named stores to a provider whose existing aliases select one store.
	/// </remarks>
	public static IServiceCollection AddOutboxMetadataSerialization(
		this IServiceCollection services,
		OutboxMetadataSerialization serialization)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serialization);

		var registered = false;
		foreach (var descriptor in services)
		{
			if (descriptor.ServiceType != typeof(OutboxMetadataSerialization)) { continue; }
			if (descriptor.IsKeyedService || !ReferenceEquals(descriptor.GetImplementationInstance(), serialization))
			{
				throw new InvalidOperationException(
					"Outbox metadata serialization is already registered differently. Register one common default instance, or select explicit metadata through the provider registration overload.");
			}

			registered = true;
		}

		if (!registered) { services.AddSingleton(serialization); }
		return services;
	}
}
