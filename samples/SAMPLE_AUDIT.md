# Samples Audit

This document records the current sample quality baseline.

## Scope

- `samples/**/*.csproj` build audit
- Orphan sample detection (`.cs` files without an associated `.csproj`)
- Sample documentation link consistency checks

## Results

- Buildable sample projects: **57**
- Build failures: **0**
- Orphan sample folders with source files and no project file: **0**

## Actions Completed

1. Removed legacy/orphan sample folders that were not build-validated.
2. Updated sample documentation paths to the categorized folder layout (`samples/01-getting-started/...` etc.).
3. Removed links to non-existent/deprecated sample folders.
4. Added a `DataProcessing`-based CDC history replay path to `09-advanced/CdcAntiCorruption`.

## Quality Standard Going Forward

1. Every runnable sample must have a `.csproj`.
2. Every sample listed in docs must point to an existing, buildable project path.
3. Legacy snippets without projects should live in docs, not in `samples/`.
4. Any new sample change must keep the full samples build audit green.
