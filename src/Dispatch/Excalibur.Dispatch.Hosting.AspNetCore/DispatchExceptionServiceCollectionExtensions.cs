// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

using DispatchProblemDetailsOptions = Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.ProblemDetailsOptions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers lightweight HTTP exception handling for Dispatch applications.</summary>
public static class DispatchExceptionServiceCollectionExtensions
{
    /// <summary>Registers framework status mapping, problem details and production-safe exception responses.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional problem-details customization.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>Enable the registered handlers with UseExceptionHandler. Multiple handler types execute in registration order.</remarks>
    public static IServiceCollection AddDispatchExceptionHandler(this IServiceCollection services,
        Action<DispatchProblemDetailsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure(configureOptions ?? (static _ => { }));
        services.AddProblemDetails();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionHandler, GlobalExceptionHandler>());
        return services;
    }
}
