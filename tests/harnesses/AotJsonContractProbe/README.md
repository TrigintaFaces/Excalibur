# Native JSON contract probe

This runnable sample exercises the existing `SystemTextJsonSerializer` through explicit owned-options
and advanced context-owned metadata routes. It has no
reflection serializer fallback. Run from the repository root:

```powershell
dotnet publish tests/harnesses/AotJsonContractProbe/AotJsonContractProbe.csproj -c Release -r win-x64 -o artifacts/aot-json-contract-probe -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false -m:1 -nr:false
./artifacts/aot-json-contract-probe/AotJsonContractProbe.exe
```

The project treats compiler and linker warnings as errors. The executable refuses
managed execution and checks that dynamic code is unavailable. Success reports
the migrated original 35 checks plus four owned-policy/registry checks (39 predicted until the current
executable is measured). The old byte-array/span, writer parity, full-input, relaxed parsing,
null-result, depth and converter work remains; exception assertions now check the codec's actual
SerializationException contract and original inner errors rather than silently unwrapping them.
The original M07 six-check LF/CRLF, tab/width-one formatting block remains unchanged.
The additional section resolves a runtime child through a separate generated context, consumes the
actual codec through the existing registry and framed IPayloadSerializer, verifies its ID and full JSON
bytes, and refuses a missing runtime child without reflection. Its marker prints only after those four
checks. The focused managed suite covers broader boundaries. Old 35-check native artifacts remain
historical evidence; they do not verify this changed source.

The codec consumes complete JSON documents and honor selected context
reader options. This deliberately corrects the earlier first-value/default-reader
behavior. Converters use standard System.Text.Json span semantics; malformed
later input can fail after user conversion code has run. This is a functional
NativeAOT probe, not a performance benchmark or full-framework certification.
