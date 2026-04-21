// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="IMaterializedViewsBuilder"/>.
/// </summary>
internal sealed class MaterializedViewsBuilder : IMaterializedViewsBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewsBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	public MaterializedViewsBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }

	/// <inheritdoc />
	public IMaterializedViewsBuilder AddBuilder<
		TView,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBuilder>()
		where TView : class, new()
		where TBuilder : class, IMaterializedViewBuilder<TView>
	{
		Services.TryAddSingleton<IMaterializedViewBuilder<TView>, TBuilder>();

		// Also register as the marker interface for discovery
		_ = Services.AddSingleton(sp =>
			new MaterializedViewBuilderRegistration(
				typeof(TView),
				typeof(TBuilder),
				sp.GetRequiredService<IMaterializedViewBuilder<TView>>()));

		return this;
	}

	/// <inheritdoc />
	public IMaterializedViewsBuilder UseStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
		where TStore : class, IMaterializedViewStore
	{
		Services.TryAddSingleton<IMaterializedViewStore, TStore>();
		return this;
	}

	/// <inheritdoc />
	public IMaterializedViewsBuilder UseStore(Func<IServiceProvider, IMaterializedViewStore> storeFactory)
	{
		ArgumentNullException.ThrowIfNull(storeFactory);
		Services.TryAddSingleton(storeFactory);
		return this;
	}

	/// <inheritdoc />
	public IMaterializedViewsBuilder UseProcessor<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>()
		where TProcessor : class, IMaterializedViewProcessor
	{
		Services.TryAddSingleton<IMaterializedViewProcessor, TProcessor>();
		return this;
	}
}
