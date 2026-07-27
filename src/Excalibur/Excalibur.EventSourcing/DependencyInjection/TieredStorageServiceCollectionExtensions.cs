// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring tiered event storage on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class TieredStorageServiceCollectionExtensions
{
	/// <summary>
	/// Enables tiered event storage with hot/cold separation.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure the archive policy.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, the <see cref="IEventStore"/> is wrapped with a
	/// <c>TieredEventStoreDecorator</c> that transparently reads through to
	/// <see cref="IColdEventStore"/> when events are missing from the hot tier.
	/// </para>
	/// <para>
	/// Consumers must also register an <see cref="IColdEventStore"/> implementation
	/// (e.g., Azure Blob, S3, or GCS provider).
	/// </para>
	/// </remarks>
	public static IEventSourcingBuilder UseTieredStorage(
		this IEventSourcingBuilder builder,
		Action<ArchivePolicy> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ArchivePolicy>, ArchivePolicyValidator>());
		builder.Services.AddOptionsWithValidateOnStart<ArchivePolicy>();

		WireTieredStorageRuntime(builder.Services);

		return builder;
	}

	// l31hx7 + jqk80w: wire tiered storage as a keyed decorator so cold read-through actually runs.
	//
	// The read path: repositories, time-travel, notification, and the non-keyed delegator all resolve the
	// keyed "default" IEventStore, so the read-through decorator MUST be re-bound onto keyed "default" —
	// registering it as an orphaned concrete type (the original l31hx7 bug) left every reader on the bare
	// hot store, and because EventArchiveService deletes archived events from the hot tier, that returned
	// INCOMPLETE history (data-loss shaped). We therefore move the RAW hot store to a private key and wrap
	// keyed "default" with the decorator.
	//
	// The archive path: EventArchiveService and the default IEventStoreArchive must enumerate and TRIM the
	// hot tier only — they bind the RAW hot ("tiered-hot"), never the decorated "default" (which would read
	// through cold during trim). The default IEventStoreArchive is the raw hot store itself (SQL Server /
	// Postgres implement IEventStoreArchive); we never fabricate one — fail-fast if the hot store can't
	// archive. A consumer may override by registering their own IEventStoreArchive before UseTieredStorage
	// (TryAdd yields to it).
	private static void WireTieredStorageRuntime(IServiceCollection services)
	{
		// Capture the registered keyed "default" hot store and move it to the private "tiered-hot" key so
		// the decorator can wrap it without re-resolving "default" (which would resolve the decorator
		// itself — infinite recursion).
		var hotDescriptor = services.LastOrDefault(sd =>
			sd.ServiceType == typeof(IEventStore)
			&& sd.IsKeyedService
			&& string.Equals(sd.ServiceKey as string, "default", StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				"UseTieredStorage requires a hot event store to be registered first. Call a hot-store "
				+ "provider (e.g. UseSqlServer(...)) before UseTieredStorage(...).");

		_ = services.Remove(hotDescriptor);

		// Raw hot store under the private "tiered-hot" key (captured from the original descriptor — no key
		// re-resolution, so no self-referential recursion). Preserves the original lifetime/instance.
		services.Add(new ServiceDescriptor(
			typeof(IEventStore),
			EventArchiveService.RawHotEventStoreKey,
			(sp, _) => ResolveOriginalEventStore(hotDescriptor, sp),
			hotDescriptor.Lifetime));

		// Re-bind keyed "default" -> the read-through decorator wrapping the raw hot. Single choke point:
		// every reader that resolves keyed "default" (and the non-keyed delegator that forwards to it) now
		// transparently reads through to cold storage on a hot-tier miss.
		_ = services.AddKeyedSingleton<IEventStore>(
			"default",
			(sp, _) => new TieredEventStoreDecorator(
				sp.GetRequiredKeyedService<IEventStore>(EventArchiveService.RawHotEventStoreKey),
				sp.GetRequiredService<IColdEventStore>(),
				sp.GetRequiredService<Logging.ILogger<TieredEventStoreDecorator>>(),
				sp.GetService<ISnapshotStore>()));

		// Default IEventStoreArchive = the RAW hot store ("tiered-hot"), never the decorated "default".
		// Fail-fast if the hot store can't archive — never a silent no-op archive.
		services.TryAddSingleton<IEventStoreArchive>(sp =>
		{
			var rawHot = sp.GetRequiredKeyedService<IEventStore>(EventArchiveService.RawHotEventStoreKey);
			return rawHot as IEventStoreArchive
				?? throw new InvalidOperationException(
					$"Tiered storage is enabled but the registered hot event store "
					+ $"'{rawHot.GetType().Name}' does not implement IEventStoreArchive (archive-candidate "
					+ "enumeration + delete-up-to-version). Use a hot store that supports archiving (SQL Server "
					+ "or Postgres), or register your own IEventStoreArchive before UseTieredStorage. Tiered "
					+ "storage never fabricates a default archive.");
		});

		// Run the archive background service (copies aged hot events to cold then trims the hot tier). Its
		// hotStore is bound to the RAW hot via [FromKeyedServices(RawHotEventStoreKey)] so trim enumerates
		// only the hot tier, never reads through cold.
		services.AddHostedService<EventArchiveService>();
	}

	// Resolves the original hot IEventStore from its (removed) keyed descriptor without re-resolving the
	// key — mirrors the keyed-safe capture used by DecorateEventStore. Handles instance-, factory-, and
	// type-registered hot stores.
	private static IEventStore ResolveOriginalEventStore(ServiceDescriptor descriptor, IServiceProvider sp)
	{
		if (descriptor.GetImplementationInstance() is IEventStore instance)
		{
			return instance;
		}

		if (descriptor.GetImplementationFactory() is { } factory)
		{
			return (IEventStore)factory(sp);
		}

		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			return (IEventStore)ActivatorUtilities.CreateInstance(sp, implementationType);
		}

		throw new InvalidOperationException(
			"Cannot resolve the original hot IEventStore from its service descriptor.");
	}

	/// <summary>
	/// Enables tiered event storage with hot/cold separation,
	/// with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configuration">The configuration section to bind <see cref="ArchivePolicy"/> from.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload binds options from configuration (e.g., <c>appsettings.json</c>) instead of
	/// an imperative <see cref="Action{T}"/> delegate. Data annotations are validated on start.
	/// </para>
	/// <para>
	/// Consumers must also register an <see cref="IColdEventStore"/> implementation
	/// (e.g., Azure Blob, S3, or GCS provider).
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder UseTieredStorage(
		this IEventSourcingBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		builder.Services.AddOptions<ArchivePolicy>()
			.Bind(configuration)
			.ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ArchivePolicy>, ArchivePolicyValidator>());

		WireTieredStorageRuntime(builder.Services);

		return builder;
	}
}
