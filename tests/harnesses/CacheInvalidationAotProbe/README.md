# Direct cache invalidation NativeAOT probe

This probe resolves the registered `ICacheInvalidationService` and verifies ordinary-tag
stamp propagation to a second provider after its memo refreshes. It also checks unrelated
stamp preservation and native string-cache invalidation. The backend is shared in-memory
storage; no Redis server or application database is used.

```powershell
dotnet publish tests/harnesses/CacheInvalidationAotProbe/CacheInvalidationAotProbe.csproj -c Release -r win-x64 -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:BuildExamplesAndTests=true
```

Follow repository build-slot and bounded-process requirements when running this command.
Execute the published executable to verify behavior; publishing alone is not a pass.
The executable asserts that dynamic code is disabled. It does NOT detect native execution:
`RuntimeFeature.IsDynamicCodeSupported` is a feature switch that `PublishAot` writes into
`runtimeconfig.json`, so it reads the same under a managed `dotnet run`. Only running the published
native executable establishes native behavior.

Use `-p:` rather than `/p:`: under a POSIX shell the leading slash is rewritten and MSBuild reads the
argument as a second project (`MSB1008`). On Windows the native link locates the MSVC toolchain
through `vswhere.exe`; if the Visual Studio Installer directory is not on `PATH` the link fails with
exit code 123 naming `vswhere.exe` rather than the missing `PATH` entry.

The provider-construction suppression covers only the registration API's warnings about
runtime-type result deserialization, which this probe never invokes. It does not establish
NativeAOT support for Dispatch result caching or remote cached-result eviction.

Current verification is **blocked**, not passed: on 2026-11-04 UTC, the .NET 10 publish
reported IL2026, IL3050 and IL2070 errors in HybridCache's default JSON serializer factory.
These compiler errors remain unsuppressed. Original registration and subsequent compiler
failures are retained under
`artifacts/benchmarks/current-refresh-20261101/direct-invalidation-native-publish-r1`
and `direct-invalidation-native-publish-r2`. Bead `Excalibur_Dispatch-kpc7hg` tracks
the remaining verification. The managed caching suite is separate evidence.
