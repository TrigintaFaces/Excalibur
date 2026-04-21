// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Provides extension methods for initializing OpenSearch resources during application startup.
/// </summary>
public static class HostExtensions
{
    /// <summary>
    /// Verifies OpenSearch connectivity during application startup by pinging the cluster.
    /// </summary>
    /// <param name="host">The application host.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="host"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the OpenSearch cluster is not reachable during startup verification.
    /// </exception>
    public static async Task VerifyOpenSearchConnectivityAsync(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        using var scope = host.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<OpenSearchClient>();
        var logger = scope.ServiceProvider.GetService<ILogger<OpenSearchClient>>();

        var response = await client.PingAsync(p => p).ConfigureAwait(false);

        if (!response.IsValid)
        {
            logger?.LogError("OpenSearch cluster is not reachable during startup verification.");
            throw new InvalidOperationException(
                "OpenSearch cluster is not reachable. Ensure the cluster is running and accessible.");
        }

        logger?.LogInformation("OpenSearch cluster connectivity verified successfully.");
    }
}
