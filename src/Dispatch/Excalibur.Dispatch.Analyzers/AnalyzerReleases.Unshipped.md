; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Analyzer%20Diagnostics.md

## Release 1.0.0

### New Rules

Rule ID  | Category                       | Severity | Notes
---------|--------------------------------|----------|-------
DISP101  | Excalibur.Dispatch.Naming      | Warning  | DI extension class in wrong namespace (should be Microsoft.Extensions.DependencyInjection)
DISP102  | Excalibur.Dispatch.Naming      | Warning  | Extension class uses interface-style 'I' prefix
DISP103  | Excalibur.Dispatch.Design      | Warning  | CancellationToken has default value in interface method
DISP104  | Excalibur.Dispatch.Naming      | Warning  | Namespace contains '.Core.' segment (ADR-075 violation)
DISP105  | Excalibur.Dispatch.Design      | Warning  | Missing ConfigureAwait(false) in library code
DISP106  | Excalibur.Dispatch.Design      | Warning  | Blocking call (.Result/.Wait()/.GetResult()) in async method
