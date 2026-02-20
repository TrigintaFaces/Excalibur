// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Excalibur in-memory test doubles.
/// </summary>
public static class ExcaliburTestingServiceCollectionExtensions
{
	/// <summary>
	/// Registers in-memory implementations of event store, snapshot store, and inbox store
	/// for use as test doubles in integration and unit tests.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience method that registers:
	/// <list type="bullet">
	///   <item><description><see cref="Excalibur.EventSourcing.InMemory.InMemoryEventStore"/> as <see cref="Excalibur.EventSourcing.Abstractions.IEventStore"/></description></item>
	///   <item><description><see cref="Excalibur.Data.InMemory.Snapshots.InMemorySnapshotStore"/> as <see cref="Excalibur.EventSourcing.Abstractions.ISnapshotStore"/></description></item>
	///   <item><description><see cref="Excalibur.Data.InMemory.Inbox.InMemoryInboxStore"/> as <see cref="Excalibur.Dispatch.Abstractions.IInboxStore"/></description></item>
	/// </list>
	/// </para>
	/// <para>
	/// A <see cref="NullLoggerFactory"/> is registered as a fallback if no logging factory is already configured,
	/// ensuring the stores can be resolved without requiring explicit logging setup.
	/// </para>
	/// <para>
	/// All stores are registered as singletons using <c>TryAddSingleton</c>, so existing registrations
	/// are not overwritten.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburTestingStores(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register NullLoggerFactory as fallback so stores can resolve ILogger<T> without explicit logging setup
		services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		services.AddInMemoryEventStore();
		services.AddInMemorySnapshotStore();
		services.AddInMemoryInboxStore();

		return services;
	}
}
