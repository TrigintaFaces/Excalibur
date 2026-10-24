# Documentation C# validation

Markdown extraction and Git diff selection live in `docs-csharp-extract.py`. C# analysis runs in the RepositoryChecks compiler tool, inside the required Architecture Tests job, including documentation-only changes. The namespace depth rules and other repository policies remain independent requirements.

The comparison uses the pull request base, merge-group base, or preceding push commit. Missing history refuses validation. Only fences intersecting added or changed lines are selected; unchanged fences in the same document may supply contextual declarations. New files select all their fences.

All selected non-ignored C# fences must bind cleanly as consumer code against a fresh Release/net10.0 ProjectReference consumer built by MSBuild with the repository's central dependency pins. The tool records evaluated runtime-library selection and exclusions, then uses MSBuild's resolved compile references; analyzer-private dependencies are not manually promoted into consumer APIs. Installed NuGet assets and dependency exposure remain the separate package-composition gate's responsibility. Both `resolve` (the default) and `compile` (`runnable` or an initial `// compile-check`) now require a compilation without warnings or errors, including contextual and global diagnostics. This deliberately replaces the former partial phantom-type/member heuristics. Unknown example context is reported as unbound, not assumed to be a missing framework API. Fragments must include the necessary declarations and imports; imports are never borrowed from other fences.

Existing explicit teaching-placeholder markers remain: `ignore`, `no-compile`, or an initial `// no-compile`. These take precedence over `runnable`, and ignored fences cannot supply declarations to another fence. Do not mark a broken real API example as ignored to pass the gate.

The tool preserves namespace scopes, aliases, generic arity, accessibility, and extension-method binding. Conflicting declarations or reference identities fail rather than selecting an arbitrary candidate. Python parses Markdown and Git output only.

Build `eng/tools/RepositoryChecks/RepositoryChecks.csproj` in Release, then run its DLL with repository root, `--documentation`, and a JSON report path. Set `DOCS_GATE_BASE_REF` to the intended comparison commit (local default: HEAD, including working-tree changes).

`docs-csharp-extract.test.sh` tests fence classification and Git selection. `DocumentationApiRuleShould` contains compiled positive and negative binding controls. `release-test-verdict-gate.test.py` verifies exact executable CI wiring and immutable comparison inputs.
