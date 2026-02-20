; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Analyzer%20Diagnostics.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|----------
NS1001  | Naming   | Error    | Namespace depth exceeds maximum (6 or more levels)
NS1002  | Naming   | Warning  | Namespace depth at acceptable maximum (5 levels)
NS1004  | Naming   | Warning  | Root namespace does not match assembly name
NS1005  | Naming   | Warning  | Namespace in .Abstractions project missing .Abstractions suffix
