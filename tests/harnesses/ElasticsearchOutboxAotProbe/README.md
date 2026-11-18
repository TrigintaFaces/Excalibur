# Public Elasticsearch outbox NativeAOT regression probe

This non-test console harness exercises the existing public
`AddExcalibur` / `AddOutbox` / `UseElasticSearch(NodeUri)` owned-client route.
It keeps prepared payload bytes and nonzero sequence/routing data, supplies a
source-generated custom-header context, refuses absent metadata, and verifies
that the rejected header did not leave a document in a disposable Elasticsearch
9.0.0 fixture. Header reads retain their structured `JsonElement` representation.

Run from the repository root using PowerShell7 and the SDK pinned in global.json:

```powershell
./tests/harnesses/ElasticsearchOutboxAotProbe/Run-Local.ps1 -Runtime win-x64
# Optional separate disk for this run's native executable and harness-only ILC obj:
./tests/harnesses/ElasticsearchOutboxAotProbe/Run-Local.ps1 -Runtime win-x64 -ArtifactsParent C:/CodexArtifacts/outbox-native
# On Linux x64, with the native toolchain installed:
./tests/harnesses/ElasticsearchOutboxAotProbe/Run-Local.ps1 -Runtime linux-x64
```

Docker must be available; an unavailable daemon/fixture is a failure, not a skip.
Windows needs the NativeAOT C++ toolchain. Ubuntu needs `clang` and `zlib1g-dev`.
See the [official NativeAOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).
The script owns one container with a random name/ownership label and dynamically
bound loopback port, records the actual image/server identity, and removes only
that container in `finally`. It uses no shared database, credentials or local
application harness. Startup, publication, process execution and cleanup are
bounded; raw merged stdout/stderr file logs and exit receipts are retained under
`artifacts/elasticsearch-outbox-aot/<unique-run-id>/` by default. An optional artifact parent receives a fresh unique child; it cannot point into repository source. The runner records it, restores its scoped environment, and keeps the source/lock fence unchanged.

Before publication, four stdlib Python ownership controls must pass: nonzero child
exit preserves both streams, a hanging child preserves partial output and is killed,
a normally exited parent leaves no output-holding orphan, and a startup error
retains a failure receipt. Run those alone from the repository root when reviewing
the harness owner:

```powershell
python tests/harnesses/ElasticsearchOutboxAotProbe/owned_process.test.py --repo .
```

Use python3 instead of python for the standalone control command on Linux.

The private owner imports the existing eng/ci/lib/pre_release_windows_job.py on
Windows. Linux uses a new session/process group, scoped subreaper adoption, bounded
kill/reap and zero verification; detached descendants are cleaned up if adopted
but cause refusal. Both streams go to a file, so a descendant holding a redirected
handle cannot create a pipe-drain deadlock. An unverified cleanup always fails.
These controls require the platform being certified; a Windows control does not
establish Linux ownership behavior.

The project uses one direct shipping-provider reference. Its local
Directory.Build.props intentionally avoids the outer test SDK/xUnit dependency
injection and test-only warning exemptions. It explicitly enables harness builds,
and the runner checks that MSBuild did not skip it. Strict compiler/linker
warnings, disabled reflection JSON and the runtime no-dynamic-code guard are
required. The evaluated NoWarn check permits only IL2121 among IL tokens, the
verified SDK10.0.404 default (actual full default:1701;1702;IL2121); it does not
set NoWarn or permit any other IL waiver. The local props consumes only the scoped
ElasticsearchOutboxProbeIntermediateDir to move this harness's intermediates
under its fresh evidence/obj directory. Effective BaseIntermediateOutputPath AND
MSBuildProjectExtensionsPath must match it; dependency intermediates are untouched.
The official validator receives a unique relative
`NuGetLockFilePath=obj/elasticsearch-outbox-native/<run>/packages.lock.json`
for the whole restore graph; tracked lock files must remain unchanged. PublishAot
stays local to the project rather than leaking to build-tool projects.

Publication must also retain the raw `Generating native code` line, the actual
harness .obj/.o and native executable created/written within this publish window,
and stable native product hashes after execution. Binary existence alone is not
a pass. These are fresh run-local directories, not reused harness obj/bin output.

The runner propagates every nonzero native process exit and requires exactly one
of each case marker plus `PROBE-COMPLETE ElasticsearchOutboxAotProbe checks=5`.
Successful container startup or publication alone cannot pass it. The checks are:

1. Primitive prepared-byte public Stage/GetUnsent with all routing fields and SequenceNumber=7.
2. Generated Unicode custom-header public Stage/GetUnsent, preserving structured JSON.
3. Missing-type refusal by the exact SDK source serializer of the owned client.
4. Public staging refusal with an identifiable missing-metadata exception.
5. Real HTTP404 `found:false` for that rejected document id.

The HTTP lookup is a read-only no-accept oracle; it does not replace public
provider mutation/drain calls. No legacy POCO Enqueue, raw SDK mutation, private
document type, fake builder, warning waiver or reflection fallback is used.
A generic transport failure does not satisfy the missing-metadata check.

Every supported RID requires its own actual publish and executable run. Prior
Windows evidence does not establish Linux success. This finite regression does
not certify host startup/polling workers, arbitrary payload converters, all SDK
features, shared-client documents or package-wide NativeAOT compatibility.
