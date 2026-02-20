// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory snapshot store.
/// </summary>
public static class InMemorySnapshotExtensions
{
	/// <summary>
	/// Adds in-memory snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemorySnapshotStore(
		this IServiceCollection services,
		Action<InMemorySnapshotOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<InMemorySnapshotOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<InMemorySnapshotStore>();
		services.TryAddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<InMemorySnapshotStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use in-memory snapshot store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseInMemorySnapshotStore(
		this IDispatchBuilder builder,
		Action<InMemorySnapshotOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemorySnapshotStore(configure);

		return builder;
	}
}
