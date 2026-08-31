// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch.IndexManagement;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using OpenSearch.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the OpenSearch index management services.
/// </summary>
public static class OpenSearchIndexManagementServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="IIndexLifecycleManager" />, <see cref="IIndexTemplateManager" />,
	/// <see cref="IIndexOperationsManager" /> and <see cref="IIndexAliasManager" />, backed by the
	/// OpenSearch client already in the container.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is null.</exception>
	/// <remarks>
	/// Register a client first — <c>AddOpenSearchServices(...)</c> or <c>AddExcaliburOpenSearch(...)</c>.
	/// Either registration shape is accepted: the <see cref="IOpenSearchClient" /> interface, or the
	/// concrete <see cref="OpenSearchClient" /> that this package's own entry points produce. Resolving
	/// a manager with no client in the container throws, naming the missing registration, rather than
	/// silently constructing a client pointed somewhere else.
	/// </remarks>
	public static IServiceCollection AddOpenSearchIndexManagement(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IIndexLifecycleManager>(static sp =>
			new IndexLifecycleManager(ResolveClient(sp), sp.GetRequiredService<ILogger<IndexLifecycleManager>>()));

		services.TryAddSingleton<IIndexTemplateManager>(static sp =>
			new IndexTemplateManager(ResolveClient(sp), sp.GetRequiredService<ILogger<IndexTemplateManager>>()));

		services.TryAddSingleton<IIndexOperationsManager>(static sp =>
			new IndexOperationsManager(ResolveClient(sp), sp.GetRequiredService<ILogger<IndexOperationsManager>>()));

		services.TryAddSingleton<IIndexAliasManager>(static sp =>
			new IndexAliasManager(ResolveClient(sp), sp.GetRequiredService<ILogger<IndexAliasManager>>()));

		return services;
	}

	private static IOpenSearchClient ResolveClient(IServiceProvider serviceProvider) =>
		serviceProvider.GetService<IOpenSearchClient>()
		?? serviceProvider.GetService<OpenSearchClient>()
		?? throw new InvalidOperationException(
			"OpenSearch index management requires an OpenSearch client in the container. " +
			"Call AddOpenSearchServices(...) or AddExcaliburOpenSearch(...) before AddOpenSearchIndexManagement().");
}
