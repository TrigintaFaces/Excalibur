// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory outbox store.
/// </summary>
public static class InMemoryOutboxExtensions
{
	/// <summary>
	/// Adds in-memory outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemoryOutboxStore(
		this IServiceCollection services,
		Action<InMemoryOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<InMemoryOutboxOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<InMemoryOutboxStore>();
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use in-memory outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseInMemoryOutboxStore(
		this IDispatchBuilder builder,
		Action<InMemoryOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryOutboxStore(configure);

		return builder;
	}
}
