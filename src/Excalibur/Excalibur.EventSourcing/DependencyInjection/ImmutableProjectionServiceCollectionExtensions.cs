// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering immutable inline projections on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class ImmutableProjectionServiceCollectionExtensions
{
	/// <summary>
	/// Registers an immutable projection with the event notification system.
	/// </summary>
	/// <typeparam name="TProjection">
	/// The immutable projection state type. No <c>new()</c> constraint -- supports
	/// C# records, init-only types, and any reference type.
	/// </typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure the immutable projection handlers.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use <c>WhenCreating</c> for factory methods (first event → new projection) and
	/// <c>WhenTransforming</c> for reducers (existing projection + event → new projection).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// builder.AddImmutableProjection&lt;OrderSummaryRecord&gt;(p =&gt; p
	///     .Inline()
	///     .WhenCreating&lt;OrderPlaced&gt;(e =&gt; new OrderSummaryRecord(e.AggregateId, e.Amount, "Placed"))
	///     .WhenTransforming&lt;OrderShipped&gt;((proj, e) =&gt; proj with { Status = "Shipped" }));
	/// </code>
	/// </example>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	public static IEventSourcingBuilder AddImmutableProjection<TProjection>(
		this IEventSourcingBuilder builder,
		Action<IImmutableProjectionBuilder<TProjection>> configure)
		where TProjection : class
#pragma warning restore RS0016
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure event notification infrastructure is registered
		builder.UseEventNotification();

		// Run configure eagerly for DI registration, defer projection registration
		var projBuilder = new ImmutableProjectionBuilder<TProjection>(builder.Services);
		configure(projBuilder);

		builder.Services.AddSingleton<EventNotificationServiceCollectionExtensions.IConfigureProjection>(sp =>
		{
			return new ConfigureImmutableProjection<TProjection>(
				sp.GetRequiredService<IProjectionRegistry>(), projBuilder);
		});

		return builder;
	}

	/// <summary>
	/// Deferred immutable projection registration.
	/// </summary>
	internal sealed class ConfigureImmutableProjection<TProjection>
		: EventNotificationServiceCollectionExtensions.IConfigureProjection
		where TProjection : class
	{
		private readonly IProjectionRegistry _registry;
		private readonly ImmutableProjectionBuilder<TProjection> _builder;
		private bool _configured;

		internal ConfigureImmutableProjection(
			IProjectionRegistry registry,
			ImmutableProjectionBuilder<TProjection> builder)
		{
			_registry = registry;
			_builder = builder;
		}

		public void Configure()
		{
			if (_configured)
			{
				return;
			}

			_configured = true;
			_builder.Build(_registry);
		}
	}
}
