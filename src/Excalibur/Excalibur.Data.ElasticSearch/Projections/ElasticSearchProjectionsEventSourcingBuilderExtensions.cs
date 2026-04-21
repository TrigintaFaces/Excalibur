// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods bridging ElasticSearch projection registration onto
/// <see cref="IEventSourcingBuilder"/> so consumers can configure projection
/// stores inside a single <c>AddExcalibur(excalibur =&gt; excalibur.AddEventSourcing(es =&gt; ...))</c>
/// composition root (ADR-321).
/// </summary>
public static class ElasticSearchProjectionsEventSourcingBuilderExtensions
{
	/// <summary>
	/// Registers multiple ElasticSearch projection stores sharing a single node URI.
	/// </summary>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="nodeUri">The shared ElasticSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddElasticSearchProjections(
		this IEventSourcingBuilder builder,
		string nodeUri,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjections(nodeUri, configure);
		return builder;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores with shared options configuration.
	/// </summary>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="configureShared">Shared options applied to all projections.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddElasticSearchProjections(
		this IEventSourcingBuilder builder,
		Action<ElasticSearchProjectionStoreOptions> configureShared,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjections(configureShared, configure);
		return builder;
	}
}
