// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for configuring projection caching on an <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// Projection caching is a sub-concern of event sourcing: it enables CQRS projection
/// handlers to invalidate cached query results when the event stream advances. It therefore
/// attaches at the event-sourcing domain boundary rather than the root Excalibur builder.
/// </para>
/// <para>
/// <strong>Prerequisites:</strong> <c>AddDispatchCaching()</c> must be configured first because
/// projection invalidation depends on <c>ICacheInvalidationService</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcalibur(excalibur => excalibur
///     .AddEventSourcing(es => es.AddProjectionCaching()));
/// </code>
/// </example>
public static class EventSourcingBuilderProjectionCachingExtensions
{
	/// <summary>
	/// Adds projection cache invalidation services to the event-sourcing builder.
	/// </summary>
	/// <param name="builder">The <see cref="IEventSourcingBuilder"/> to configure.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddProjectionCaching(this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddExcaliburProjectionCaching();
		return builder;
	}
}
