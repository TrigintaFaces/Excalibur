; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Analyzer%20Diagnostics.md

### New Rules

Rule ID   | Category                | Severity | Notes
----------|-------------------------|----------|-------
EXDATA001 | Excalibur.Data.Tenancy  | Warning  | A declared absence of a tenant term supplies no justification
EXDATA002 | Excalibur.Data.Tenancy  | Warning  | A data request accepts a tenant partition and never uses it
