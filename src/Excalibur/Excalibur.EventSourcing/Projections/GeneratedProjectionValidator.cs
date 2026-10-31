// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Validates actual scoped capabilities before a generated projection host starts.</summary>
internal sealed class GeneratedProjectionValidator(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        GeneratedProjectionValidation.ValidateAsync(services, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class GeneratedProjectionValidation
{
    internal static void RequireSerializer(IServiceProvider services)
    {
        if (services.GetRequiredService<IEventSerializer>() is not AotJsonEventSerializer)
        {
            throw new InvalidOperationException("Generated projections require AddAotEventSerializer with the application's generated event context.");
        }
    }

    internal static async Task ValidateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        EventNotificationServiceCollectionExtensions.ConfigureProjections(services);
        var registrations = services.GetService<IProjectionRegistry>()?.GetAll() ?? [];
        await using var scope = services.CreateAsyncScope();
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            registration.ValidateGeneratedStore?.Invoke(scope.ServiceProvider);
        }
    }
}
