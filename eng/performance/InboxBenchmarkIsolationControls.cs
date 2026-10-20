// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Untimed, package-free controls for the compiled fixture. Reflection is verification-only;
// this tool is neither shipping code nor an AOT/performance measurement.
internal static class Program
{
    private const int Cycles = 16;
    private const int WarmupCycles = 2;
    private const long GrowthLimit = 64L * 1024 * 1024;
    private static readonly string[] WriteMethods = ["CreateEntry", "FullProcessingCycle"];
    private static readonly int[] Counts = [100, 1000, 10000];
    private static readonly HashSet<string> PriorWrites = new(StringComparer.Ordinal);
    private static int _negativeChecks;

    public static async Task Main(string[] args)
    {
        Require(args.Length == 1, "Supply exactly one compiled benchmark runtime bundle.");
        var bundle = Path.GetFullPath(args[0]);
        var before = PinFiles(bundle);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(bundle, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
        var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns.InboxThroughputBenchmarks", true)!;
        CheckHooks(type);
        var reports = new List<object>();
        long baseline = 0, maxLive = 0;
        foreach (var method in WriteMethods)
        foreach (var count in Counts)
        {
            // Warm both workload paths and every cardinality before starting its bounded samples.
            for (var i = 0; i < WarmupCycles; i++) await RunCycle(type, method, count, false);
            var scenarioBaseline = GC.GetTotalMemory(true);
            if (baseline == 0) baseline = scenarioBaseline;
            maxLive = Math.Max(maxLive, scenarioBaseline);
            var scenarioMax = scenarioBaseline;
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                await RunCycle(type, method, count, cycle == 0);
                var live = GC.GetTotalMemory(true);
                maxLive = Math.Max(maxLive, live);
                scenarioMax = Math.Max(scenarioMax, live);
                Require(live - scenarioBaseline < GrowthLimit, "Post-cleanup live managed heap exceeded the scoped 64 MiB growth budget.");
            }
            reports.Add(new { method, messageCount = count, warmupCycles = WarmupCycles, verifiedCycles = Cycles,
                scenarioBaselineLiveBytes = scenarioBaseline, scenarioMaxLiveBytes = scenarioMax,
                scenarioLiveGrowthBytes = scenarioMax - scenarioBaseline });
        }
        Require(reports.Count == 6 && _negativeChecks == 24, "Incomplete scenario/negative-control population.");
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true)
            .Select(a => LoadedPin(a, bundle, before)).OrderBy(p => p.relativePath, StringComparer.Ordinal).ToArray();
        Require(loaded.Any(p => p.relativePath == "Excalibur.Dispatch.Benchmarks.dll") &&
            loaded.Any(p => p.relativePath == "Excalibur.Inbox.InMemory.dll"), "Expected actual project modules were not loaded.");
        var after = PinFiles(bundle);
        Require(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var value) && value == p.Value),
            "Runtime bundle files changed during controls.");
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "inbox-benchmark-isolation-controls-r1", passed = true,
            scenarios = reports, scenarioCount = 6, cyclesPerScenario = Cycles, verifiedCycles = Cycles * 6,
            warmupCycles = WarmupCycles * 6, rejectingNegativeControls = _negativeChecks,
            baselineLiveBytes = baseline, maxLiveBytes = maxLive, liveGrowthBytes = maxLive - baseline,
            scopedScenarioGrowthLimitBytes = GrowthLimit, runtimeFilesBefore = before, runtimeFilesAfter = after,
            loadedProjectModules = loaded, runtimeBundleUnchanged = true, nativeAotVerified = false,
            timingOrThroughputMeasured = false, fullSuiteComplete = false, allTimedIterationsValidated = false,
            timerCallbackQuiescenceVerified = false, universalMemoryBoundVerified = false,
            scope = "Six original write-method/cardinality cases,16 post-warmup isolated cycles each. Full GlobalSetup and targeted setup retain default cleanup timer and production retention. Validated public entry snapshots, fresh stores, cleanup and disposed-store behavior. Sampled post-cleanup live managed heap is a bounded diagnostic; Timer.Dispose does not prove queued callback quiescence. No timed child, NativeAOT or universal retention claim." }));
    }

    private static void CheckHooks(Type type)
    {
        var setup = type.GetMethod("SetupWriteIteration")!;
        var cleanup = type.GetMethod("CleanupWriteIteration")!;
        foreach (var pair in new[] { (setup, "IterationSetupAttribute"), (cleanup, "IterationCleanupAttribute") })
        {
            var attribute = pair.Item1.CustomAttributes.Single(a => a.AttributeType.Name == pair.Item2);
            var targets = (IEnumerable<CustomAttributeTypedArgument>)attribute.NamedArguments.Single(a => a.MemberName == "Targets").TypedValue.Value!;
            var names = targets.Select(a => (string)a.Value!).ToArray();
            Require(names.Length == 2 && names.ToHashSet(StringComparer.Ordinal).SetEquals(WriteMethods), "Write hook targets must be exactly the two write methods.");
        }
        Require(type.GetMethod("GlobalSetup")!.CustomAttributes.Count(a => a.AttributeType.Name == "GlobalSetupAttribute") == 1 &&
            type.GetMethod("GlobalCleanup")!.CustomAttributes.Count(a => a.AttributeType.Name == "GlobalCleanupAttribute") == 1, "Missing complete global lifecycle hooks.");
        Require(!type.CustomAttributes.Any(a => a.AttributeType.Name is "InvocationCountAttribute" or "UnrollFactorAttribute"),
            "Read-only adaptive jobs must not receive a class-wide invocation/unroll override.");
        var paramsAttribute = type.GetProperty("MessageCount")!.CustomAttributes.Single(a => a.AttributeType.Name == "ParamsAttribute");
        var values = (IEnumerable<CustomAttributeTypedArgument>)paramsAttribute.ConstructorArguments.Single().Value!;
        Require(values.Select(a => Convert.ToInt32(a.Value)).Order().SequenceEqual(Counts), "Original three cardinalities changed.");
        var hooks = type.GetMethods().SelectMany(m => m.CustomAttributes.Where(a => a.AttributeType.Name is "IterationSetupAttribute" or "IterationCleanupAttribute").Select(a => (m, a))).ToArray();
        Require(hooks.Length == 2, "Unexpected additional iteration hooks could affect read cases.");
        foreach (var read in new[] { "GetAllEntries", "GetStatistics", "IsAlreadyProcessed" })
            Require(type.GetMethod(read)!.CustomAttributes.Any(a => a.AttributeType.Name == "BenchmarkAttribute"), "Original read benchmark missing.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task RunCycle(Type type, string method, int count, bool negativeControls)
    {
        var fixture = Activator.CreateInstance(type)!;
        type.GetProperty("MessageCount")!.SetValue(fixture, count);
        var storeField = type.GetField("_inboxStore", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var globalSetup = type.GetMethod("GlobalSetup")!;
        var globalCleanup = type.GetMethod("GlobalCleanup")!;
        var setup = type.GetMethod("SetupWriteIteration")!;
        var cleanup = type.GetMethod("CleanupWriteIteration")!;
        try
        {
            globalSetup.Invoke(fixture, null);
            var initial = storeField.GetValue(fixture)!;
            Validate(await Snapshot(initial), count, false, "Received", PriorWrites);
            setup.Invoke(fixture, null);
            var store = storeField.GetValue(fixture)!;
            Require(!ReferenceEquals(initial, store), "Targeted setup reused the global seed store.");
            AssertDisposed(initial);
            var seedRows = await Snapshot(store);
            Validate(seedRows, count, false, "Received", PriorWrites);
            var outcome = type.GetMethod(method)!.Invoke(fixture, null);
            Require(outcome is Task, "Write benchmark must retain its awaited Task return shape.");
            await (Task)outcome!;
            var rows = await Snapshot(store);
            AssertSeedsUnchanged(seedRows, rows);
            var expectedStatus = method == "FullProcessingCycle" ? "Processed" : "Received";
            var written = Validate(rows, count, true, expectedStatus, PriorWrites)!;
            if (negativeControls)
            {
                Reject(() => Validate(rows[..^1], count, true, expectedStatus, PriorWrites), "wrong count");
                var writeIndex = Array.FindIndex(rows, r => r.MessageId == written);
                var wrongStatus = (Row[])rows.Clone();
                wrongStatus[writeIndex] = rows[writeIndex] with { Status = expectedStatus == "Received" ? "Processed" : "Received" };
                Reject(() => Validate(wrongStatus, count, true, expectedStatus, PriorWrites), "wrong status");
                var badPayload = (byte[])rows[writeIndex].Payload.Clone(); badPayload[0] = 1;
                var wrongPayload = (Row[])rows.Clone(); wrongPayload[writeIndex] = rows[writeIndex] with { Payload = badPayload };
                Reject(() => Validate(wrongPayload, count, true, expectedStatus, PriorWrites), "wrong payload");
                Require(PriorWrites.Count > 0, "Warmup must produce an actual old GUID for the stale-row negative control.");
                var stale = (Row[])rows.Clone(); stale[writeIndex] = rows[writeIndex] with { MessageId = PriorWrites.First() };
                Reject(() => Validate(stale, count, true, expectedStatus, PriorWrites), "old GUID refill");
            }
            Require(PriorWrites.Add(written), "Write GUID repeated across isolated cycles.");
            cleanup.Invoke(fixture, null);
            Require(storeField.GetValue(fixture) is null, "Cleanup retained the store field.");
            AssertDisposed(store);
            cleanup.Invoke(fixture, null);
            Require(storeField.GetValue(fixture) is null, "Repeated cleanup changed the cleared field.");
            // A real next setup proves that cleanup followed by refill restores precisely N seeds.
            setup.Invoke(fixture, null);
            var next = storeField.GetValue(fixture)!;
            Require(!ReferenceEquals(store, next), "Next setup reused the disposed store.");
            Validate(await Snapshot(next), count, false, "Received", PriorWrites);
            cleanup.Invoke(fixture, null);
            Require(storeField.GetValue(fixture) is null, "Next cleanup retained the store.");
            AssertDisposed(next);
        }
        finally
        {
            globalCleanup.Invoke(fixture, null);
            Require(storeField.GetValue(fixture) is null, "Global cleanup retained the store.");
        }
    }

    private static string? Validate(Row[] rows, int count, bool hasWrite, string writeStatus, HashSet<string> forbidden)
    {
        Require(rows.Length == count + (hasWrite ? 1 : 0), "Entry count differs.");
        var byId = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            Require(byId.TryAdd(row.MessageId, row) && !forbidden.Contains(row.MessageId), "Duplicate or stale message GUID.");
            Require(row.HandlerType == "TestHandler" && row.MessageType == "TestEvent", "Handler/message scope changed.");
            Require(row.Payload.Length == 1024 && row.Payload.All(b => b == 0), "Original payload bytes changed.");
            Require(Guid.TryParseExact(row.MetadataCorrelationId, "D", out _) && row.CorrelationId == row.MetadataCorrelationId, "Correlation metadata/property lost.");
            Require(row.RetryCount == 0 && row.LastError is null, "Unexpected failure/retry state.");
        }
        for (var i = 0; i < count; i++)
        {
            Require(byId.Remove("msg-" + i, out var row), "Seed identifier missing.");
            var seed = row ?? throw new InvariantFailure("Seed row was null.");
            Require(seed.Status == "Received" && seed.ProcessedAt is null && seed.MetadataCount == 3 &&
                seed.UserId == "benchmark-user" && seed.TenantId == "benchmark-tenant", "Seed status or metadata changed.");
        }
        if (!hasWrite) { Require(byId.Count == 0, "Unexpected row in fresh seed store."); return null; }
        Require(byId.Count == 1, "Write did not add exactly one entry.");
        var added = byId.Values.Single();
        Require(Guid.TryParseExact(added.MessageId, "D", out _) && added.Status == writeStatus &&
            (added.ProcessedAt is not null) == (writeStatus == "Processed") && added.MetadataCount == 1 &&
            added.UserId is null && added.TenantId is null, "Written identity, processing transition or metadata changed.");
        return added.MessageId;
    }

    private static void AssertSeedsUnchanged(Row[] before, Row[] after)
    {
        var byId = after.ToDictionary(row => row.MessageId, StringComparer.Ordinal);
        foreach (var seed in before)
        {
            Require(byId.TryGetValue(seed.MessageId, out var current), "Original seed disappeared after write.");
            var retained = current ?? throw new InvariantFailure("Retained seed was null.");
            Require(seed.Payload.SequenceEqual(retained.Payload) && seed == (retained with { Payload = seed.Payload }),
                "Write altered a seeded row's original fields, timestamps, bytes or correlation metadata.");
        }
    }

    private static async Task<Row[]> Snapshot(object store)
    {
        var pending = store.GetType().GetMethod("GetAllTenantsEntriesAsync")!.Invoke(store, [CancellationToken.None])!;
        var task = (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
        await task;
        var entries = (IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
        return entries.Cast<object>().Select(entry =>
        {
            object? Get(string name) => entry.GetType().GetProperty(name)!.GetValue(entry);
            var metadata = (IDictionary)Get("Metadata")!;
            string? MetadataString(string key) => metadata[key] is string value ? value : metadata[key] is null ? null : throw new InvariantFailure("Non-string original metadata.");
            return new Row((string)Get("MessageId")!, (string)Get("HandlerType")!, (string)Get("MessageType")!,
                ((byte[])Get("Payload")!).ToArray(), Get("Status")!.ToString()!, (DateTimeOffset)Get("ReceivedAt")!, (DateTimeOffset?)Get("ProcessedAt"),
                (string?)Get("CorrelationId"), metadata.Count, MetadataString("UserId"), MetadataString("TenantId"),
                MetadataString("CorrelationId"), (int)Get("RetryCount")!, (string?)Get("LastError"));
        }).ToArray();
    }

    private static void AssertDisposed(object store)
    {
        var entries = (IDictionary)store.GetType().GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
        Require(entries.Count == 0 && (bool)store.GetType().GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!, "Disposed store retained entries or was not disposed.");
        try { store.GetType().GetMethod("GetAllTenantsEntriesAsync")!.Invoke(store, [CancellationToken.None]); }
        catch (TargetInvocationException e) when (e.InnerException is ObjectDisposedException) { return; }
        throw new InvariantFailure("Disposed store accepted a public read.");
    }

    private static Dictionary<string, FilePin> PinFiles(string bundle)
    {
        Require(Directory.Exists(bundle), "Runtime bundle missing.");
        var result = new Dictionary<string, FilePin>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(bundle);
        while (pending.TryPop(out var directory))
        {
            Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Reparse bundle directory refused.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                Require((attributes & FileAttributes.ReparsePoint) == 0, "Reparse runtime member refused.");
                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                var relative = Path.GetRelativePath(bundle, path).Replace('\\', '/');
                result.Add(relative, new FilePin(new FileInfo(path).Length, Hash(path)));
            }
        }
        Require(result.Count > 0, "Empty runtime bundle.");
        return result;
    }

    private static LoadedProjectPin LoadedPin(Assembly assembly, string bundle, Dictionary<string, FilePin> before)
    {
        var path = Path.GetFullPath(assembly.Location);
        var relative = Path.GetRelativePath(bundle, path).Replace('\\', '/');
        Require(!Path.IsPathRooted(relative) && !relative.StartsWith("../", StringComparison.Ordinal) &&
            before.TryGetValue(relative, out var expected) && new FileInfo(path).Length == expected.length && Hash(path) == expected.sha256,
            "Loaded project module is outside or differs from the selected runtime bundle.");
        return new LoadedProjectPin(relative, Hash(path), assembly.ManifestModule.ModuleVersionId);
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Reject(Action validator, string label)
    {
        try { validator(); }
        catch (InvariantFailure) { _negativeChecks++; return; }
        throw new InvariantFailure("Negative validator accepted " + label + ".");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvariantFailure(message); }
    private sealed class InvariantFailure(string message) : Exception(message) { }
    private sealed record FilePin(long length, string sha256);
    private sealed record LoadedProjectPin(string relativePath, string sha256, Guid moduleVersionId);
    private sealed record Row(string MessageId, string HandlerType, string MessageType, byte[] Payload, string Status,
        DateTimeOffset ReceivedAt, DateTimeOffset? ProcessedAt, string? CorrelationId, int MetadataCount, string? UserId, string? TenantId,
        string? MetadataCorrelationId, int RetryCount, string? LastError);
}