# Inline native AOT probe

```powershell
dotnet publish tests/harnesses/InlineProjectionAotProbe/InlineProjectionAotProbe.csproj -c Release -r win-x64 -p:BuildExamplesAndTests=true
./tests/harnesses/InlineProjectionAotProbe/bin/Release/net10.0/win-x64/publish/InlineProjectionAotProbe.exe
```

The executable refuses JIT execution. It exercises the actual repository → notification broker →
Inline processor → positioned store → session reader path. Twenty committed events fold into one
write without a separately started worker. A destination failure preserves the successful commit;
starting the host then recovers it without a second save. No notification handler is registered.

Event JSON uses generated metadata. The probe's typed, clone-safe projection store intentionally
isolates framework orchestration from external provider serialization. Its event store/checkpoint
are process-local: this is not a process-crash durability or SQL/Elasticsearch provider AOT test.
The entry point narrowly suppresses the existing public provider-interface serialization annotations;
the publish still treats other linker/AOT warnings as errors. This does not establish an annotation-free
public registration API.
