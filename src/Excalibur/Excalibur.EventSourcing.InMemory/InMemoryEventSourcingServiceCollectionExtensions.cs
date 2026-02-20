// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering in-memory event sourcing services.
/// </summary>
public static class InMemoryEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds the in-memory event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="InMemoryEventStore"/> as a singleton implementation of <see cref="IEventStore"/>.
	/// </para>
	/// <para>
	/// <b>Warning:</b> The in-memory event store is intended for testing and development only.
	/// Data is lost when the process restarts.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInMemoryEventStore(this IServiceCollection services)
	{
		_ = services.AddSingleton<InMemoryEventStore>();
		_ = services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<InMemoryEventStore>());

		return services;
	}
}
