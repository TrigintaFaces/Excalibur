// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Validates actual scoped capabilities before a generated projection host starts.</summary>
internal sealed class GeneratedProjectionValidator<TProjection>(IServiceProvider services, JsonTypeInfo<TProjection> typeInfo) : IHostedService, IGeneratedProjectionValidator
    where TProjection : class
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        _ = ProjectionStoreOperations<TProjection>.Generated(scope.ServiceProvider, typeInfo);
        GeneratedProjectionValidation.RequireSerializer(scope.ServiceProvider);
    }

    public Task ValidateAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal interface IGeneratedProjectionValidator
{
    Task ValidateAsync(CancellationToken cancellationToken);
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
        foreach (var validator in services.GetServices<IGeneratedProjectionValidator>())
        {
            await validator.ValidateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
