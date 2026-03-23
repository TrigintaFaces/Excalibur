// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Inbox.InMemory;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory inbox store.
/// </summary>
public static class InMemoryInboxExtensions
{
	/// <summary>
	/// Adds in-memory inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemoryInboxStore(
		this IServiceCollection services,
		Action<InMemoryInboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<InMemoryInboxOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryInboxOptions>, InMemoryInboxOptionsValidator>());

		services.TryAddSingleton<InMemoryInboxStore>();
		services.AddKeyedSingleton<IInboxStore>("inmemory", (sp, _) => sp.GetRequiredService<InMemoryInboxStore>());
		services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("inmemory"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use in-memory inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseInMemoryInboxStore(
		this IDispatchBuilder builder,
		Action<InMemoryInboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryInboxStore(configure);

		return builder;
	}
}
