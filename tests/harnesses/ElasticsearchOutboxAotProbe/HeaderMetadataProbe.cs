// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Transport.Extensions;
using System.Text.Json;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

internal static class HeaderMetadataProbe
{
    internal static async Task RunAsync(IServiceProvider provider, IOutboxStore store, OutboundMessage message, byte[] payload, string run, string connection, CancellationToken token)
    {
        await GeneratedAsync(store, message, payload, run, token);
        var missingId = run + "-missing";
        await MissingMetadataAsync(provider, store, message, payload, run, missingId, token);
        await AssertAbsentAsync(connection, run, missingId, token);
    }

    private static async Task GeneratedAsync(IOutboxStore store, OutboundMessage message, byte[] payload, string run, CancellationToken token)
    {
        // The original prepared-byte primitive workload above is preserved. This is an additional owned-client metadata control.
        var generatedId = run + "-generated";
        var generatedValue = "generated-東京-" + run;
        var generated = NewHeaderMessage(message, payload, run, generatedId, new GeneratedHeader { Value = generatedValue });
        await store.StageMessageAsync(generated, token);
        var generatedFound = (await store.GetUnsentMessagesAsync(10, token)).Single(x => x.Id == generatedId);
        var generatedMismatches = ProbeMessageAssertions.MessageMismatches(generated, generatedFound);
        if (!generatedFound.Headers.TryGetValue("custom", out var customHeader))
            generatedMismatches.Add("Headers.custom: expected structuredJSON; actual=<missing>");
        else if (customHeader is not JsonElement custom || custom.ValueKind != JsonValueKind.Object)
            generatedMismatches.Add("Headers.custom: expected JsonElement.Object; actual=" +
                (customHeader is JsonElement wrongKind ? wrongKind.ValueKind.ToString() : "non-JsonElement"));
        else if (!custom.TryGetProperty("value", out var customValue))
            generatedMismatches.Add("Headers.custom.value: expected=" + generatedValue + "; actual=<missing>");
        else if (customValue.ValueKind != JsonValueKind.String || customValue.GetString() != generatedValue)
            generatedMismatches.Add("Headers.custom.value: expected=" + generatedValue + "; actual=" + customValue.GetRawText());
        if (generatedMismatches.Count != 0)
            throw new InvalidOperationException("Generated-header Stage/GetUnsent mismatches: " + string.Join(" | ", generatedMismatches));

        Console.WriteLine("PROBE-PASS generated-header-stage-read");

    }

    private static async Task MissingMetadataAsync(IServiceProvider provider, IOutboxStore store, OutboundMessage message, byte[] payload, string run, string missingId, CancellationToken token)
    {
        // Read the exact SDK client created by the public NodeUri builder, without replacing its source serializer.
        var ownedClient = provider.GetRequiredService<Elastic.Clients.Elasticsearch.ElasticsearchClient>();
        try
        {
            _ = ownedClient.SourceSerializer.SerializeToString(new MissingHeader { Value = "not-generated" });
            throw new InvalidOperationException("Owned source serializer silently accepted a type with absent metadata.");
        }
        catch (NotSupportedException) { }

        Console.WriteLine("PROBE-PASS source-missing-metadata");

        var missing = NewHeaderMessage(message, payload, run, missingId, new MissingHeader { Value = "not-generated" });
        Exception? metadataError = null;
        try
        {
            await store.StageMessageAsync(missing, token);
        }
        catch (Exception error) when (ContainsMissingMetadata(error))
        {
            metadataError = error;
        }
        if (metadataError is null)
            throw new InvalidOperationException("Missing custom-header metadata was accepted by public staging.");

        Console.WriteLine("PROBE-PASS stage-missing-metadata");

    }

    private static async Task AssertAbsentAsync(string connection, string run, string missingId, CancellationToken token)
    {
        // Independent read-only inventory checks the public Stage refusal did not leave a persisted row.
        // This HTTP check is an oracle only: no raw request replaces the provider Stage/GetUnsent workload.
        using var inventory = new HttpClient();
        using var absent = await inventory.GetAsync(
            new Uri(connection.TrimEnd('/') + "/" + Uri.EscapeDataString(run) + "/_doc/" + Uri.EscapeDataString(missingId)), token);
        var absentBody = await absent.Content.ReadAsStringAsync(token);
        using var absentJson = JsonDocument.Parse(absentBody);
        if (absent.StatusCode != System.Net.HttpStatusCode.NotFound ||
            !absentJson.RootElement.TryGetProperty("found", out var wasFound) || wasFound.ValueKind != JsonValueKind.False)
            throw new InvalidOperationException("Missing-metadata staging did not prove document absence with a real404 found=false response.");

        Console.WriteLine("PROBE-PASS no-accepted-missing-header-row");

    }

    private static OutboundMessage NewHeaderMessage(OutboundMessage message, byte[] payload, string run, string id, object value) => new(
        message.MessageType, payload, message.Destination,
        new Dictionary<string, object> { ["probe"] = run, ["custom"] = value })
    {
        Id = id, TenantId = message.TenantId, CorrelationId = message.CorrelationId,
        CausationId = message.CausationId, PartitionKey = message.PartitionKey,
        GroupKey = message.GroupKey, SequenceNumber = message.SequenceNumber
    };

    private static bool ContainsMissingMetadata(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            // Do not treat cancellation, a generic transport exception or an invalid server response as this control.
            if (current is NotSupportedException && current.Message.Contains(nameof(MissingHeader), StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
