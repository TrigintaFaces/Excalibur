// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Opts a leader election registration out of the on-by-default fencing-token auto-registration.
/// </summary>
/// <remarks>
/// Fencing is on by default: every built-in <c>Add{Store}LeaderElection()</c> / <c>Use{Store}()</c> entry
/// point auto-registers that store's arbitrated <see cref="IFencingTokenProvider"/>, because the failure
/// mode of an unfenced election is silent data corruption, not an exception a consumer would notice. Call
/// <see cref="WithoutFencingTokens(ILeaderElectionBuilder)"/> to run a specific election in non-fencing
/// mode instead.
/// </remarks>
public static class FencingOptOutServiceCollectionExtensions
{
	/// <summary>
	/// Suppresses the fencing-token provider that <c>Add{Store}LeaderElection()</c> / <c>Use{Store}()</c>
	/// would otherwise register by default, so <see cref="IFencingTokenProvider"/> resolves to
	/// <see langword="null"/> and the election runs in non-fencing mode.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Order-independent within a single composition pass: the opt-out is checked lazily, the first time
	/// something resolves <see cref="IFencingTokenProvider"/> (always after the whole service collection
	/// has been configured), so this may be called before or after the store's <c>Use{Store}()</c> call.
	/// </remarks>
	public static ILeaderElectionBuilder WithoutFencingTokens(this ILeaderElectionBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.WithoutFencingTokens();

		return builder;
	}

	/// <inheritdoc cref="WithoutFencingTokens(ILeaderElectionBuilder)"/>
	/// <param name="services">The service collection.</param>
	public static IServiceCollection WithoutFencingTokens(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<FencingOptOutMarker>();

		return services;
	}
}

/// <summary>
/// Marker whose mere registration signals that
/// <see cref="FencingOptOutServiceCollectionExtensions.WithoutFencingTokens(IServiceCollection)"/> was
/// called. Checked lazily by the fencing-token provider factory and by
/// <see cref="FencingTokenPrerequisiteValidator"/>, both of which run after the whole service collection
/// has been configured — so its presence is order-independent relative to when it was registered.
/// </summary>
internal sealed class FencingOptOutMarker;

/// <summary>
/// Shared registration helper each store's leader election extension uses to auto-register its arbitrated
/// fencing-token provider by default, unless the consumer opted out.
/// </summary>
internal static class FencingTokenDefaultRegistration
{
	/// <summary>
	/// Registers <paramref name="createProvider"/> as the default <see cref="IFencingTokenProvider"/> for
	/// this host, plus the fencing middleware and startup prerequisite check — everything
	/// <c>WithFencingTokens()</c> wires today — unless the consumer opted out via
	/// <see cref="FencingOptOutServiceCollectionExtensions.WithoutFencingTokens(IServiceCollection)"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="createProvider">
	/// Builds the store's concrete provider from the service provider. Not invoked at all when the
	/// consumer opted out — the factory returns <see langword="null"/> instead, so
	/// <c>GetService&lt;IFencingTokenProvider&gt;()</c> reports the provider absent rather than
	/// constructing one nobody asked for.
	/// </param>
	internal static void TryAddDefaultFencingTokenProvider(
		this IServiceCollection services,
		Func<IServiceProvider, IFencingTokenProvider> createProvider)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(createProvider);

		services.TryAddSingleton<IFencingTokenProvider>(sp =>
			sp.GetService<FencingOptOutMarker>() is not null ? null! : createProvider(sp));

		services.RegisterFencingSupportServices();
	}

	/// <summary>
	/// Registers the fencing middleware and the startup prerequisite validator. Shared by both the
	/// on-by-default auto-registration path and the explicit opt-in <c>WithFencingTokens()</c> path (for a
	/// consumer-supplied <see cref="ILeaderElection"/> that is not one of the built-in providers).
	/// </summary>
	internal static void RegisterFencingSupportServices(this IServiceCollection services)
	{
		services.TryAddSingleton<FencingTokenMiddleware>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, FencingTokenPrerequisiteValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, FencingTokenPrerequisiteValidator>());
	}
}
