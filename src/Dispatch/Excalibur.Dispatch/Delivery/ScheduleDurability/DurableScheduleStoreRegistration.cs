// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Registration-time attestation that the configured <see cref="IScheduleStore" /> keeps pending schedules
/// across process restarts.
/// </summary>
/// <remarks>
/// The marker is emitted only alongside the registration that supplies a durable store, never on its own,
/// so a volatile store cannot carry a truthful-looking attestation.
/// </remarks>
internal interface IDurableScheduleStoreCapability
{
	/// <summary>
	/// Structural-lock member: makes this interface implementable only from within this assembly. Never
	/// invoked — the capability is consumed purely as a registration-time presence signal.
	/// </summary>
	void AssertEmittedAlongsideDurableStoreRegistration();
}

/// <summary>
/// Registers an <see cref="IScheduleStore" /> together with, and inseparably from, its durability
/// attestation, and installs the boot-time gate that refuses a volatile store the host never asked for.
/// </summary>
public static class DurableScheduleStoreRegistration
{
	/// <summary>
	/// Registers <typeparamref name="TScheduleStore" /> as the singleton <see cref="IScheduleStore" /> and,
	/// in the same act, attests that pending schedules survive a restart.
	/// </summary>
	/// <typeparam name="TScheduleStore"> The durable schedule store implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	public static IServiceCollection AddDurableScheduleStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TScheduleStore>(this IServiceCollection services)
		where TScheduleStore : class, IScheduleStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// Replace rather than TryAdd, deliberately. AddDispatchScheduling seats the volatile in-memory store
		// into this same contract key, so a TryAdd here is a silent no-op whenever scheduling was composed
		// first -- and the attestation below would still be emitted. That combination is the precise defect
		// this seam exists to prevent: a host that asked for a durable store, passed the gate, and is running
		// on the volatile one. Replacing keeps the store and its attestation inseparable in either order.
		_ = services.Replace(ServiceDescriptor.Singleton<IScheduleStore, TScheduleStore>());
		services.TryAddSingleton<IDurableScheduleStoreCapability, DurableScheduleStoreCapabilityMarker>();

		return services;
	}

	/// <summary>
	/// Adds the boot-time gate that fails startup when scheduled delivery is left on a volatile store
	/// without the host having accepted that explicitly.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	public static IServiceCollection AddScheduleDurabilityGate(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ScheduleDurabilityOptions>, ScheduleDurabilityValidator>());

		_ = services.AddOptions<ScheduleDurabilityOptions>().ValidateOnStart();

		return services;
	}

	private sealed class DurableScheduleStoreCapabilityMarker : IDurableScheduleStoreCapability
	{
		public void AssertEmittedAlongsideDurableStoreRegistration()
		{
			// Never invoked; the type-level unimplementability outside this assembly is the mechanism.
		}
	}
}
