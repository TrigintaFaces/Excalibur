// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

// Separate diagnostic process; reflection here does not establish NativeAOT compatibility.
var bundle = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, n) => File.Exists(Path.Combine(bundle,n.Name+".dll"))
    ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle,n.Name+".dll")) : null;
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle,"Excalibur.Dispatch.Benchmarks.dll"));
var type = assembly.GetType("Excalibur.Dispatch.Benchmarks.Scenarios.EndToEndScenarioBenchmarks",true)!;
var eventType = type.GetNestedType("BenchmarkOrderEvent",BindingFlags.NonPublic)!;
const string wire = "excalibur.benchmarks.order-event.v1";
Require(eventType.CustomAttributes.Any(a => a.AttributeType.Name == "MessageNameAttribute" && (string?)a.ConstructorArguments[0].Value == wire),"Stable MessageName missing in compiled fixture.");
var setup = type.GetMethod("IterationSetup")!;
var cleanup = type.GetMethod("IterationCleanup")!;
Require(setup.ReturnType == typeof(void) && cleanup.ReturnType == typeof(void),"Iteration hooks must bind to BenchmarkDotNet Action delegates.");
Require(setup.CustomAttributes.Any(a => a.AttributeType.Name == "IterationSetupAttribute") && cleanup.CustomAttributes.Any(a => a.AttributeType.Name == "IterationCleanupAttribute"),"Missing isolation hooks.");
var invocation = type.CustomAttributes.Single(a => a.AttributeType.Name == "InvocationCountAttribute");
Require((int)invocation.ConstructorArguments[0].Value! == 1 && (int)invocation.ConstructorArguments[1].Value! == 1,"Invocation/unroll changed.");
var tenantType = assembly.GetReferencedAssemblies().Select(Assembly.Load).Select(a => a.GetType("Excalibur.Dispatch.UntenantedContext")).First(t => t != null)!;
var tenant = (string)Get(tenantType.GetProperty("Instance")!.GetValue(null)!,"TenantId");
var traces = new ConcurrentDictionary<ActivityTraceId,ConcurrentQueue<string>>();
using var listener = new ActivityListener {
 ShouldListenTo = s => s.Name == "Excalibur.EventSourcing",
 Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
 SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
 ActivityStopped = a => { if(a.OperationName == "EventStore.Append" && traces.TryGetValue(a.TraceId,out var q)) q.Enqueue(a.GetTagItem("operation.result")?.ToString() ?? "missing"); }
};
ActivitySource.AddActivityListener(listener);
var methods = new[] { "CommandDispatchToOutbox", "CommandDispatchToOutbox_10Sequential", "EventSourcingFullCycle", "EventSourcingSnapshotPartialLoad", "EventSourcingNewAggregate", "FullPipelineWithTransport", "FullPipelineWithTransport_10Sequential", "FullPipelineWithTransport_10Concurrent" };
var reports = new List<object>();
long baseline = GC.GetTotalMemory(true), maxLive = baseline;
var negatives = 0;
Reject(() => ValidateActivity(new[]{"success"},0,0));
Reject(() => ValidateActivity(Array.Empty<string>(),1,0));
Reject(() => ValidateActivity(new[]{"failure"},1,0));
Reject(() => ValidateActivity(new[]{"success","success"},1,0));
Reject(() => ValidateOutput(0,true,10));
Reject(() => ValidateOutput(new object(),false,0));
foreach(var method in methods)
{
 var fixture = Activator.CreateInstance(type)!;
 cleanup.Invoke(fixture,null); cleanup.Invoke(fixture,null);
 for(var repeat=0;repeat<8;repeat++)
 {
  setup.Invoke(fixture,null);
  try {
   try { setup.Invoke(fixture,null); throw new Exception("Double setup accepted."); }
   catch(TargetInvocationException ex) when(ex.InnerException is InvalidOperationException) { }
   var eventStore = Field(fixture,"_eventStore")!;
   var fullStore = Field(fixture,"_fullPipelineEventStore")!;
   var fullOutbox = Field(fixture,"_fullPipelineOutboxStore")!;
   var sender = Field(fixture,"_transportSender")!;
   var snapshotStore = Field(fixture,"_snapshotStore")!;
   var provider = Field(fixture,"_dispatchToOutboxProvider")!;
   var serviceInterface = assembly.GetReferencedAssemblies().Select(Assembly.Load).Select(a => a.GetType("Excalibur.Dispatch.IOutboxStore")).First(t => t != null)!;
   var outbox = provider.GetType().GetMethod("GetService",new[]{typeof(Type)})!.Invoke(provider,new object[]{serviceInterface})!;
   var aggregate = (string)Field(fixture,"_preloadedAggregateId")!;
   var orderId = Get(Field(fixture,"_submitOrderCommand")!,"OrderId").ToString()!;
   var s1OrderId = Get(Field(fixture,"_placeOrderCommand")!,"OrderId").ToString()!;
   ValidateStore(Capture(eventStore),tenant,new Dictionary<string,int>{{aggregate,20}},"BenchmarkOrder",aggregate,false);
   ValidateStore(Capture(fullStore),tenant,new Dictionary<string,int>(),"Order",aggregate,true);
   ValidateOutbox(outbox,0,false,s1OrderId); ValidateOutbox(fullOutbox,0,true,orderId); ValidateTransport(sender,0,orderId);
   ValidateSnapshot(snapshotStore,aggregate,20);
   using var parent = new Activity("end-to-end-control-window").SetIdFormat(ActivityIdFormat.W3C).Start();
   var activities = new ConcurrentQueue<string>(); Require(traces.TryAdd(parent.TraceId,activities),"Trace reused.");
   object? output;
   try {
    var task = (Task)type.GetMethod(method)!.Invoke(fixture,null)!; await task;
    output = task.GetType().GetProperty("Result")?.GetValue(task);
   } finally { traces.TryRemove(parent.TraceId,out _); }
   var full = method.StartsWith("FullPipeline");
   var ten = method.Contains("10");
   var dispatch = full || method.StartsWith("Command");
   if(dispatch) ValidateOutput(output,ten,ten?10:0);
   if(method == "EventSourcingNewAggregate") Require(Get(output!,"Outcome").ToString() == "Committed","New aggregate did not commit.");
   ValidateActivity(activities.ToArray(),full || method is "EventSourcingFullCycle" or "EventSourcingNewAggregate" ? 1:0,full && ten?9:0);
   var expected = new Dictionary<string,int>{{aggregate,method == "EventSourcingFullCycle"?23:20}};
   if(method == "EventSourcingNewAggregate") {
    var snapshot = Capture(eventStore);
    var extra = snapshot.Streams.Single(s => s.AggregateId != aggregate); expected.Add(extra.AggregateId,5);
   }
   ValidateStore(Capture(eventStore),tenant,expected,"BenchmarkOrder",aggregate,false);
   ValidateStore(Capture(fullStore),tenant,full?new Dictionary<string,int>{{orderId,1}}:new Dictionary<string,int>(),"Order",aggregate,true);
   ValidateOutbox(outbox,method.StartsWith("Command")?(ten?10:1):0,false,s1OrderId);
   ValidateOutbox(fullOutbox,full?(ten?10:1):0,true,orderId);
   ValidateTransport(sender,full?(ten?10:1):0,orderId);
   ValidateSnapshot(snapshotStore,aggregate,method == "EventSourcingFullCycle"?22:20);
   // Synthetic mutations exercise the same whole-store validator, not a second surrogate.
   if(method == methods[0] && repeat == 0) {
    var snap = Capture(eventStore); var row = snap.Streams[0].Rows[0];
    Reject(() => ValidateStore(snap with { Streams = Array.Empty<Stream>() },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateStore(snap with { Index = snap.Index.Concat(new[]{new Index("wrong",row.Id,row)}).ToArray() },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateStore(snap with { Streams = snap.Streams.Concat(new[]{snap.Streams[0]}).ToArray() },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateStore(snap with { Index = snap.Index.Select((i,n)=> n==0?i with { Tenant = "wrong" }:i).ToArray() },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateOutbox(outbox,0,false,s1OrderId));
    Reject(() => ValidateTransport(sender,1,orderId));
    var wrongRows = snap.Streams[0].Rows.ToArray(); wrongRows[0] = row with { Json = "{}" };
    Reject(() => ValidateStore(snap with { Streams = new[]{snap.Streams[0] with { Rows = wrongRows }} },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateStore(snap with { Index = snap.Index.Skip(1).ToArray() },tenant,expected,"BenchmarkOrder",aggregate,false));
    Reject(() => ValidateStore(snap with { Index = snap.Index.Select((i,n)=> n==0?i with { Row = row with { Version = 99 } }:i).ToArray() },tenant,expected,"BenchmarkOrder",aggregate,false));
   }
   cleanup.Invoke(fixture,null);
   Require(((IDictionary)Field(outbox,"_messages")!).Count == 0 && ((IDictionary)Field(fullOutbox,"_messages")!).Count == 0,"Disposed outbox retained messages.");
   Require((bool)Field(snapshotStore,"_disposed")! && ((IDictionary)Field(snapshotStore,"_snapshots")!).Count == 0,"Snapshot not disposed.");
   ValidateTransport(sender,0,orderId);
   foreach(var f in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(f=>!f.FieldType.IsValueType)) Require(f.GetValue(fixture) == null,"Cleanup retained fixture reference.");
   Require(!(bool)Field(fixture,"_iterationActive")!,"Cleanup retained active flag.");
   cleanup.Invoke(fixture,null);
  } finally { cleanup.Invoke(fixture,null); }
  maxLive = Math.Max(maxLive,GC.GetTotalMemory(true)); Require(maxLive-baseline < 64L*1024*1024,"Managed memory exceeded bound.");
 }
 reports.Add(new { method, repeats=8 });
}
Console.WriteLine(JsonSerializer.Serialize(new { passed=true,uniqueCases=8,repeats=8,cycles=64,negativeControls=negatives,baselineLiveBytes=baseline,maxLiveBytes=maxLive,scenarios=reports,aotVerification=false }));
void Reject(Action a) { try { a(); } catch(Exception e) when(e is InvalidOperationException or JsonException) { negatives++; return; } throw new Exception("Negative validator accepted."); }
static object Get(object o,string name)=>o.GetType().GetProperty(name)!.GetValue(o)!;
static object? Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o);
static void Require(bool c,string message) { if(!c) throw new InvalidOperationException(message); }
static void ValidateOutput(object? result,bool batch,int expected) {
 if(batch) Require(result is int count && count==expected,"Wrong dispatch count.");
 else Require(result != null && result.GetType().GetProperty("Succeeded") != null && (bool)Get(result,"Succeeded"),"Dispatch failed or result missing.");
}
static void ValidateActivity(string[] tags,int success,int conflicts)=>Require(tags.Length==success+conflicts && tags.Count(t=>t=="success")==success && tags.Count(t=>t=="concurrency_conflict")==conflicts,"Append outcome census differs.");
static Row RowOf(object o)=>new((string)Get(o,"EventId"),(string)Get(o,"AggregateId"),(string)Get(o,"AggregateType"),(long)Get(o,"Version"),(long)Get(o,"GlobalPosition"),(string)Get(o,"EventType"),(DateTimeOffset)Get(o,"Timestamp"),System.Text.Encoding.UTF8.GetString((byte[])Get(o,"EventData")),System.Text.Encoding.UTF8.GetString((byte[])Get(o,"Metadata")));
static Store Capture(object store) {
 var streams = ((IEnumerable)Field(store,"_events")!).Cast<object>().Select(e=> { var k=(System.Runtime.CompilerServices.ITuple)Get(e,"Key"); return new Stream((string)k[0]!, (string)k[1]!, (string)k[2]!,((IEnumerable)Get(e,"Value")).Cast<object>().Select(RowOf).ToArray()); }).ToArray();
 var index = ((IEnumerable)Field(store,"_eventsById")!).Cast<object>().Select(e=> { var k=(System.Runtime.CompilerServices.ITuple)Get(e,"Key"); return new Index((string)k[0]!, (string)k[1]!,RowOf(Get(e,"Value"))); }).ToArray();
 return new(streams,index);
}
static JsonElement P(JsonElement e,string name)=>e.EnumerateObject().Single(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Value;
static void ValidateStore(Store store,string tenant,Dictionary<string,int> expected,string aggregateType,string seeded,bool full) {
 Require(store.Streams.Length==expected.Count,"Unexpected stream population."); var all=new Dictionary<string,Row>(); var ids=new HashSet<string>();
 foreach(var stream in store.Streams) {
  Require(stream.Tenant==tenant && stream.Type==aggregateType && ids.Add(stream.AggregateId) && expected.ContainsKey(stream.AggregateId),"Wrong stream key.");
  Require(stream.Rows.Length==expected[stream.AggregateId],"Wrong stream size.");
  for(var i=0;i<stream.Rows.Length;i++) {
   var row=stream.Rows[i]; Require(row.Aggregate==stream.AggregateId && row.AggregateType==aggregateType && row.Version==i && row.Wire=="excalibur.benchmarks.order-event.v1" && all.TryAdd(row.Id,row),"Stored identity/version/type mismatch.");
   using var doc=JsonDocument.Parse(row.Json); var p=doc.RootElement;
   var seed=stream.AggregateId==seeded; var added=seed && i>=20;
   Require(P(p,"EventId").GetString()==row.Id && P(p,"AggregateId").GetString()==row.Aggregate && P(p,"OccurredAt").GetDateTimeOffset()==row.Timestamp &&
    P(p,"Version").GetInt64()==(full?1:added?i:i+1) && P(p,"EventType").GetString()==(full?"OrderSubmitted":seed?"OrderItemAdded":"OrderCreated") &&
    P(p,"ItemName").GetString()==(full?"FullPipelineOrder":added?$"NewItem-{i-20}":seed?$"Item-{i+1}":$"Item-{i}") &&
    P(p,"Quantity").GetInt32()==(full?3:added?i-19:seed?i+1:1),"Payload changed.");
   using var metadata=JsonDocument.Parse(row.Metadata); var key=full?"CustomerId":"UserId"; var value=full?"customer-bench-001":"benchmark-user";
   Require(P(metadata.RootElement,key).GetString()==value && P(P(p,"Metadata"),key).GetString()==value,"Metadata changed.");
  }
 }
 Require(all.Count==expected.Values.Sum() && store.Index.Length==all.Count,"Index cardinality differs."); var indexIds=new HashSet<string>();
 foreach(var index in store.Index) Require(index.Tenant==tenant && indexIds.Add(index.Id) && all.TryGetValue(index.Id,out var row) && row==index.Row,"Orphan/wrong tenant/index row.");
 Require(all.Values.Select(r=>r.Position).Order().SequenceEqual(Enumerable.Range(1,all.Count).Select(i=>(long)i)),"Global positions differ.");
}
static void ValidateOutbox(object store,int count,bool full,string order) {
 var rows=((IDictionary)Field(store,"_messages")!).Values.Cast<object>().ToArray(); Require(rows.Length==count && rows.Select(r=>(string)Get(r,"Id")).Distinct().Count()==count,"Outbox population differs.");
 foreach(var r in rows) { var headers=(IDictionary)Get(r,"Headers"); Require(Get(r,"MessageType").ToString()==(full?"OrderSubmitted":"OrderPlaced") && Get(r,"Destination").ToString()=="orders-topic" && Get(r,"Status").ToString()=="Staged" && ((byte[])Get(r,"Payload")).SequenceEqual(new byte[64]) && (string?)headers["OrderId"]==order && (full || (string?)headers["CustomerId"]=="customer-bench-001"),"Outbox payload/status changed."); }
}
static void ValidateTransport(object sender,int count,string order) {
 var rows=((IEnumerable)Get(sender,"SentMessages")).Cast<object>().ToArray(); Require(rows.Length==count && rows.Select(r=>(string)Get(r,"Id")).Distinct().Count()==count,"Transport population differs.");
 foreach(var r in rows) Require(((ReadOnlyMemory<byte>)Get(r,"Body")).Span.SequenceEqual(new byte[64]) && Get(r,"ContentType").ToString()=="application/json" && Get(r,"CorrelationId").ToString()==order && Get(r,"Subject").ToString()=="order.submitted" && Get(r,"MessageType").ToString()=="OrderSubmitted","Transport changed.");
}
static void ValidateSnapshot(object store,string aggregate,long version) {
 var rows=((IEnumerable)Field(store,"_snapshots")!).Cast<object>().ToArray(); Require(rows.Length==1,"Snapshot population differs."); var row=Get(rows[0],"Value");
 var key=store.GetType().GetMethod("GetKey",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(store,new object[]{aggregate,"BenchmarkOrder"});
 Require(Equals(Get(rows[0],"Key"),key) && Get(row,"AggregateId").ToString()==aggregate && Get(row,"AggregateType").ToString()=="BenchmarkOrder" && (long)Get(row,"Version")==version && ((ReadOnlyMemory<byte>)Get(row,"Data")).Span.SequenceEqual(new byte[256]),"Snapshot identity/version/data differs.");
}
record Row(string Id,string Aggregate,string AggregateType,long Version,long Position,string Wire,DateTimeOffset Timestamp,string Json,string Metadata);
record Stream(string Tenant,string AggregateId,string Type,Row[] Rows);
record Index(string Tenant,string Id,Row Row);
record Store(Stream[] Streams,Index[] Index);
