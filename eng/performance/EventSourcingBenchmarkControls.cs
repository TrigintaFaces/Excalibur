// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

// Diagnostic-only reflection against the actual compiled fixture. This is not an AOT test.
var bundle = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(bundle, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"));
var eventType = assembly.GetType("Excalibur.Dispatch.Benchmarks.Patterns.TestDomainEvent", true)!;
const string wireName = "excalibur.benchmarks.test-domain-event.v1";
Require(eventType.CustomAttributes.Any(a => a.AttributeType.Name == "MessageNameAttribute" &&
    a.ConstructorArguments.Count == 1 && (string?)a.ConstructorArguments[0].Value == wireName),
    "Compiled fixture lacks the required stable MessageName; refusing stress against the old bundle.");
var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.LoadTests.EventSourcingConcurrentLoadTest", true)!;
var field = type.GetField("_eventStore", BindingFlags.NonPublic | BindingFlags.Instance)!;
var tenantContextType = assembly.GetReferencedAssemblies().Select(Assembly.Load)
    .Select(a => a.GetType("Excalibur.Dispatch.UntenantedContext")).First(t => t != null)!;
var tenant = (string)Get(tenantContextType.GetProperty("Instance")!.GetValue(null)!, "TenantId");
Require(!string.IsNullOrWhiteSpace(tenant), "Actual UntenantedContext has no explicit tenant marker.");
var observations = new ConcurrentDictionary<ActivityTraceId, ConcurrentQueue<string>>();
using var listener = new ActivityListener
{
    ShouldListenTo = s => s.Name == "Excalibur.EventSourcing",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = a =>
    {
        if (a.OperationName == "EventStore.Append" && observations.TryGetValue(a.TraceId, out var queue))
            queue.Enqueue(a.GetTagItem("operation.result")?.ToString() ?? "<missing>");
    }
};
ActivitySource.AddActivityListener(listener);
var reports = new List<object>();
long baseline = GC.GetTotalMemory(true), maxLive = baseline;
var negativeControls = 0;
Reject(() => ValidateActivity(new[] { "success" }, 2, 0));
Reject(() => ValidateActivity(new[] { "success", "success", "success" }, 2, 0));
Reject(() => ValidateActivity(new[] { "success", "failure" }, 1, 1));
Reject(() => ValidateActivity(new[] { "success", "<missing>" }, 1, 1));
var timestamp = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
var valid = new Row("id", "aggregate-0", "LoadTestAggregate", timestamp, 0, 1, wireName,
    "{\"EventId\":\"id\",\"AggregateId\":\"aggregate-0\",\"Version\":1,\"EventType\":\"LoadTestEvent\",\"OccurredAt\":\"2026-01-01T00:00:00Z\",\"Data\":\"Load test event v1\",\"Metadata\":{\"Writer\":\"load-test\"}}", "{\"Writer\":\"load-test\"}");
ValidateRows(new[] { valid }, "aggregate-0", 1);
Reject(() => ValidateRows(Array.Empty<Row>(), "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Version = 1 } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { AggregateId = "wrong" } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid, valid with { Version = 1, Position = 2 } }, "aggregate-0", 2));
Reject(() => ValidateRows(new[] { valid with { Json = "{" } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Json = valid.Json.Replace("event v1", "event v2") } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { EventType = "wrong" } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Json = valid.Json.Replace("\"Version\":1", "\"Version\":2") } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Metadata = "{\"Writer\":\"wrong\"}" } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { AggregateType = "wrong" } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Timestamp = timestamp.AddSeconds(1) } }, "aggregate-0", 1));
Reject(() => ValidateRows(new[] { valid with { Json = valid.Json.Replace("LoadTestEvent", "wrong") } }, "aggregate-0", 1));
var validStore = new StoreSnapshot(new[] { new StreamSnapshot(tenant, "aggregate-0", "LoadTestAggregate", new[] { valid }) },
    new[] { new IndexSnapshot(tenant, valid.Id, valid) });
var expectedStore = new Dictionary<string, int> { ["aggregate-0"] = 1 };
ValidateStore(validStore, tenant, expectedStore);
Reject(() => ValidateStore(validStore, tenant, new Dictionary<string, int>()));
Reject(() => ValidateStore(validStore with { Streams = validStore.Streams.Concat(new[] { new StreamSnapshot(tenant, "extra", "LoadTestAggregate", Array.Empty<Row>()) }).ToArray() }, tenant, expectedStore));
Reject(() => ValidateStore(validStore with { Index = validStore.Index.Concat(new[] { new IndexSnapshot(tenant, "orphan", valid) }).ToArray() }, tenant, expectedStore));
Reject(() => ValidateStore(validStore with { Index = new[] { new IndexSnapshot(tenant, valid.Id, valid with { Version = 5 }) } }, tenant, expectedStore));
Reject(() => ValidateStore(validStore with { Index = new[] { new IndexSnapshot("wrong", valid.Id, valid) } }, tenant, expectedStore));
Reject(() => ValidateStore(validStore with { Streams = new[] { validStore.Streams[0] with { Tenant = "wrong" } } }, tenant, expectedStore));
Reject(() => ValidateStore(validStore with { Streams = new[] { validStore.Streams[0] with { Rows = new[] { valid with { Position = 2 } } } }, Index = new[] { new IndexSnapshot(tenant, valid.Id, valid with { Position = 2 }) } }, tenant, expectedStore));
foreach (var writers in new[] { 1, 5, 10 })
foreach (var events in new[] { 10, 100 })
foreach (var method in new[] { "ConcurrentAppend", "ConcurrentAppendThenLoad", "OptimisticConcurrencyConflicts" })
{
    var fixture = Activator.CreateInstance(type)!;
    type.GetProperty("ConcurrentWriters")!.SetValue(fixture, writers);
    type.GetProperty("EventsPerAggregate")!.SetValue(fixture, events);
    object? previousStore = null;
    for (var cycle = 0; cycle < 8; cycle++)
    {
        type.GetMethod("IterationSetup")!.Invoke(fixture, null);
        var store = field.GetValue(fixture)!;
        Require(!ReferenceEquals(previousStore, store), "Iteration setup reused prior store.");
        previousStore = store;
        ValidateStore(Snapshot(store), tenant, new Dictionary<string, int>());
        var conflict = method == "OptimisticConcurrencyConflicts";
        var ids = conflict ? new[] { "shared-aggregate" } : Enumerable.Range(0, writers).Select(w => $"aggregate-{w}").ToArray();
        foreach (var id in ids) Require((await Load(store, id)).Length == 0, "Fresh store contains prior events.");
        using var parent = new Activity("benchmark-control-window").SetIdFormat(ActivityIdFormat.W3C).Start();
        var queue = new ConcurrentQueue<string>();
        Require(observations.TryAdd(parent.TraceId, queue), "Duplicate trace window.");
        try
        {
            await (Task)type.GetMethod(method)!.Invoke(fixture, null)!;
            ValidateActivity(queue.ToArray(), conflict ? 2 : writers, conflict ? writers * events - 1 : 0);
        }
        finally { observations.TryRemove(parent.TraceId, out _); }
        ValidateStore(Snapshot(store), tenant, ids.ToDictionary(id => id, _ => conflict ? 2 : events));
        var all = new List<Row>();
        foreach (var id in ids)
        {
            var rows = await Load(store, id);
            ValidateRows(rows, id, conflict ? 2 : events);
            all.AddRange(rows);
        }
        Require(all.Select(r => r.Id).Distinct().Count() == all.Count, "Duplicate cross-stream event ID.");
        Require(all.Select(r => r.Position).Order().SequenceEqual(Enumerable.Range(1, all.Count).Select(i => (long)i)), "Global positions are not a contiguous committed prefix.");
        // Sequential result classification controls, NOT proof of concurrent result-object classification.
        type.GetMethod("IterationSetup")!.Invoke(fixture, null);
        var directStore = field.GetValue(fixture)!;
        Require(!ReferenceEquals(store, directStore), "Direct control setup reused store.");
        ValidateStore(Snapshot(directStore), tenant, new Dictionary<string, int>());
        foreach (var (version, expected, outcome) in new[] { (1L, -1L, "Committed"), (2L, 0L, "Committed"), (3L, 0L, "ConcurrencyConflict") })
        {
            var ev = type.GetMethod("CreateEvent", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { "direct-control", version })!;
            var domainInterface = eventType.GetInterfaces().Single(i => i.Name == "IDomainEvent");
            var array = Array.CreateInstance(domainInterface, 1); array.SetValue(ev, 0);
            var result = await AwaitValueTask(directStore.GetType().GetMethods().Single(m => m.Name == "AppendAsync" && m.GetParameters().Length == 5)
                .Invoke(directStore, new object[] { "direct-control", "LoadTestAggregate", array, expected, CancellationToken.None })!);
            Require(Get(result, "Outcome").ToString() == outcome, "Unexpected direct AppendResult.Outcome.");
            ValidateStore(Snapshot(directStore), tenant, new Dictionary<string, int> { ["direct-control"] = version == 1 ? 1 : 2 });
        }
        ValidateRows(await Load(directStore, "direct-control"), "direct-control", 2);
        ValidateStore(Snapshot(directStore), tenant, new Dictionary<string, int> { ["direct-control"] = 2 });
        maxLive = Math.Max(maxLive, GC.GetTotalMemory(true));
        Require(maxLive - baseline < 64L * 1024 * 1024, "Live managed memory grew beyond diagnostic budget.");
    }
    reports.Add(new { method, writers, events, cycles = 8 });
}
Require(reports.Count == 18, "Incomplete scenario population.");
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, uniqueCases = reports.Count, repeats = 8, cycles = 144,
    negativeControls, baselineLiveBytes = baseline, maxLiveBytes = maxLive, liveGrowthBytes = maxLive - baseline, scenarios = reports,
    aotVerification = false }));

void Reject(Action action)
{
    try { action(); } catch (Exception e) when (e is InvalidOperationException or JsonException) { negativeControls++; return; }
    throw new InvalidOperationException("Negative validator control was accepted.");
}
static object Get(object row, string property) => row.GetType().GetProperty(property)!.GetValue(row)!;
static async Task<object> AwaitValueTask(object value)
{
    var task = (Task)value.GetType().GetMethod("AsTask")!.Invoke(value, null)!;
    await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
}
static async Task<Row[]> Load(object store, string id)
{
    var method = store.GetType().GetMethod("LoadAsync", new[] { typeof(string), typeof(string), typeof(CancellationToken) })!;
    var result = (IEnumerable)await AwaitValueTask(method.Invoke(store, new object[] { id, "LoadTestAggregate", CancellationToken.None })!);
    return result.Cast<object>().Select(ToRow).ToArray();
}
static Row ToRow(object r) => new((string)Get(r,"EventId"), (string)Get(r,"AggregateId"),
    (string)Get(r,"AggregateType"), (DateTimeOffset)Get(r,"Timestamp"),
    (long)Get(r,"Version"), (long)Get(r,"GlobalPosition"), (string)Get(r,"EventType"),
    System.Text.Encoding.UTF8.GetString((byte[])Get(r,"EventData")), System.Text.Encoding.UTF8.GetString((byte[])Get(r,"Metadata")));
static StoreSnapshot Snapshot(object store)
{
    // Workload is awaited before enumeration; no writer can mutate either collection here.
    var streams = new List<StreamSnapshot>();
    var index = new List<IndexSnapshot>();
    foreach (var entry in (IEnumerable)store.GetType().GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!)
    {
        var key = (System.Runtime.CompilerServices.ITuple)Get(entry, "Key");
        streams.Add(new((string)key[0]!, (string)key[1]!, (string)key[2]!, ((IEnumerable)Get(entry, "Value")).Cast<object>().Select(ToRow).ToArray()));
    }
    foreach (var entry in (IEnumerable)store.GetType().GetField("_eventsById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!)
    {
        var key = (System.Runtime.CompilerServices.ITuple)Get(entry, "Key");
        index.Add(new((string)key[0]!, (string)key[1]!, ToRow(Get(entry,"Value"))));
    }
    return new(streams.ToArray(), index.ToArray());
}
static void ValidateStore(StoreSnapshot snapshot, string tenant, Dictionary<string,int> expected)
{
    Require(snapshot.Streams.Length == expected.Count, "Extra or missing stored stream.");
    var rows = new Dictionary<string, Row>();
    var streamIds = new HashSet<string>();
    foreach (var stream in snapshot.Streams)
    {
        Require(stream.Tenant == tenant && stream.AggregateType == "LoadTestAggregate" &&
            streamIds.Add(stream.AggregateId) && expected.TryGetValue(stream.AggregateId, out var count), "Wrong composite stream key.");
        ValidateRows(stream.Rows, stream.AggregateId, expected[stream.AggregateId]);
        foreach (var row in stream.Rows) Require(rows.TryAdd(row.Id, row), "Duplicate whole-store identity.");
    }
    Require(rows.Count == expected.Values.Sum() && snapshot.Index.Length == rows.Count, "Whole-store/index total differs.");
    var indexed = new HashSet<string>();
    foreach (var entry in snapshot.Index)
        Require(entry.Tenant == tenant && indexed.Add(entry.EventId) && rows.TryGetValue(entry.EventId, out var row) && row == entry.Row,
            "Orphan, wrong-tenant, duplicate or mismatched event index.");
    Require(rows.Values.Select(r => r.Position).Order().SequenceEqual(Enumerable.Range(1, rows.Count).Select(i => (long)i)), "Global position prefix changed.");
}
static void ValidateActivity(string[] results, int successes, int conflicts)
{
    Require(results.Length == successes + conflicts && results.Count(x => x == "success") == successes &&
        results.Count(x => x == "concurrency_conflict") == conflicts, "Missing, extra or unsuccessful append activity.");
}
static JsonElement Property(JsonElement element, string name) => element.EnumerateObject().Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
static void ValidateRows(Row[] rows, string aggregate, int count)
{
    Require(rows.Length == count && rows.Select(r => r.Id).Distinct().Count() == count, "Wrong event count or duplicate identity.");
    for (var i = 0; i < rows.Length; i++)
    {
        var row = rows[i];
        Require(row.AggregateId == aggregate && row.AggregateType == "LoadTestAggregate" && row.Version == i && row.EventType == "excalibur.benchmarks.test-domain-event.v1", "Stored identity/version/type changed.");
        using var json = JsonDocument.Parse(row.Json);
        var payload = json.RootElement;
        Require(Property(payload,"EventId").GetString() == row.Id && Property(payload,"AggregateId").GetString() == aggregate &&
            Property(payload,"Version").GetInt64() == i + 1 && Property(payload,"EventType").GetString() == "LoadTestEvent" &&
            Property(payload,"OccurredAt").GetDateTimeOffset() == row.Timestamp && Property(payload,"Data").GetString() == $"Load test event v{i + 1}" &&
            Property(Property(payload,"Metadata"),"Writer").GetString() == "load-test", "Event payload changed.");
        using var metadata = JsonDocument.Parse(row.Metadata);
        Require(Property(metadata.RootElement,"Writer").GetString() == "load-test", "Stored Writer metadata changed.");
    }
}
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
record Row(string Id, string AggregateId, string AggregateType, DateTimeOffset Timestamp, long Version, long Position, string EventType, string Json, string Metadata);


record StreamSnapshot(string Tenant, string AggregateId, string AggregateType, Row[] Rows);
record IndexSnapshot(string Tenant, string EventId, Row Row);
record StoreSnapshot(StreamSnapshot[] Streams, IndexSnapshot[] Index);

