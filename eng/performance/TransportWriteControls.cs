using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

// Untimed compiled-fixture controls only. Not shipping code or an AOT benchmark.
internal static class Program
{
    private const int Cycles = 16;
    private const int Warmups = 2;
    private const long GrowthLimit = 64L * 1024 * 1024;
    private static int _negatives;
    private static readonly string[] PhaseWrites = ["PublishToInMemoryAdapter"];
    private static readonly string[] BreakdownWrites = ["PublishPathConcurrent", "PublishReceiveCombinedConcurrent"];
    private sealed record Pin(long length, string sha256);
    private sealed record Snapshot(string[] ids, object[] messages);
    private sealed class InvariantFailure(string message) : Exception(message);

    public static async Task Main(string[] args)
    {
        Require(args.Length == 1, "Exactly one compiled bundle required.");
        var bundle = Path.GetFullPath(args[0]);
        var before = Pins(bundle);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(bundle, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var main = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
        var phase = main.GetType("Excalibur.Dispatch.Benchmarks.Diagnostics.TransportAdapterPhaseBenchmarks", true)!;
        var breakdown = main.GetType("Excalibur.Dispatch.Benchmarks.Diagnostics.TransportConcurrencyBreakdownBenchmarks", true)!;
        var cadence = Discover(phase, breakdown);
        var rows = new List<object>();
        foreach (var type in new[] { phase, breakdown })
        foreach (var method in type == phase ? PhaseWrites : BreakdownWrites)
        foreach (var payload in type == phase ? new[] { 256, 4096, 65536 } : new[] { 256, 4096 })
        foreach (var concurrency in type == phase ? new[] { 1, 8 } : new[] { 1, 2, 4, 8, 16 })
        {
            var fixture = Activator.CreateInstance(type)!;
            type.GetProperty("PayloadSizeBytes")!.SetValue(fixture, payload);
            type.GetProperty("Concurrency")!.SetValue(fixture, concurrency);
            long baseline = 0, max = 0;
            try
            {
                type.GetMethod("GlobalSetup")!.Invoke(fixture, null);
                var adapter = Field(type, "_adapter").GetValue(fixture)!;
                var command = Field(type, "_command").GetValue(fixture)!;
                var setup = type.GetMethod(type == phase ? "SetupPublishIteration" : "IterationSetup")!;
                var cleanup = type.GetMethod("CleanupPublishIteration")!;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var expected = type == phase ? 1 : concurrency;
                for (var cycle = -Warmups; cycle < Cycles; cycle++)
                {
                    setup.Invoke(fixture, null);
                    Validate(Sent(adapter), 0, command, payload, seen, false);
                    var task = (Task)type.GetMethod(method)!.Invoke(fixture, null)!;
                    await task;
                    var result = Convert.ToInt32(task.GetType().GetProperty("Result")!.GetValue(task));
                    Require(result == expected, "Awaited original method result differs.");
                    var sent = Sent(adapter);
                    Validate(sent, expected, command, payload, seen, true);
                    if (cycle == 0)
                    {
                        Reject(() => Validate(sent, expected + 1, command, payload, seen, true), "wrong count");
                        Reject(() => Validate(sent, expected, new object(), payload, seen, true), "wrong original command reference");
                        Reject(() => Validate(sent, expected, command, payload + 1, seen, true), "wrong payload");
                        var stale = new HashSet<string>(seen, StringComparer.Ordinal) { sent.ids[0] };
                        Reject(() => Validate(sent, expected, command, payload, stale, true), "stale GUID");
                        Reject(() => CheckOutcome(result + 1, expected), "wrong return count");
                    }
                    CheckOutcome(result, expected);
                    foreach (var id in sent.ids) Require(seen.Add(id), "GUID repeated across iterations.");
                    cleanup.Invoke(fixture, null);
                    Validate(Sent(adapter), 0, command, payload, seen, false);
                    cleanup.Invoke(fixture, null);
                    Validate(Sent(adapter), 0, command, payload, seen, false);
                    if (cycle == -1) baseline = GC.GetTotalMemory(true);
                    if (cycle >= 0)
                    {
                        var live = GC.GetTotalMemory(true); max = Math.Max(max, live);
                        Require(live - baseline < GrowthLimit, "Scoped post-cleanup heap growth budget exceeded.");
                    }
                }
                rows.Add(new { fixture = type.FullName, method, payloadSizeBytes = payload, concurrency,
                    expectedRetainedPeak = expected, verifiedCycles = Cycles, warmupCycles = Warmups,
                    baselineLiveBytes = baseline, maxLiveBytes = max, growthBytes = max - baseline });
            }
            finally { type.GetMethod("GlobalCleanup")!.Invoke(fixture, null); }
        }
        Require(rows.Count == 26 && _negatives == 130, "Exact26 scenarios/130 rejecting controls required.");
        var after = Pins(bundle);
        Require(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var value) && value == p.Value), "Runtime bundle changed.");
        var modules = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true).Select(a =>
        {
            var relative = Path.GetRelativePath(bundle, a.Location).Replace('\\', '/');
            Require(before.TryGetValue(relative, out var pin) && pin.sha256 == Sha(a.Location), "Loaded project module differs/outside bundle.");
            return new { relativePath = relative, sha256 = Sha(a.Location), mvid = a.ManifestModule.ModuleVersionId };
        }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "transport-write-isolation-controls-r1", passed = true,
            scenarioCount = 26, verifiedCycles = 26 * Cycles, warmupCycles = 26 * Warmups, rejectingNegativeControls = _negatives,
            cadence, scenarios = rows, runtimeFilesBefore = before, runtimeFilesAfter = after, loadedProjectModules = modules,
            runtimeBundleUnchanged = true, nativeAotVerified = false, timingOrThroughputMeasured = false,
            allTimedIterationsValidated = false, universalMemoryBoundVerified = false, fullSuiteComplete = false,
            scope = "26 original write method/parameter cases with explicit invocation1/unroll1 and untimed clear hooks. Repeated fixture cycles verify retention cardinality, original message reference and payload, GUID freshness, awaited return count, and cleanup. Managed heap samples are finite diagnostics. No production retention, timed child output, real overlap, AOT, throughput or universal memory claim." }));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object[] Discover(Type phase, Type breakdown)
    {
        var rows = new List<object>(); int writes = 0, reads = 0;
        foreach (var type in new[] { phase, breakdown })
        {
            Require(!type.CustomAttributes.Any(a => a.AttributeType.Name is "InvocationCountAttribute" or "UnrollFactorAttribute"), "Class-wide cadence override refused.");
            var selected = type == phase ? PhaseWrites : BreakdownWrites;
            foreach (var item in BenchmarkConverter.TypeToBenchmarks(type).BenchmarksCases)
            {
                var method = item.Descriptor.WorkloadMethod.Name;
                var write = selected.Contains(method, StringComparer.Ordinal);
                var explicitInvocation = item.Job.Run.HasValue(RunMode.InvocationCountCharacteristic);
                var explicitUnroll = item.Job.Run.HasValue(RunMode.UnrollFactorCharacteristic);
                var setup = (MethodInfo?)item.Descriptor.GetType().GetProperty("IterationSetupMethod")!.GetValue(item.Descriptor);
                var cleanup = (MethodInfo?)item.Descriptor.GetType().GetProperty("IterationCleanupMethod")!.GetValue(item.Descriptor);
                var expectedSetup = type == phase ? (write ? "SetupPublishIteration" : null) : "IterationSetup";
                Require(setup?.Name == expectedSetup && cleanup?.Name == (write ? "CleanupPublishIteration" : null), "Converted iteration hook targets changed.");
                Require(explicitUnroll && item.Job.Run.UnrollFactor == 1, "Original diagnostic unroll1 changed.");
                if (write) { Require(explicitInvocation && item.Job.Run.InvocationCount == 1, "Write case not isolated invocation1."); writes++; }
                else { Require(!explicitInvocation, "Nonwrite original adaptive invocation cadence changed."); reads++; }
                rows.Add(new { fixture = type.FullName, method, parameters = item.Parameters.Items.Select(p => new { p.Name, p.Value }).ToArray(),
                    write, explicitInvocation, invocationCount = explicitInvocation ? item.Job.Run.InvocationCount : (long?)null,
                    unrollFactor = item.Job.Run.UnrollFactor, jobId = item.Job.Id });
            }
        }
        Require(writes == 26 && reads == 40 && rows.Count == 66, "Converter must produce exactly26 write and40 unchanged nonwrite cases.");
        return rows.ToArray();
    }

    private static Snapshot Sent(object adapter)
    {
        var dictionary = (IEnumerable)adapter.GetType().GetProperty("SentMessages")!.GetValue(adapter)!;
        var entries = dictionary.Cast<object>().ToArray();
        return new Snapshot(entries.Select(e => (string)e.GetType().GetProperty("Key")!.GetValue(e)!).ToArray(),
            entries.Select(e => e.GetType().GetProperty("Value")!.GetValue(e)!).ToArray());
    }
    private static void Validate(Snapshot snapshot, int count, object command, int payload, HashSet<string> prior, bool verifyMessages)
    {
        Require(snapshot.ids.Length == count && snapshot.messages.Length == count, "Retained count differs.");
        Require(snapshot.ids.Distinct(StringComparer.Ordinal).Count() == count, "Duplicate retained identifier.");
        for (var i = 0; i < count; i++)
        {
            Require(Guid.TryParseExact(snapshot.ids[i], "D", out _) && !prior.Contains(snapshot.ids[i]), "Malformed or stale retained GUID.");
            if (!verifyMessages) continue;
            Require(ReferenceEquals(snapshot.messages[i], command), "Original command reference lost.");
            var value = (string)command.GetType().GetProperty("Payload")!.GetValue(command)!;
            Require(value.Length == payload && value.All(c => c == 'x'), "Original payload changed.");
        }
    }
    private static void CheckOutcome(int actual, int expected) => Require(actual == expected, "Wrong original returned count.");
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Reject(Action validator, string label)
    {
        try { validator(); } catch (InvariantFailure) { _negatives++; return; }
        throw new InvariantFailure("Negative control accepted " + label);
    }
    private static Dictionary<string, Pin> Pins(string root)
    {
        var result = new Dictionary<string, Pin>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Reparse runtime directory refused.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                Require((attributes & FileAttributes.ReparsePoint) == 0, "Reparse member refused.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                else result.Add(Path.GetRelativePath(root, path).Replace('\\', '/'), new Pin(new FileInfo(path).Length, Sha(path)));
            }
        }
        Require(result.Count > 0, "Empty runtime bundle."); return result;
    }
    private static string Sha(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvariantFailure(message); }
}
