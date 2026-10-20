# Native JSON contract probe

This runnable sample exercises `AotJsonSerializer` and
`CompositeAotJsonSerializer` against explicit source-generated contexts. It has no
reflection serializer fallback. Run from the repository root:

```powershell
dotnet publish tests/harnesses/AotJsonContractProbe/AotJsonContractProbe.csproj -c Release -r win-x64 -o artifacts/aot-json-contract-probe -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false -m:1 -nr:false
./artifacts/aot-json-contract-probe/AotJsonContractProbe.exe
```

The project treats compiler and linker warnings as errors. The executable refuses
managed execution and checks that dynamic code is unavailable. Success reports
29 checks across disabled reflection-based JSON, byte-array/span round trips, writer parity, selected context
priority before/after warmup, full-input rejection, relaxed parsing, null-result
policy, selected depth cases and a custom converter's result/options/exception
identity. The focused managed suite adds broader contract and boundary coverage.

Both serializers consume complete JSON documents and honor selected context
reader options. This deliberately corrects the earlier first-value/default-reader
behavior. Converters use standard System.Text.Json span semantics; malformed
later input can fail after user conversion code has run. This is a functional
NativeAOT probe, not a performance benchmark or full-framework certification.
