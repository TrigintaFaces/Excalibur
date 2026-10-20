# AOT declaration inventory

Run the gate from the repository root:

```shell
python3 eng/ci/aot-annotation-inventory-gate.py --self-test
python3 eng/ci/aot-annotation-inventory-gate.py
```

The gate evaluates every source project and target framework in Release, including
the SDK's implicit framework symbols and configured language version. The Roslyn
helper records explicit public/protected declaration annotations, including
implicit-public interface members and accessors. It refuses unreadable or
unsupported inputs rather than reporting an empty successful scan. Target-added
and generated compilation inputs are outside this lexical inventory's scope.

The normal command compares current identities with
`eng/ci/aot-annotation-inventory.json` and checks the consumer warning matrix
against current annotations and the suppression baseline. Missing matrix rows for
annotated or suppressed packages, duplicate rows and invalid statuses fail.
`--check-docs` checks only the recorded baseline and does not scan source.

To propose a reviewed baseline change, use `--write-candidate <fresh-path>`.
Review its additions and removals before replacing the baseline. The gate never
rewrites its own baseline; `--regenerate` refuses. The former `.txt` baseline is
retained as legacy migration evidence and is no longer read by the gate.

This is annotation governance, not semantic API equivalence, complete package
catalog validation or NativeAOT certification. Publish and execute the actual
consumer's native binary to verify its required paths. The helper is nonpackable
and its build disables product style analyzers; it does not reference or change
shipping assemblies. Commands have parent-process timeouts and retain their logs;
the caller must coordinate builds and inspect descendants after a timeout.
