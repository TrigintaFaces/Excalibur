// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Fluent builder for configuring an immutable projection's mode, event handlers, and caching.
/// </summary>
/// <typeparam name="TProjection">
/// The immutable projection state type. No <c>new()</c> constraint required --
/// supports C# records, init-only types, and any immutable data structure.
/// </typeparam>
/// <remarks>
/// <para>
/// Use <c>WhenCreating</c> for factory methods (first event → new projection) and
/// <c>WhenTransforming</c> for reducers (existing projection + event → new projection).
/// </para>
/// <para>
/// Behavior per Q1 decision:
/// <list type="bullet">
/// <item>null current + Creating → factory returns new instance</item>
/// <item>null current + Transforming → throws (no state to transform)</item>
/// <item>non-null current + Transforming → reducer returns new instance</item>
/// <item>non-null current + Creating → factory replaces (last-wins)</item>
/// </list>
/// </para>
/// </remarks>
#pragma warning disable RS0016 // Generic interface members not representable in public API baseline
public interface IImmutableProjectionBuilder<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Configures the projection to run inline during <c>SaveAsync</c>.
	/// </summary>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> Inline();

	/// <summary>
	/// Configures the projection to run asynchronously.
	/// </summary>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> Async();

	/// <summary>
	/// Registers a factory handler that creates a new projection from an event.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type.</typeparam>
	/// <param name="factory">A function that creates a new projection from the event.</param>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> WhenCreating<TEvent>(
		Func<TEvent, TProjection> factory) where TEvent : IDomainEvent;

	/// <summary>
	/// Registers a transform handler that produces a new projection from the current state and an event.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type.</typeparam>
	/// <param name="transform">
	/// A function that takes the current projection and event, returning a new projection instance.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> WhenTransforming<TEvent>(
		Func<TProjection, TEvent, TProjection> transform) where TEvent : IDomainEvent;

	/// <summary>
	/// Registers a DI-resolved typed handler for immutable projection transforms.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type.</typeparam>
	/// <typeparam name="THandler">
	/// The handler type implementing
	/// <see cref="IImmutableProjectionHandler{TProjection, TEvent}"/>.
	/// </typeparam>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> WhenHandledBy<TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IImmutableProjectionHandler<TProjection, TEvent>;

	/// <summary>
	/// Scans the specified assembly for all implementations of
	/// <see cref="Excalibur.EventSourcing.Abstractions.IImmutableProjectionHandler{TProjection, TEvent}"/>
	/// and registers them as handlers for this projection.
	/// </summary>
	/// <param name="assembly">The assembly to scan.</param>
	/// <returns>This builder for fluent chaining.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Assembly scanning uses reflection to discover IImmutableProjectionHandler<T, TEvent> implementations.")]
	IImmutableProjectionBuilder<TProjection> AddImmutableProjectionHandlersFromAssembly(
		System.Reflection.Assembly assembly);

	/// <summary>
	/// Configures optional caching for ephemeral projection results.
	/// </summary>
	/// <param name="ttl">The cache time-to-live.</param>
	/// <returns>This builder for fluent chaining.</returns>
	IImmutableProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl);
}
#pragma warning restore RS0016
