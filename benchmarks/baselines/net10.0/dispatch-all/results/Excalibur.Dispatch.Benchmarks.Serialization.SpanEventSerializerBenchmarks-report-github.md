```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                      | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|-------------------------------------------- |-----:|------:|------:|--------:|------------:|
| &#39;JSON Serialize Small&#39;                      |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Serialize Small (byte[])&#39;        |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Serialize Large&#39;                      |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Serialize Large (byte[])&#39;        |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Deserialize Small&#39;                    |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Deserialize Small (Span)&#39;        |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Deserialize Large&#39;                    |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Deserialize Large (Span)&#39;        |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Serialize Small (Pooled Buffer)&#39; |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Serialize Large (Pooled Buffer)&#39; |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Full Pooled Workflow&#39;            |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Event Replay (100 events)&#39;            |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Event Replay (100 events)&#39;       |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Event Append (100 events)&#39;            |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Event Append (100 events)&#39;       |   NA |    NA |     ? |       ? |           ? |
| &#39;JSON Round-Trip Small&#39;                     |   NA |    NA |     ? |       ? |           ? |
| &#39;ZeroAlloc Round-Trip Small (Pooled)&#39;       |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  SpanEventSerializerBenchmarks.'JSON Serialize Small': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Serialize Small (byte[])': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Serialize Large': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Serialize Large (byte[])': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Deserialize Small': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Deserialize Small (Span)': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Deserialize Large': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Deserialize Large (Span)': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Serialize Small (Pooled Buffer)': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Serialize Large (Pooled Buffer)': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Full Pooled Workflow': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Event Replay (100 events)': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Event Replay (100 events)': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Event Append (100 events)': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Event Append (100 events)': DefaultJob
  SpanEventSerializerBenchmarks.'JSON Round-Trip Small': DefaultJob
  SpanEventSerializerBenchmarks.'ZeroAlloc Round-Trip Small (Pooled)': DefaultJob
