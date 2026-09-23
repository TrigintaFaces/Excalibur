// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.InMemory;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory persistence services.
/// </summary>
/// <remarks>
/// The in-memory persistence provider is intended for testing and development only.
/// Data is lost when the process restarts.
/// </remarks>
public static class InMemoryServiceCollectionExtensions
{
	/// <summary>
	/// Adds in-memory persistence services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An optional delegate to configure the in-memory provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("Enabling InMemoryProviderOptions.PersistToDisk serialises the store through reflection-based System.Text.Json, which trimming may break. Leave PersistToDisk disabled (the default) for a trim-safe host.")]
	[RequiresDynamicCode("Enabling InMemoryProviderOptions.PersistToDisk serialises the store through reflection-based System.Text.Json, whose converters are constructed at runtime. Leave PersistToDisk disabled (the default) for an AOT host.")]
	public static IServiceCollection AddExcaliburInMemory(
		this IServiceCollection services,
		Action<InMemoryProviderOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var builder = services.AddOptions<InMemoryProviderOptions>();
		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryProviderOptions>, InMemoryProviderOptionsValidator>());

		services.TryAddSingleton<InMemoryPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("inmemory"));

		return services;
	}

	/// <summary>
	/// Adds in-memory persistence services to the service collection using a fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the in-memory provider using the fluent builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburInMemory(inmemory =>
	/// {
	///     inmemory.MaxItemsPerCollection(5000)
	///             .EnableDetailedLogging(true)
	///             .PersistToDisk("./data/test-store.json");
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Enabling InMemoryProviderOptions.PersistToDisk serialises the store through reflection-based System.Text.Json, which trimming may break. Leave PersistToDisk disabled (the default) for a trim-safe host.")]
	[RequiresDynamicCode("Enabling InMemoryProviderOptions.PersistToDisk serialises the store through reflection-based System.Text.Json, whose converters are constructed at runtime. Leave PersistToDisk disabled (the default) for an AOT host.")]
	public static IServiceCollection AddExcaliburInMemory(
		this IServiceCollection services,
		Action<IInMemoryDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var inmemoryOptions = new InMemoryProviderOptions();
		var builder = new InMemoryDataBuilder(inmemoryOptions);
		configure(builder);

		var optionsBuilder = services.AddOptions<InMemoryProviderOptions>()
			.Configure(opt =>
			{
				opt.MaxItemsPerCollection = inmemoryOptions.MaxItemsPerCollection;
				opt.EnableDetailedLogging = inmemoryOptions.EnableDetailedLogging;
				opt.EnableMetrics = inmemoryOptions.EnableMetrics;
				opt.PersistToDisk = inmemoryOptions.PersistToDisk;
				opt.PersistenceFilePath = inmemoryOptions.PersistenceFilePath;
				opt.IsReadOnly = inmemoryOptions.IsReadOnly;
			});

		_ = optionsBuilder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryProviderOptions>, InMemoryProviderOptionsValidator>());

		services.TryAddSingleton<InMemoryPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("inmemory"));

		return services;
	}

	/// <summary>
	/// Adds in-memory persistence services to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("Binds IConfiguration to InMemoryProviderOptions by reflection, which requires types that trimming may remove. Use the AddExcaliburInMemory(IServiceCollection, Action<InMemoryProviderOptions>) overload to configure the options in code, or enable the source-generated configuration binder (EnableConfigurationBindingGenerator).")]
	[RequiresDynamicCode("Binds IConfiguration to InMemoryProviderOptions by reflection, which constructs accessors at runtime. Use the AddExcaliburInMemory(IServiceCollection, Action<InMemoryProviderOptions>) overload to configure the options in code, or enable the source-generated configuration binder (EnableConfigurationBindingGenerator).")]
	public static IServiceCollection AddExcaliburInMemory(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<InMemoryProviderOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryProviderOptions>, InMemoryProviderOptionsValidator>());

		services.TryAddSingleton<InMemoryPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("inmemory"));

		return services;
	}
}
