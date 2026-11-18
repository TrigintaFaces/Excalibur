# NativeAOT DataProcessing contract probe

Links `DataProcessingSmoke.cs` from the broader AOT sample and exercises regular explicit
registrations, option binding, empty-page traversal, handler scope disposal and checkpoint callbacks.
Its bulk branch verifies sequential records, inline-wait suppression and exactly one final call to a
deterministic projection-service stand-in.

## Status

**The native publish works. Native execution has not been observed on the current build machine, and
the reason is now identified.**

Measured 2026-11-05:

- ILC compiled and the MSVC linker produced a 3,096,576-byte native executable, exit 0.
- Reading or executing that produced binary is then denied: the publish copy step fails with
  `MSB3021 ... Access to the path ... is denied` on the file it had just created, and launching it
  directly fails with a permission error.

This is an **endpoint-security product on the build machine blocking that produced binary**, not a
defect in this sample, the toolchain, or the framework. The discriminator, taken in an empty
directory with no file present, no deny ACE and no open handle:

| create, same directory | result |
|---|---|
| `BulkDataProcessing.exe` | access denied |
| `writeprobe.exe` | OK |
| `BulkDataProcessing.dll` | OK |
| `Other.exe` | OK |

A block on one exact filename while three controls in the same directory succeed. Redirecting
`NativeOutputPath` elsewhere moves the denial with the file, so it follows the binary rather than the
path. A sibling AOT sample in this folder publishes and links natively on the same machine with the
same toolchain, which is why the toolchain is ruled out.

**To get a native execution result:** exclude the repository's build output from the endpoint-security
product on that machine, or publish on a machine or CI leg without that rule. Changing a machine or
security setting is the machine owner's decision and none were changed here.

*(This section previously stated that the cause of the file-access failures remained unidentified, and
described `LNK1104` as the blocker. `LNK1104` was a downstream symptom: the native output directory is
created by the same target that invokes the linker, so an attempt whose output directory had been
moved aside reported a link failure instead of the access denial underneath it.)*

## Publish and execute

Requires the .NET 10 NativeAOT toolchain. Use `-p:` rather than `/p:` — under a POSIX shell the
leading slash is rewritten and MSBuild reads the argument as a second project (`MSB1008`).

```powershell
dotnet publish samples/10-aot/BulkDataProcessing -c Release -r win-x64 -p:BuildExamplesAndTests=true -o artifacts/bulk-native
./artifacts/bulk-native/BulkDataProcessing.exe
```

On Windows the native link step locates the MSVC toolchain through `vswhere.exe`. If the Visual Studio
Installer directory is not on `PATH`, the link fails with exit code 123 and a message naming
`vswhere.exe` rather than the missing `PATH` entry; prepend that directory for the build invocation.

## Scope

This target's in-process check establishes that dynamic code is disabled. It does **not** establish
that the process is a native binary — `RuntimeFeature.IsDynamicCodeSupported` is a feature switch that
`PublishAot` writes into `runtimeconfig.json`, so it reads the same under a managed `dotnet run`. Only
running the published native executable establishes native behavior.

It does not connect to a database or certify SQL checkpoint durability. The stand-in does not persist
projections. For a real repository/inline-projection example, run
`samples/09-advanced/persistence-patterns/BulkDataProcessing`; that separate managed example uses
in-memory stores and does not establish NativeAOT compatibility for the complete projection pipeline.
No throughput or production-speed claim is made by either example.

## Certification status

This target is **quarantined** in the sample fitness matrix, not certified. Sample certification
iterates the certified set only, so it neither builds nor runs this target and no certification run
reports a pass for it; a whole-solution build still compiles it.

It stays quarantined until a native execution result exists, because a managed run is not evidence of
NativeAOT behavior. The shared checks it calls *are* verified as actual NativeAOT through
`samples/10-aot/Excalibur.Dispatch.Aot.Sample`, whose default path invokes them and which the AOT
validation workflow publishes natively and runs — so the coverage is not missing, only this target's
own native result.
