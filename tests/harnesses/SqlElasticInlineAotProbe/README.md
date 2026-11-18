# SQL Server → Elasticsearch Inline native AOT verification

This probe runs the real SQL Server event store, global reader and durable checkpoint store,
the pooled Elasticsearch projection client, the repository and the Inline processor. It refuses
dynamic-code-enabled execution. That runtime check alone does not distinguish a managed invocation
from a successfully published Native AOT executable. It uses SQL password authentication on Windows x64 / .NET 10.

## Current release status: strict publication required for alpha22

The alpha22 prerequisite is successful strict publication of this same SQL Server-to-Elasticsearch
durable workload. It currently **fails**; neither managed execution nor earlier diagnostic native
runtime success satisfies that prerequisite. Do not suppress diagnostics or weaken the workload to
make the gate green. The upstream repair remains tracked in `Excalibur_Dispatch-481e8z`.

The dependency limitation accepted on 2026-10-24 is historical. The explicit alpha22 strict-success
requirement supersedes that release disposition. The current admission gate returns failure for
the known upstream diagnostic multiset as well as for new or missing diagnostics; matching the
historical baseline no longer admits a failed publication. See the
[strict publication admission decision](../../../management/architecture/inline-native-publication-admission.md).
Native runtime certification remains a separate requirement.

```powershell
dotnet publish tests/harnesses/SqlElasticInlineAotProbe/SqlElasticInlineAotProbe.csproj -c Release -r win-x64 -p:BuildExamplesAndTests=true
```

The shipping pins are Microsoft.Data.SqlClient 7.1.1, Elastic.Clients.Elasticsearch 9.5.3,
Elastic.Transport 1.1.0, Dapper 2.1.89 and aligned .NET 10.0.13 servicing packages.
Provider-owned SQL operations use explicit parameter binding and ordinal readers; public request
constructors retain their existing Dapper behavior. Dapper diagnostics reached zero with the old
2.1.72 package before upgrading, so upstream diagnostic suppression is not the basis for this result.
Ordinary JSON event-envelope construction no longer eagerly registers MemoryPack formatters;
legacy binary envelopes retain the generated wire format and register when used.

Strict publication remains blocked by SqlClient/configuration diagnostics. The generated projection
registration and session-reader overloads have cleared the four projection API diagnostics. The probe has no entry-point warning suppressions.
Only official NuGet dependencies are allowed for this release; source-built driver experiments are not
release candidates. SqlClient's upstream [zero-warning NativeAOT work remains open](https://github.com/dotnet/SqlClient/issues/1947).

The 2026-10-23 strict run in `artifacts/native-inline/ab1458d2b9544f3abee952dfccac253f`
has zero projection API diagnostics and 57 dependency diagnostics: 54 trimming and three dynamic-code
diagnostics. They include configuration/authentication discovery, UDT reflection, diagnostic payloads,
SqlVector JSON and enclave JSON. SQL password authentication does not remove these reachable paths.
That 7.1.1 evidence predates the official 8.0.0-preview1.26313.4 package. In the retained
2026-11-17 qualification, the byte-unchanged probe with that preview and default features still failed
strict publication with 27 unique IL diagnostics plus the failing MSBuild diagnostic. A separate
diagnostic-only variant disabling application-configuration loading and Azure extension discovery
still failed with ten UDT-related IL2057/IL2072/IL2075 diagnostics plus MSBuild. Those switches change
real capabilities and have not been approved as the release configuration. The source dependency
pins remain 7.1.1; qualification is not adoption of the preview.

Valid source-generated metadata and preservation annotations are permitted when they truthfully
describe known reflected types and members. The remaining warnings do not establish that the
fixed non-UDT probe requires runtime code generation or that Native AOT is impossible. Application
type preservation has not, however, been shown to resolve the current driver-internal dynamic type
and member flows. No capability-preserving strict-success solution is verified yet.

Keep the upstream repair open. A successful strict publication and the required native host scenarios
must be evidenced separately; a managed run, diagnostic-policy match or annotated build alone is
not proof that the published native executable ran this durable workload.

## Verification after the five Inline review fixes (2026-10-23)

- EventSourcing unit suite: 4,519 passed, zero failed/skipped (benchmark cases excluded).
- Strict publish: `artifacts/native-inline/b419caa403354f168522c11f62b20d26`, failed with
  57 dependency diagnostics (54 trimming, three dynamic-code), zero projection API diagnostics.
- Diagnostic native runtime: `artifacts/native-inline/3d351cb6432742989221cf78fe6e3b5f`, passed
  API alone (including aggregate reload), Jobs alone, and eight concurrent API writers with Jobs.
  Each concurrent document had count 20 and Elasticsearch version 1.
- Outage recovery: the original Jobs process survived at least three failed attempts, then recovered
  the committed-but-pending document without restarting. The verification process only read the
  projection; it did not start a processor or save another command. Search visibility and a subsequent
  restarted recovery process also passed. SQL checkpoint CAS and snapshot/reconciliation checks passed.

Every dependency diagnostic remains visible. Diagnostic mode changes warning severity only to enable
runtime investigation; its verdict is explicitly `not-certified`. Strict publication stays failed;
the required diagnostic policy check distinguishes the accepted dependency limitation from new failures.

## Diagnostic runtime verification

The local runner uses the existing AngularApp1.Local Docker services on ports 11433 and 19200. It
creates a separate database and Elasticsearch index per run and retains evidence/data. It temporarily
stops and restarts **only the isolated Elasticsearch container** for the outage arm. Run with no other
local scenario using that container. Credentials are read from the existing local harness environment
file and are not written into evidence.

```powershell
pwsh tests/harnesses/SqlElasticInlineAotProbe/Run-Local.ps1 -Diagnostic
```

`-Diagnostic` keeps all compiler diagnostics visible but permits producing an executable to investigate
runtime behavior. Its verdict always says `strictPublication: not-certified`. It verifies API-only,
Jobs-only, cooperating native processes, aggregate reload and update, single successful indexing per
concurrent document, pending committed writes during an outage, fresh-process recovery and search
visibility. It also checks checkpoint CAS/enumeration, metadata/timestamp mapping, original-append
retry recognition and a rejected append with no matching committed events. A failing generic data
request also verifies that native error formatting preserves the original exception as its cause.
Native request diagnostics use CLR type names without loading the optional Demystifier formatter;
JIT diagnostics retain their existing formatting.

## Generated metadata configuration

Three different serialization boundaries require metadata. The configured SQL event resolver also supplies
custom snapshot metadata under native AOT; annotate those metadata types in the application context.
Reflection-enabled JIT retains the existing arbitrary snapshot metadata behavior. Missing native metadata
fails before the snapshot write.

```csharp
services.AddAotEventSerializer(AppJson.Default);
services.AddElasticSearchProjectionStore<ReadModel>(o =>
{
    o.NodeUri = elasticsearchUri;
    o.SourceTypeInfoResolver = AppJson.Default;
    o.Index.EnsureSearchVisibilityOnProjectionCheckpoint = true;
});
// Inside AddEventSourcing:
es.UseSqlServer(sql => sql.ConnectionString(connectionString)
    .EventTypeInfoResolver(AppJson.Default));
es.RegisterEventTypes<Added>();
es.AddProjection<ReadModel>(AppJson.Default.ReadModel, p => p.Inline().When<Added>((view, e) => view.Count += e.Amount));
```

Pass the same context metadata to session reads: `reader.ReadAsync(id, save, AppJson.Default.ReadModel, ct)`.

The generated overload supports Inline and Async. It requires `AddAotEventSerializer` and a supported
outer store capability; unsupported decorators, arbitrary injected clients, and Ephemeral caching are
rejected. Supply a `JsonSerializerContext` directly as the Elasticsearch source resolver. The provider
uses its effective source serializer settings with generated metadata, preserving existing camelCase,
enum, null, and number conventions. Elasticsearch's framework
document envelopes receive framework-generated metadata automatically. Missing application metadata
does not fall back to reflection when a resolver is supplied. The provider keeps Elastic's own
request/response serializer and singleton client lifetime.

The SQL JSON fallback requires both dynamic-code support and JSON reflection to be enabled. Trimmed
JIT and native-AOT defaults therefore require generated metadata. Explicitly re-enabling reflection
in a trimmed application still makes preservation of reflected types the application's responsibility.

The entry point has no warning suppressions; public API and dependency diagnostics fail strict publishing. This evidence does not cover Entra
authentication, Always Encrypted/UDTs, Linux, arbitrary serializers, or arbitrary projection query shapes.


## Generation routing verification (2026-10-29 UTC)

The updated native probe also exercises exact-generation get/query/count using generated JSON metadata for Elasticsearch and SQL Server, verifies that reader pointers prevent retirement of their target binding, and decommissions the Elasticsearch reader with a revision compare-and-set. SQL reads target a different table from the configured writer to detect accidental version requalification or fallback to the writer destination.

The diagnostic runtime run at `artifacts/native-inline/33725e0953b54633a07460f392611443` passed these checks together with API-only, worker-only, concurrent writers, aggregate reload, checkpoint CAS, metadata mapping, append reconciliation and same-process outage recovery. It also verifies terminal retirement through generated reads, queries, counts and guarded writes. The run configures shared storage admission to one; native telemetry verifies an observed peak of one admitted storage operation. An unfiltered live-row control proves retired rows disappear from query/count results rather than merely failing an unrelated filter.

The strict publication verdict at `artifacts/inline-native-aot-final-eac4f95e8dd24c6ba268b11b8248fe15/verdict.json` matches the approved multiset of 58 diagnostics, including the failing MSBuild diagnostic. Exercising deletion initially exposed two additional Dapper diagnostics; SQL deletion now uses a parameterized database command and those diagnostics are absent. The baseline was not relaxed. This is runtime evidence, not NativeAOT certification.
