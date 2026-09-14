// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for the projection rebuild service.
/// </summary>
public static class ProjectionRebuildServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="IProjectionRebuildService"/> so a host can replay the global stream through its
	/// registered projections.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// <b>Why this exists as an explicit call rather than as part of the event-sourcing registration.</b> The
	/// rebuild replays every event in the global stream, so it is only meaningful on a host that has one. A
	/// host composed without an event-store provider is a legitimate composition, and registering the service
	/// there unconditionally would either start a host whose rebuild can never succeed, or fail a host that
	/// never intended to rebuild anything. Opting in keeps both cases honest, and matches
	/// <c>AddMaterializedViews</c>, the sibling projection registration in this package.
	/// </para>
	/// <para>
	/// <b>It fails fast.</b> The service resolves its collaborators from the provider at call time, so the
	/// container cannot validate them when the host starts. A startup prerequisite check stands in for that:
	/// a host that opts into rebuild without a global stream query stops at startup rather than reporting a
	/// failed rebuild for every projection later. See <see cref="ProjectionRebuildPrerequisiteValidator"/>
	/// for what is checked and, as importantly, what deliberately is not.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddProjectionRebuild(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<ProjectionRebuildOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ProjectionRebuildOptions>, ProjectionRebuildOptionsValidator>());

		services.TryAddSingleton<IProjectionRebuildService, ProjectionRebuildService>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ProjectionRebuildPrerequisiteValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ProjectionRebuildPrerequisiteValidator>());

		return services;
	}
}
