// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

// Package-free regression controls against the actual compiled benchmark fixtures.
// Reflection is confined to this verification tool; it is not shipping/AOT code.
var bundle = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(bundle, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
var scenarios = new[]
{
    ("OutboxStagingBenchmarks", "StageSingleSmall", 1, 1024, "Staged"),
    ("OutboxStagingBenchmarks", "StageSingleMedium", 1, 10 * 1024, "Staged"),
    ("OutboxStagingBenchmarks", "StageSingleLarge", 1, 100 * 1024, "Staged"),
    ("OutboxStagingBenchmarks", "StageBatch10", 10, 1024, "Staged"),
    ("OutboxStagingBenchmarks", "StageBatch100", 100, 1024, "Staged"),
    ("OutboxStagingBenchmarks", "StageLargePayload", 1, 1024 * 1024, "Staged"),
    ("OutboxPublishingBenchmarks", "StageMessage", 1, 1024, "Staged"),
    ("OutboxPublishingBenchmarks", "FullPublishCycle", 1, 1024, "Sent"),
    ("OutboxPublishingBenchmarks", "StageThenProcessBatch100", 100, 1024, "Sent"),
    ("OutboxPublishingBenchmarks", "GetStatistics", 0, 0, "Staged"),
};
long baseline = GC.GetTotalMemory(true), maxLive = baseline;
var reports = new List<object>();
foreach (var (className, methodName, count, payloadBytes, status) in scenarios)
{
    var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns." + className, true)!;
    var invocation = type.CustomAttributes.Single(a => a.AttributeType.Name == "InvocationCountAttribute");
    Require((int)invocation.ConstructorArguments[0].Value! == 1 &&
        (int)invocation.ConstructorArguments[1].Value! == 1, "Invocation/unroll must both be one.");
    var setup = type.GetMethod("IterationSetup")!;
    var cleanup = type.GetMethod("IterationCleanup")!;
    Require(setup.CustomAttributes.Any(a => a.AttributeType.Name == "IterationSetupAttribute"), "Missing setup hook.");
    Require(cleanup.CustomAttributes.Any(a => a.AttributeType.Name == "IterationCleanupAttribute"), "Missing cleanup hook.");
    var fixture = Activator.CreateInstance(type)!;
    var storeField = type.GetField("_outboxStore", BindingFlags.Instance | BindingFlags.NonPublic)!;
    setup.Invoke(fixture, null);
    cleanup.Invoke(fixture, null);
    cleanup.Invoke(fixture, null);
    Require(storeField.GetValue(fixture) is null, "Hook-jitting cleanup retained its store.");
    for (var cycle = 0; cycle < 128; cycle++)
    {
        setup.Invoke(fixture, null);
        var store = storeField.GetValue(fixture)!;
        var rowsField = store.GetType().GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rows = (IDictionary)rowsField.GetValue(store)!;
        Require(rows.Count == 0, "Prior rows escaped into a fresh window.");
        var outcome = type.GetMethod(methodName)!.Invoke(fixture, null);
        if (outcome is Task task) await task;
        else if (outcome is ValueTask valueTask) await valueTask;
        else throw new InvalidOperationException("Unexpected benchmark return shape.");
        if (methodName == "StageThenProcessBatch100")
            Require((int)outcome.GetType().GetProperty("Result")!.GetValue(outcome)! == 100, "Batch did not process 100 messages.");
        if (methodName == "GetStatistics")
            Require((long)outcome.GetType().GetProperty("Result")!.GetValue(outcome)! == 0, "Empty statistics changed.");
        Require(rows.Count == count, $"Unexpected message count in {methodName}.");
        var ids = new HashSet<string>();
        foreach (var row in rows.Values)
        {
            var rowType = row!.GetType();
            object? Get(string name) => rowType.GetProperty(name)!.GetValue(row);
            Require(ids.Add((string)Get("Id")!), "Duplicate message identity.");
            Require(((byte[])Get("Payload")!).Length == payloadBytes, "Payload size changed.");
            Require(Get("Status")!.ToString() == status, "Delivery status changed.");
            Require((string)Get("MessageType")! == "TestEvent" && (string)Get("Destination")! == "test-destination", "Envelope changed.");
            var headers = (IDictionary)Get("Headers")!;
            Require(Guid.TryParse((string?)headers["CorrelationId"], out _), "Correlation identity lost.");
            if (className == "OutboxStagingBenchmarks")
                Require((string?)headers["UserId"] == "benchmark-user" && (string?)headers["TenantId"] == "benchmark-tenant", "Scope headers changed.");
        }
        // A lifecycle misuse must fail rather than silently losing a prior store.
        try { setup.Invoke(fixture, null); throw new Exception("Double setup accepted."); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
        cleanup.Invoke(fixture, null);
        Require(storeField.GetValue(fixture) is null && rows.Count == 0, "Cleanup retained messages/store.");
        cleanup.Invoke(fixture, null); // Idempotent cleanup after a real workload.
        if (cycle % 16 == 0)
        {
            var live = GC.GetTotalMemory(true);
            maxLive = Math.Max(maxLive, live);
            Require(live - baseline < 64L * 1024 * 1024, "Live managed memory exceeded bounded verification budget.");
        }
    }
    reports.Add(new { className, methodName, cycles = 128, expectedRows = count, payloadBytes, status });
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, cycles = 1280, baselineLiveBytes = baseline,
    maxLiveBytes = maxLive, liveGrowthBytes = maxLive - baseline, scenarios = reports }));

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
