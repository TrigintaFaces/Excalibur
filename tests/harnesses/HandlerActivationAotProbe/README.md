# Native handler activation probe

This console probe verifies the shipping `AotHandlerActivator` in an actual native
executable. It deliberately fails when dynamic code is supported; running its DLL
under `dotnet` is not a passing NativeAOT verification.

From the repository root on Windows:

```powershell
dotnet publish tests/harnesses/HandlerActivationAotProbe/HandlerActivationAotProbe.csproj -c Release -r win-x64 -p:BuildExamplesAndTests=true -o artifacts/handler-activation-aot
./artifacts/handler-activation-aot/HandlerActivationAotProbe.exe
```

For another supported platform, use its runtime identifier and native toolchain.
Only a successful publish and execution verify that platform. Compiler, trimming
and AOT warnings remain errors; the probe adds no trimming-warning suppressions.

The probe checks property-only handlers, private setters, inherited properties,
interface and registry precedence, late/replacement registry entries, requested
type boundaries, excluded property shapes, constructor injection, setter
exceptions and 64 transient activations with distinct contexts. The plain service
types avoid automatically generated handler setters, so the property fallback is
actually exercised. Contexts are created explicitly rather than rented from a
context factory; no pool-return claim is made.

The full-dispatch check selects the default native activator and registers handlers
through `AddActionHandler` (with and without a response), `AddEventHandler` and
`AddDocumentHandler`. Each singleton receives three different contexts; each
handler must execute and bind context exactly once per dispatch. Two event
handlers verify fan-out, and the typed action must return42. Registrations on both
sides of `AddDispatch` verify deferred discovery. Closed invokers are explicitly
registered; generated context setters and extra `IHandlerRegistry.Register` calls
are absent, with explicit registry-absence assertions for all five handlers.
The executable prints the number of checks it actually completed.

The earlier ordinary-descriptor native failure (`native-run-r4.log` under the
October benchmark refresh artifacts) and explicit annotated registry positive
control (`native-run-r5.log`) remain preserved. Ordinary DI descriptors alone do
not guarantee preservation of reflective context properties; use the typed helpers or generated
registration. The late-registration dispatch control warms a singleton plan with no registered setter, then verifies first registration and replacement through the actual native dispatcher using the caller's context. This does not establish managed-only fast-delegate coverage; those paths have separate unit controls.

Expected successful output includes `dynamicCodeSupported=False`. This is a
functional probe, not a performance benchmark or certification of every Dispatch
feature under NativeAOT.
