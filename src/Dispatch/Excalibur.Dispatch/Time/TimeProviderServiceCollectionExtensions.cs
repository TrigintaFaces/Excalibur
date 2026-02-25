// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering TimeProvider services. Promotes use of System.TimeProvider (.NET 8+) over custom time abstractions.
/// </summary>
public static class TimeProviderServiceCollectionExtensions
{
	/// <summary>
	/// Adds the system TimeProvider as a singleton service. This is the recommended approach for production applications using .NET 8+.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// TimeProvider.System is thread-safe and provides:
	/// - High-resolution timing via GetUtcNow()
	/// - Timer creation with CreateTimer()
	/// - Delay operations with Delay()
	/// - Testability through dependency injection.
	/// </remarks>
	public static IServiceCollection AddSystemTimeProvider(this IServiceCollection services)
	{
		services.TryAddSingleton(static _ => TimeProvider.System);
		return services;
	}

	/// <summary>
	/// Adds a custom TimeProvider implementation. Useful for testing or specialized time scenarios.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="timeProvider"> The TimeProvider instance to register. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddTimeProvider(this IServiceCollection services, TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);
		services.TryAddSingleton(timeProvider);
		return services;
	}

	/// <summary>
	/// Adds a TimeProvider using a factory function. The factory is called once and the result is registered as a singleton.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="factory"> Factory function to create the TimeProvider. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddTimeProvider(this IServiceCollection services, Func<IServiceProvider, TimeProvider> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		services.TryAddSingleton(factory);
		return services;
	}
}
