// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Non-owning, provider-local witnesses of store instances created by the durable registration seam.
/// </summary>
internal interface IDurableScheduleStoreCapability
{
	/// <summary>Records an instance created by an attesting store factory in this provider.</summary>
	/// <param name="store">The instance whose persistence the registration declares.</param>
	void Attest(IScheduleStore store);

	/// <summary>Checks whether this provider attested the exact selected store instance.</summary>
	/// <param name="store">The selected store instance.</param>
	/// <returns>Whether an attesting factory created this instance in this provider.</returns>
	bool IsFor(IScheduleStore store);
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
	/// <remarks>
	/// The singleton is created with <see cref="ActivatorUtilities"/>. A constructor marked with
	/// <see cref="ActivatorUtilitiesConstructorAttribute"/> takes precedence; otherwise the longest
	/// public constructor whose dependencies can be supplied is selected. Ambiguous or unsatisfied
	/// constructors fail resolution. This intentionally differs from ordinary implementation-type DI
	/// registration, which does not honor the preferred-constructor attribute. The container owns the
	/// resulting store once; no concrete/interface disposal alias is registered.
	/// </remarks>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	public static IServiceCollection AddDurableScheduleStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TScheduleStore>(this IServiceCollection services)
		where TScheduleStore : class, IScheduleStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// The witness is constructed by each provider, never captured by the service collection.
		// Only this exact factory attests the instance; a later registration cannot inherit it.
		services.TryAddSingleton<IDurableScheduleStoreCapability, DurableScheduleStoreCapabilityMarker>();
		_ = services.Replace(ServiceDescriptor.Singleton<IScheduleStore>(static provider =>
		{
			var store = ActivatorUtilities.CreateInstance<TScheduleStore>(provider);
			provider.GetRequiredService<IDurableScheduleStoreCapability>().Attest(store);
			return store;
		}));

		return services;
	}

	/// <summary>
	/// Adds the boot-time gate that fails startup when scheduled delivery is left on a volatile store
	/// without the host having accepted that explicitly.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="services" /> is <see langword="null" />. </exception>
	internal static IServiceCollection AddScheduleDurabilityGate(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ScheduleDurabilityOptions>, ScheduleDurabilityValidator>());

		_ = services.AddOptions<ScheduleDurabilityOptions>().ValidateOnStart();

		return services;
	}

	private sealed class DurableScheduleStoreCapabilityMarker : IDurableScheduleStoreCapability
	{
		private readonly HashSet<IScheduleStore> _stores = new(ReferenceEqualityComparer.Instance);
		private readonly Lock _gate = new();

		public void Attest(IScheduleStore store)
		{
			lock (_gate)
			{
				_ = _stores.Add(store);
			}
		}

		public bool IsFor(IScheduleStore store)
		{
			lock (_gate)
			{
				return _stores.Contains(store);
			}
		}
	}
}
