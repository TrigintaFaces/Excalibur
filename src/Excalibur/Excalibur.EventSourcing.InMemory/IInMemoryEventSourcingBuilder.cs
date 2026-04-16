// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// Fluent builder interface for configuring in-memory event sourcing settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory-specific options such as the store name
/// used for keyed DI registration.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburEventSourcing(es =>
/// {
///     es.UseInMemory(inmemory =>
///     {
///         inmemory.StoreName("test-store");
///     });
/// });
/// </code>
/// </example>
public interface IInMemoryEventSourcingBuilder
{
	/// <summary>
	/// Sets the keyed service name for the in-memory event store registration.
	/// </summary>
	/// <param name="storeName">The store name used as the keyed service key. Must not be null or whitespace.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="storeName"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is <c>"inmemory"</c>. Use this to differentiate multiple in-memory stores
	/// in the same service collection.
	/// </para>
	/// </remarks>
	IInMemoryEventSourcingBuilder StoreName(string storeName);
}
