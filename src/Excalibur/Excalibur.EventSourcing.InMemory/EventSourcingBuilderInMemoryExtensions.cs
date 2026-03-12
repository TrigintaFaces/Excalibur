// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// Extension methods for configuring in-memory event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>CdcBuilderInMemoryExtensions</c>).
/// </para>
/// <para>
/// <b>Warning:</b> The in-memory event store is intended for testing and development only.
/// Data is lost when the process restarts.
/// </para>
/// </remarks>
public static class EventSourcingBuilderInMemoryExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use an in-memory event store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseInMemory()
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseInMemory(this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddInMemoryEventStore();

		return builder;
	}
}
