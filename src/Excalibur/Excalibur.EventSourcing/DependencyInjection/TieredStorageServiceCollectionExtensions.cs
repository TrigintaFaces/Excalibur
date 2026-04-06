// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing.Abstractions;
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

		// Register the decorator as a named/keyed service that wraps the hot store
		builder.Services.AddSingleton<TieredEventStoreDecorator>(sp =>
		{
			var hotStore = sp.GetRequiredKeyedService<IEventStore>("default");
			return new TieredEventStoreDecorator(
				hotStore,
				sp.GetRequiredService<IColdEventStore>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TieredEventStoreDecorator>>(),
				sp.GetService<ISnapshotStore>());
		});

		return builder;
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

		// Register the decorator as a named/keyed service that wraps the hot store
		builder.Services.AddSingleton<TieredEventStoreDecorator>(sp =>
		{
			var hotStore = sp.GetRequiredKeyedService<IEventStore>("default");
			return new TieredEventStoreDecorator(
				hotStore,
				sp.GetRequiredService<IColdEventStore>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TieredEventStoreDecorator>>(),
				sp.GetService<ISnapshotStore>());
		});

		return builder;
	}
}
