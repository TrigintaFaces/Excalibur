// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Outbox.Marten;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Marten outbox store on the service collection.
/// </summary>
public static class MartenOutboxExtensions
{
	/// <summary>
	/// Adds the Marten outbox store to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional action to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	public static IServiceCollection AddMartenOutboxStore(
		this IServiceCollection services,
		Action<MartenOutboxStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<MartenOutboxStoreOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MartenOutboxStoreOptions>, MartenOutboxStoreOptionsValidator>());

		services.TryAddSingleton<MartenOutboxStore>();
		services.AddKeyedSingleton<IOutboxStore>("marten", (sp, _) => sp.GetRequiredService<MartenOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("marten"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use the Marten outbox store.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Optional action to configure the options. </param>
	/// <returns> The dispatch builder for fluent configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder"/> is null. </exception>
	public static IDispatchBuilder UseMartenOutboxStore(
		this IDispatchBuilder builder,
		Action<MartenOutboxStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddMartenOutboxStore(configure);

		return builder;
	}
}
