// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering fencing token services.
/// </summary>
public static class FencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Adds fencing token middleware and registers the specified provider.
	/// </summary>
	/// <typeparam name="TProvider">The fencing token provider implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFencingTokenSupport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
		this IServiceCollection services)
		where TProvider : class, IFencingTokenProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, TProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds fencing token middleware using an existing provider instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="provider">The fencing token provider instance.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFencingTokenSupport(
		this IServiceCollection services,
		IFencingTokenProvider provider)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(provider);

		services.TryAddSingleton(provider);
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}

    /// <summary>Registers a selected issuing provider, its nonowning issuer view, and fencing middleware.</summary>
    /// <typeparam name="TIssuer">The actual issuing provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// Uses ordinary implementation-type DI activation and TryAdd semantics. The default issuer view
    /// forwards to the selected reader; a selected read-only replacement refuses issuer resolution.
    /// An independently registered issuer bypasses that view and owns authority consistency, even if
    /// only the issuer registration is overridden. No physical-backend equivalence is inferred.
    /// </remarks>
    public static IServiceCollection AddFencingTokenIssuerSupport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TIssuer>(
        this IServiceCollection services)
        where TIssuer : class, IFencingTokenIssuer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFencingTokenProvider, TIssuer>();
        AddIssuerView(services);
        return services;
    }

    /// <summary>Registers an externally owned issuing instance and a nonowning selected issuer view.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="issuer">The externally owned issuer instance.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// The supplied instance is intentionally shared across containers built from this collection;
    /// the caller owns its disposal. Existing reader/issuer registrations retain TryAdd precedence.
    /// Independently supplied issuer registrations are caller-owned authority consistency.
    /// </remarks>
    public static IServiceCollection AddFencingTokenIssuerSupport(
        this IServiceCollection services, IFencingTokenIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(issuer);
        services.TryAddSingleton<IFencingTokenProvider>(issuer);
        AddIssuerView(services);
        return services;
    }

    private static void AddIssuerView(IServiceCollection services)
    {
        services.TryAddSingleton<IFencingTokenIssuer>(static provider =>
        {
            var selected = provider.GetRequiredService<IFencingTokenProvider>();
            if (selected is not IFencingTokenIssuer issuer)
            {
                throw new InvalidOperationException("The selected IFencingTokenProvider cannot mint. Select an IFencingTokenIssuer for issuing support; read-only providers remain valid for observation.");
            }
            return new SelectedIssuerView(issuer);
        });
        services.TryAddSingleton<FencingTokenMiddleware>();
    }

    // Deliberately not disposable: the selected provider is owned once by its original DI registration
    // (or by the caller of the instance overload), never captured again as a disposable factory alias.
    private sealed class SelectedIssuerView(IFencingTokenIssuer selected) : IFencingTokenIssuer
    {
        public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken) =>
            selected.GetTokenAsync(resourceId, cancellationToken);
        public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken) =>
            selected.ValidateTokenAsync(resourceId, token, cancellationToken);
        public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken) =>
            selected.IssueTokenAsync(resourceId, cancellationToken);
    }
}
