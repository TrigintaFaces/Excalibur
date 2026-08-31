# Testing API Reference

This page is the contributor entry point for testing APIs, helpers, and execution patterns.

## Scope

Use this document when you need to:

- pick the correct test layer
- apply required test traits
- run shard-compatible commands locally
- locate shared test fixtures and context helpers

## Core References

- Test organization and taxonomy: `docs/testing/README.md`
- CI category filtering: `docs/testing/ci-filtering-guide.md`
- Dispatch-focused scenarios: `docs/testing/dispatch-testing-guide.md`

## Common Commands

```bash
dotnet test Excalibur.sln --filter "Category=Unit"
dotnet test Excalibur.sln --filter "Category=Integration|Category=EndToEnd"
dotnet test Excalibur.sln --filter "Category=Conformance"
dotnet test Excalibur.sln --filter "Category=Architecture|Category=Boundary"
```

## Required Traits

Every new test should include at least:

- `Category`
- `Component`
- `Pattern`

This ensures CI shards can include the test and governance reports remain accurate.

## CI Alignment

For local parity with CI:

1. run a clean build once
2. run category shards with `--no-build`
3. use quiet/normal verbosity to keep logs inspectable

## Related Docs

- `docs/testing/test-hierarchy.md`
- `docs/testing/test-fixtures.md`
- `docs/testing/test-containers.md`
