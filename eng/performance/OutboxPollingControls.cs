// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

// Untimed actual-fixture controls; reflection is verification-only, not shipping/AOT code.
internal static class Program
{
    private static readonly string[] Methods = ["PollBatch10", "PollBatch100", "PollBatch500"];
    private static readonly int[] Counts = [0, 10, 100, 1000];
    private const int Cycles = 4;
    private const long GrowthLimit = 64L * 1024 * 1024;
    private static int _negativeChecks;
    private sealed class InvariantFailure(string message) : Exception(message);
    private sealed record Pin(long length, string sha256);
    private sealed record Row(string Id, object Original, byte[] Payload, string MessageType,
        string Destination, string Status, string? DispatcherId, int HeaderCount, int Index, string CorrelationId);
    private sealed record Lease(string Id, DateTimeOffset At, string By);
    private sealed record State(Row[] Rows, Lease[] Leases);

    public static async Task Main(string[] args)
    {
        Require(args.Length == 1, "Exactly one compiled benchmark bundle required.");
        var bundle = Path.GetFullPath(args[0]); var before = Pins(bundle);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(bundle, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
        var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns.OutboxPollingBenchmarks", true)!;
        var cadence = Discover(type); var scenarios = new List<object>();
        foreach (var method in Methods)
        foreach (var count in Counts)
        {
            var fixture = Activator.CreateInstance(type)!; type.GetProperty("MessageCount")!.SetValue(fixture, count);
            var field = Field(type, "_outboxStore"); var seen = new HashSet<string>(StringComparer.Ordinal);
            var batch = method == "PollBatch10" ? 10 : method == "PollBatch100" ? 100 : 500;
            long baseline = 0, max = 0;
            try
            {
                type.GetMethod("GlobalSetup")!.Invoke(fixture, null);
                var prior = field.GetValue(fixture)!; Validate(Read(prior), count, 0, seen, Processor(prior));
                foreach (var row in Read(prior).Rows) Require(seen.Add(row.Id), "Initial GUID collision.");
                for (var cycle = -1; cycle < Cycles; cycle++)
                {
                    type.GetMethod("SetupPollIteration")!.Invoke(fixture, null);
                    var store = field.GetValue(fixture)!;
                    Require(!ReferenceEquals(prior, store), "Iteration reused a previously seeded store."); AssertDisposed(prior);
                    CheckOptions(store); var seeded = Read(store); var processor = Processor(store);
                    Validate(seeded, count, 0, seen, processor);
                    var task = (Task)type.GetMethod(method)!.Invoke(fixture, null)!; await task;
                    var returned = Convert.ToInt32(task.GetType().GetProperty("Result")!.GetValue(task));
                    var claimed = Math.Min(count, batch); Require(returned == claimed, "Original poll returned wrong count.");
                    var first = Read(store); Validate(first, count, claimed, seen, processor); SameSeeds(seeded, first);
                    if (cycle == 0)
                    {
                        Reject(() => Validate(first, count + 1, claimed, seen, processor), "wrong seed N");
                        Reject(() => Validate(first, count, claimed + 1, seen, processor), "wrong claimed count");
                        if (count > 0)
                        {
                            var staleRows = (Row[])first.Rows.Clone(); staleRows[0] = staleRows[0] with { Id = seen.First() };
                            Reject(() => Validate(first with { Rows = staleRows }, count, claimed, seen, processor), "stale seed GUID");
                            var badRows = (Row[])first.Rows.Clone(); var bytes = (byte[])badRows[0].Payload.Clone(); bytes[0] = 1;
                            badRows[0] = badRows[0] with { Payload = bytes };
                            Reject(() => Validate(first with { Rows = badRows }, count, claimed, seen, processor), "changed payload");
                        }
                    }
                    // Separate untimed probe obtains actual returned IDs, not an inference from counts.
                    var second = await Probe(store, batch); var firstIds = first.Leases.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
                    Require(second.Length == Math.Min(batch, count - claimed), "Second probe returned wrong remaining count.");
                    var secondIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var message in second)
                    {
                        var id = (string)message.GetType().GetProperty("Id")!.GetValue(message)!;
                        Require(secondIds.Add(id) && !firstIds.Contains(id), "Second probe duplicated a still-leased ID.");
                        Require(seeded.Rows.Any(row => row.Id == id && ReferenceEquals(row.Original, message)), "Probe returned an unseeded or substituted message.");
                    }
                    var afterProbe = Read(store); Validate(afterProbe, count, claimed + second.Length, seen, processor); SameSeeds(seeded, afterProbe);
                    Require(afterProbe.Leases.Select(l => l.Id).ToHashSet(StringComparer.Ordinal).SetEquals(firstIds.Concat(secondIds)), "Lease union differs from actual first/second claims.");
                    foreach (var row in seeded.Rows) Require(seen.Add(row.Id), "Fresh GUID repeated across setups.");
                    type.GetMethod("CleanupPollIteration")!.Invoke(fixture, null);
                    Require(field.GetValue(fixture) is null, "Cleanup retained fixture store."); AssertDisposed(store);
                    type.GetMethod("CleanupPollIteration")!.Invoke(fixture, null); Require(field.GetValue(fixture) is null, "Repeated cleanup retained store.");
                    prior = store;
                    var live = GC.GetTotalMemory(true);
                    if (cycle == -1) baseline = live;
                    else { max = Math.Max(max, live); Require(live - baseline < GrowthLimit, "Scoped post-cleanup managed growth exceeded64 MiB."); }
                }
                scenarios.Add(new { method, messageCount = count, batchSize = batch, expectedFirstClaim = Math.Min(count, batch),
                    verifiedCycles = Cycles, warmupCycles = 1, baselineLiveBytes = baseline, maxLiveBytes = max, liveGrowthBytes = max - baseline });
            }
            finally { type.GetMethod("GlobalCleanup")!.Invoke(fixture, null); Require(field.GetValue(fixture) is null, "Global cleanup retained store."); }
        }
        Require(scenarios.Count == 12 && _negativeChecks == 42, "Exact12 scenarios/42 rejecting controls required.");
        var after = Pins(bundle); Require(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var value) && value == p.Value), "Runtime bundle changed.");
        var modules = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true).Select(a =>
        {
            var relative = Path.GetRelativePath(bundle, a.Location).Replace('\\', '/');
            Require(before.TryGetValue(relative, out var pin) && pin.sha256 == Sha(a.Location), "Loaded project module outside/different from bundle.");
            return new { relativePath = relative, sha256 = Sha(a.Location), mvid = a.ManifestModule.ModuleVersionId };
        }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "outbox-polling-isolation-controls-r1", passed = true,
            scenarioCount = 12, verifiedCycles = 48, warmupCycles = 12, rejectingNegativeControls = _negativeChecks,
            cadence, scenarios, runtimeFilesBefore = before, runtimeFilesAfter = after, loadedProjectModules = modules,
            runtimeBundleUnchanged = true, nativeAotVerified = false, timingOrThroughputMeasured = false,
            allTimedIterationsValidated = false, universalMemoryBoundVerified = false, fullSuiteComplete = false,
            scope = "12 fixture cases,4 post-warmup cycles each. Exact original first-poll count, independent seeded message/lease snapshots, DispatcherId, second-probe returned ID disjointness, fresh GUIDs/store replacement and disposal checked. Options retain lease120s/MaxMessages0 and original1024-byte payload/metadata. Finite post-cleanup heap budget only; no timed output, faulted reset, concurrency/fencing, AOT, universal retention or performance claim." }));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object[] Discover(Type type)
    {
        Require(!type.CustomAttributes.Any(a => a.AttributeType.Name is "InvocationCountAttribute" or "UnrollFactorAttribute"), "Class-wide override refused.");
        var rows = new List<object>(); var identities = new HashSet<string>(StringComparer.Ordinal); int polls = 0, stats = 0;
        foreach (var item in BenchmarkConverter.TypeToBenchmarks(type).BenchmarksCases)
        {
            var method = item.Descriptor.WorkloadMethod.Name; var poll = Methods.Contains(method, StringComparer.Ordinal);
            Require(poll || method == "GetStatistics", "Unexpected benchmark method.");
            var parameter = item.Parameters.Items.Single(); var count = Convert.ToInt32(parameter.Value);
            Require(parameter.Name == "MessageCount" && Counts.Contains(count), "Original parameter population changed.");
            Require(identities.Add(method + "|" + count), "Duplicate converted benchmark identity.");
            var setup = (MethodInfo?)item.Descriptor.GetType().GetProperty("IterationSetupMethod")!.GetValue(item.Descriptor);
            var cleanup = (MethodInfo?)item.Descriptor.GetType().GetProperty("IterationCleanupMethod")!.GetValue(item.Descriptor);
            Require(setup?.Name == (poll ? "SetupPollIteration" : null) && cleanup?.Name == (poll ? "CleanupPollIteration" : null), "Converted lifecycle targets differ.");
            var explicitInvocation = item.Job.Run.HasValue(RunMode.InvocationCountCharacteristic);
            var explicitUnroll = item.Job.Run.HasValue(RunMode.UnrollFactorCharacteristic);
            if (poll) { Require(explicitInvocation && explicitUnroll && item.Job.Run.InvocationCount == 1 && item.Job.Run.UnrollFactor == 1, "Poll not isolated1/1."); polls++; }
            else { Require(!explicitInvocation && !explicitUnroll, "Statistics original adaptive cadence changed."); stats++; }
            rows.Add(new { method, messageCount = count, poll, explicitInvocation, explicitUnroll,
                invocationCount = explicitInvocation ? item.Job.Run.InvocationCount : (long?)null,
                unrollFactor = explicitUnroll ? item.Job.Run.UnrollFactor : (int?)null });
        }
        Require(polls == 12 && stats == 4 && rows.Count == 16, "Exact12 poll/4 statistics cases required."); return rows.ToArray();
    }

    private static void CheckOptions(object store)
    {
        var options = Field(store.GetType(), "_options").GetValue(store)!;
        Require((int)options.GetType().GetProperty("LeaseTimeoutSeconds")!.GetValue(options)! == 120 &&
            (int)options.GetType().GetProperty("MaxMessages")!.GetValue(options)! == 0, "Original lease/capacity options changed.");
    }
    private static string Processor(object store) => (string)Field(store.GetType(), "_processorId").GetValue(store)!;
    private static State Read(object store)
    {
        var messages = (IDictionary)Field(store.GetType(), "_messages").GetValue(store)!;
        var rows = messages.Values.Cast<object>().Select(message =>
        {
            object? Get(string name) => message.GetType().GetProperty(name)!.GetValue(message);
            var headers = (IDictionary)Get("Headers")!;
            return new Row((string)Get("Id")!, message, ((byte[])Get("Payload")!).ToArray(), (string)Get("MessageType")!,
                (string)Get("Destination")!, Get("Status")!.ToString()!, (string?)Get("DispatcherId"), headers.Count,
                (int)headers["Index"]!, (string)headers["CorrelationId"]!);
        }).ToArray();
        var leaseMap = (IDictionary)Field(store.GetType(), "_leases").GetValue(store)!;
        var leases = leaseMap.Keys.Cast<object>().Select(key =>
        {
            var value = leaseMap[key]!; return new Lease((string)key, (DateTimeOffset)value.GetType().GetField("Item1")!.GetValue(value)!,
                (string)value.GetType().GetField("Item2")!.GetValue(value)!);
        }).ToArray();
        return new State(rows, leases);
    }
    private static void Validate(State state, int expectedN, int expectedClaims, HashSet<string> forbidden, string processor)
    {
        Require(state.Rows.Length == expectedN && state.Leases.Length == expectedClaims, "Seed/claim count differs.");
        Require(state.Rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == expectedN &&
            state.Rows.Select(r => r.Index).Order().SequenceEqual(Enumerable.Range(0, expectedN)), "Seed identity/index population differs.");
        var leases = state.Leases.ToDictionary(l => l.Id, StringComparer.Ordinal);
        foreach (var row in state.Rows)
        {
            Require(Guid.TryParse(row.Id, out _) && !forbidden.Contains(row.Id), "Malformed/stale seed GUID.");
            Require(row.Payload.Length == 1024 && row.Payload.All(b => b == 0) && row.MessageType == "TestEvent" &&
                row.Destination == "test-destination" && row.Status == "Staged" && row.HeaderCount == 2 && Guid.TryParse(row.CorrelationId, out _), "Original payload/envelope/metadata changed.");
            if (leases.Remove(row.Id, out var lease)) Require(row.DispatcherId == processor && lease.By == processor && lease.At <= DateTimeOffset.UtcNow, "Lease/DispatcherId claim identity differs.");
            else Require(row.DispatcherId is null, "Unleased row carried a dispatcher claim.");
        }
        Require(leases.Count == 0, "Lease names an unseeded row.");
    }
    private static void SameSeeds(State before, State after)
    {
        var rows = after.Rows.ToDictionary(r => r.Id, StringComparer.Ordinal);
        foreach (var row in before.Rows) Require(rows.TryGetValue(row.Id, out var retained) && ReferenceEquals(row.Original, retained.Original) &&
            row.Payload.SequenceEqual(retained.Payload) && row == (retained with { Payload = row.Payload, DispatcherId = row.DispatcherId }), "Poll altered/substituted original seed fields.");
    }
    private static async Task<object[]> Probe(object store, int batch)
    {
        var pending = store.GetType().GetMethod("GetUnsentMessagesAsync", [typeof(int), typeof(CancellationToken)])!.Invoke(store, [batch, CancellationToken.None])!;
        var task = (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!; await task;
        return ((IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!).Cast<object>().ToArray();
    }
    private static void AssertDisposed(object store)
    {
        var state = Read(store); Require(state.Rows.Length == 0 && state.Leases.Length == 0 && (bool)Field(store.GetType(), "_disposed").GetValue(store)!, "Old store retained rows/leases or was not disposed.");
        try { store.GetType().GetMethod("GetUnsentMessagesAsync", [typeof(int), typeof(CancellationToken)])!.Invoke(store, [1, CancellationToken.None]); }
        catch (TargetInvocationException e) when (e.InnerException is ObjectDisposedException) { return; }
        throw new InvariantFailure("Disposed store accepted a poll.");
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Reject(Action validator, string label) { try { validator(); } catch (InvariantFailure) { _negativeChecks++; return; } throw new InvariantFailure("Negative control accepted " + label); }
    private static Dictionary<string, Pin> Pins(string root)
    {
        var result = new Dictionary<string, Pin>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Reparse runtime directory refused.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path); Require((attributes & FileAttributes.ReparsePoint) == 0, "Reparse runtime member refused.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                else result.Add(Path.GetRelativePath(root, path).Replace('\\', '/'), new Pin(new FileInfo(path).Length, Sha(path)));
            }
        }
        Require(result.Count > 0, "Empty runtime bundle."); return result;
    }
    private static string Sha(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvariantFailure(message); }
}
