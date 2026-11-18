// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.Hosting;

internal static class SampleCleanup
{
    internal static async Task<AggregateException?> CollectAsync(
        IHost app, CosmosDbPersistenceProvider provider, string[] ownedIds, PartitionKey partitionKey)
    {
        var failures = new List<Exception>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        foreach (var id in ownedIds)
        {
            try
            {
                await DeleteOwnedAsync(provider, id, partitionKey, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }

        try
        {
            app.Dispose();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        return failures.Count == 0 ? null : new AggregateException("Owned sample cleanup failed.", failures);
    }

    private static async Task DeleteOwnedAsync(
        CosmosDbPersistenceProvider provider, string id, PartitionKey partitionKey, CancellationToken ct)
    {
        if (await provider.GetByIdAsync<Product>(id, partitionKey, null, ct).ConfigureAwait(false) != null)
        {
            var deleted = await provider.DeleteAsync(id, partitionKey, null, ct).ConfigureAwait(false);
            SampleWorkload.RequireSuccess(deleted, "Cleanup " + id);
            if (await provider.GetByIdAsync<Product>(id, partitionKey, null, ct).ConfigureAwait(false) != null)
            {
                throw new InvalidOperationException("Cleanup left an owned product readable: " + id);
            }
        }
    }
}
