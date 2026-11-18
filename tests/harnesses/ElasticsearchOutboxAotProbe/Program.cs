// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

if (RuntimeFeature.IsDynamicCodeSupported || JsonSerializer.IsReflectionEnabledByDefault)
    throw new InvalidOperationException("Run the actual native executable with reflection JSON disabled.");
Console.WriteLine("PROBE-NATIVE dynamic-code=false json-reflection=false");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var token = timeout.Token;
var run = Environment.GetEnvironmentVariable("PROBE_RUN_ID")
    ?? throw new InvalidOperationException("Root must supply a fresh PROBE_RUN_ID and isolated provider namespace.");
var connection = Environment.GetEnvironmentVariable("PROBE_CONNECTION")
    ?? throw new InvalidOperationException("Root must supply PROBE_CONNECTION for a dedicated test fixture.");
var services = new ServiceCollection();
services.AddLogging();
services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseElasticSearch(elastic =>
    elastic.NodeUri(new Uri(connection)).IndexName(run).SourceSerializer(ProbeHeaderJson.Default))));
await using var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<IOutboxStore>();
var payload = new byte[] { 0, 1, 2, 255 };
var message = new OutboundMessage("native-preencoded-event", payload, "native-sink",
    new Dictionary<string, object> { ["probe"] = run })
{
    Id = run, TenantId = "native-probe-tenant", CorrelationId = "native-probe-correlation",
    CausationId = "native-probe-causation", PartitionKey = "native-probe-partition",
    GroupKey = "native-probe-group", SequenceNumber = 7
};
await store.StageMessageAsync(message, token);
var found = (await store.GetUnsentMessagesAsync(10, token)).Single(x => x.Id == run);
var primitiveMismatches = ProbeMessageAssertions.MessageMismatches(message, found);
if (!found.Headers.TryGetValue("probe", out var primitiveHeader))
    primitiveMismatches.Add("Headers.probe: expected=" + run + "; actual=<missing>");
else
{
    var header = primitiveHeader is JsonElement json ? json.GetString() : primitiveHeader?.ToString();
    if (header != run) primitiveMismatches.Add("Headers.probe: expected=" + run + "; actual=" + header);
}
if (primitiveMismatches.Count != 0)
    throw new InvalidOperationException("Primitive Stage/GetUnsent mismatches: " + string.Join(" | ", primitiveMismatches));
Console.WriteLine("PROBE-PASS primitive-stage-read");

await HeaderMetadataProbe.RunAsync(provider, store, message, payload, run, connection, token);
Console.WriteLine("PROBE-COMPLETE ElasticsearchOutboxAotProbe checks=5");
