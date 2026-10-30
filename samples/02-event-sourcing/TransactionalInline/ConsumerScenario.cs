// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.EventSourcing;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Examples.TransactionalInline;

internal static class ConsumerScenario
{
    internal static async Task RunAsync(IServiceProvider services, CancellationToken token)
    {
        var id = Guid.NewGuid().ToString("N");
        var request = new RequestMetadata("sample-user", Guid.NewGuid().ToString("N"));
        OrderWriteResult created;
        await using (var scope = services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<OrderApplicationService>();
            if (await app.ExistsAsync(id, token)) { throw new InvalidOperationException("Fresh sample key already exists."); }
            created = await app.CreateAsync(id, 10, request, token);
        }

        OrderWriteResult updated;
        await using (var scope = services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<OrderApplicationService>();
            var loaded = await app.ReadAsync(id, token);
            if (loaded?.Total != 10 || loaded.ETag != created.ETag)
            { throw new InvalidOperationException("Reload must reconstruct the aggregate and its ETag."); }
            updated = await app.AddAmountAsync(id, 5, loaded.ETag, request, token);
            if ((await app.ReadModelAsync(id, token))?.Total != 15)
            { throw new InvalidOperationException("Committed projection total must be 15."); }
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<OrderApplicationService>();
            try
            {
                await app.AddAmountAsync(id, 100, created.ETag, request, token);
                throw new InvalidOperationException("An old client ETag must be rejected.");
            }
            catch (ConcurrencyException)
            {
                // An HTTP boundary can map this to a conflict/precondition response.
                // The client must reload and reconsider its command, rather than retry blindly.
            }
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<OrderApplicationService>();
            try
            {
                await app.AddAmountAsync(id, -1, updated.ETag, request, token);
                throw new InvalidOperationException("The projection should have rejected this save.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "Sample projection refuses negative amounts.")
            {
                // The service discards the failed aggregate; the next request loads fresh state.
            }
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<OrderApplicationService>();
            var reloaded = await app.ReadAsync(id, token);
            var model = await app.ReadModelAsync(id, token);
            if (reloaded?.Version != 2 || reloaded.Total != 15 || reloaded.ETag != updated.ETag || model?.Total != 15)
            { throw new InvalidOperationException("Rejected saves must preserve stream state, ETag and projection total."); }
            var source = scope.ServiceProvider.GetRequiredService<IEventStore>();
            var history = await source.LoadAsync(id, nameof(Order), token);
            if (history.Count != 2 || history.Any(row => row.Metadata is null))
            { throw new InvalidOperationException("Committed events must preserve application audit metadata."); }
            foreach (var row in history)
            {
                using var metadata = System.Text.Json.JsonDocument.Parse(row.Metadata!);
                if (metadata.RootElement.GetProperty("actor-id").GetString() != request.ActorId
                    || metadata.RootElement.GetProperty("correlation-id").GetString() != request.CorrelationId)
                { throw new InvalidOperationException("Stored audit metadata must match the originating request."); }
            }
        }

        Console.WriteLine("PASS: create, reload, ETag update, stale-client refusal and transactional rollback verified.");
        Console.WriteLine("PASS: two events and total 15 committed; rejected third event and model update rolled back.");
    }
}
